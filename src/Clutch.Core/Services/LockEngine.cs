namespace Clutch.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Enums;
    using Clutch.Core.Helpers;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;

    /// <summary>
    /// Orchestrates lock acquisition. The grant decision is always a single database transaction; the
    /// engine adds fail-fast and bounded-wait behavior, retrying the transaction when a waiter is signaled
    /// or a poll interval elapses. No lock decision is ever made from in-memory state.
    /// </summary>
    public class LockEngine
    {
        #region Private-Members

        private readonly ILockHolderMethods _Holders;
        private readonly ILockAuditMethods _Audit;
        private readonly LockCoordinator _Coordinator;
        private readonly LockEngineOptions _Options;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="holders">Lock holder methods.</param>
        /// <param name="audit">Lock audit methods.</param>
        /// <param name="coordinator">Waiter coordinator.</param>
        /// <param name="options">Engine options.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public LockEngine(ILockHolderMethods holders, ILockAuditMethods audit, LockCoordinator coordinator, LockEngineOptions options)
        {
            _Holders = holders ?? throw new ArgumentNullException(nameof(holders));
            _Audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// The coordinator used to wake local waiters.
        /// </summary>
        public LockCoordinator Coordinator
        {
            get
            {
                return _Coordinator;
            }
        }

        /// <summary>
        /// Acquire a lock, optionally waiting up to a timeout.
        /// </summary>
        /// <param name="request">The acquire request.</param>
        /// <param name="behavior">Fail-fast or wait.</param>
        /// <param name="timeoutMs">Maximum wait in milliseconds when behavior is Wait. Null uses the engine maximum.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The lock result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
        public async Task<LockResult> AcquireAsync(AcquireRequest request, LockBehaviorEnum behavior, int? timeoutMs, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrEmpty(request.NodeId)) request.NodeId = _Options.NodeId;

            LockResult result = new LockResult();
            string tenantKey = request.TenantId + "|" + request.LockKey;

            int waitBudget = behavior == LockBehaviorEnum.Wait
                ? Math.Clamp(timeoutMs ?? _Options.MaxWaitMs, 0, _Options.MaxWaitMs)
                : 0;
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(waitBudget);
            bool auditedWaiting = false;

            while (true)
            {
                result.Attempts = result.Attempts + 1;
                AcquireOutcome outcome = await _Holders.TryAcquireAsync(request, _Options.DefaultLeaseMs, token).ConfigureAwait(false);

                if (outcome.Result == AcquireResultEnum.Granted)
                {
                    result.Result = LockResultEnum.Granted;
                    result.Holder = outcome.Holder;
                    result.FencingToken = outcome.FencingToken;
                    result.LeaseExpiresUtc = outcome.LeaseExpiresUtc;
                    return result;
                }

                if (outcome.Result == AcquireResultEnum.PolicyConflict)
                {
                    result.Result = LockResultEnum.PolicyConflict;
                    result.Reason = outcome.Reason;
                    return result;
                }

                // Incompatible.
                if (behavior == LockBehaviorEnum.FailFast)
                {
                    result.Result = LockResultEnum.Denied;
                    result.Reason = outcome.Reason;
                    await AuditAsync(request, LockEventTypeEnum.Denied, outcome.Reason, token).ConfigureAwait(false);
                    return result;
                }

                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0)
                {
                    result.Result = LockResultEnum.Timeout;
                    result.Reason = "Timed out waiting for the lock to become available.";
                    await AuditAsync(request, LockEventTypeEnum.Denied, result.Reason, token).ConfigureAwait(false);
                    return result;
                }

                if (!auditedWaiting)
                {
                    auditedWaiting = true;
                    await AuditAsync(request, LockEventTypeEnum.Waited, outcome.Reason, token).ConfigureAwait(false);
                }

                int wait = Math.Min(remaining, _Options.WaiterPollMs);
                await _Coordinator.WaitAsync(tenantKey, wait, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Release a held lock owned by a session.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="holderId">Holder identifier.</param>
        /// <param name="sessionId">Owning session identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a holder was released.</returns>
        public async Task<bool> ReleaseAsync(string tenantId, string holderId, string sessionId, CancellationToken token = default)
        {
            LockHolder? holder = await _Holders.ReadAsync(tenantId, holderId, token).ConfigureAwait(false);
            bool released = await _Holders.ReleaseAsync(tenantId, holderId, sessionId, token).ConfigureAwait(false);
            if (released && holder != null) _Coordinator.SignalKey(holder.TenantId, holder.LockKey);
            return released;
        }

        /// <summary>
        /// Renew the leases of holders owned by a session.
        /// </summary>
        /// <param name="sessionId">Owning session identifier.</param>
        /// <param name="holderIds">Holder identifiers.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The renewed holders.</returns>
        public async Task<List<LockHolder>> HeartbeatAsync(string sessionId, IEnumerable<string> holderIds, CancellationToken token = default)
        {
            return await _Holders.HeartbeatAsync(sessionId, holderIds, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Release all holders owned by a session and signal local waiters on the affected keys.
        /// </summary>
        /// <param name="sessionId">Owning session identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The affected composite keys.</returns>
        public async Task<List<string>> ReleaseAllForSessionAsync(string sessionId, CancellationToken token = default)
        {
            List<string> keys = await _Holders.ReleaseAllForSessionAsync(sessionId, token).ConfigureAwait(false);
            foreach (string composite in keys) _Coordinator.SignalComposite(composite);
            return keys;
        }

        #endregion

        #region Private-Methods

        private async Task AuditAsync(AcquireRequest request, LockEventTypeEnum eventType, string? reason, CancellationToken token)
        {
            LockAuditEntry entry = new LockAuditEntry();
            entry.Id = IdGenerator.GenerateLockAuditId();
            entry.TenantId = request.TenantId;
            entry.LockKey = request.LockKey;
            entry.Mode = request.Mode;
            entry.EventType = eventType;
            entry.CredentialId = string.IsNullOrEmpty(request.CredentialId) ? null : request.CredentialId;
            entry.SessionId = string.IsNullOrEmpty(request.SessionId) ? null : request.SessionId;
            entry.NodeId = request.NodeId;
            entry.Reason = reason;
            entry.CreatedUtc = DateTime.UtcNow;
            await _Audit.CreateAsync(entry, token).ConfigureAwait(false);
        }

        #endregion
    }
}

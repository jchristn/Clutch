namespace Clutch.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Models;

    /// <summary>
    /// Background service that prunes lock audit history per each tenant's retention setting and prunes
    /// request history per the configured retention. Runs on an interval.
    /// </summary>
    public class RetentionService
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly int _RequestHistoryRetentionDays;
        private readonly int _IntervalMs;
        private CancellationTokenSource? _Cts;
        private Task? _Loop;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="requestHistoryRetentionDays">Retention in days for request history.</param>
        /// <param name="intervalMs">Run interval in milliseconds. Minimum 60000. Defaults are set by the caller.</param>
        /// <exception cref="ArgumentNullException">Thrown when database is null.</exception>
        public RetentionService(DatabaseDriverBase database, int requestHistoryRetentionDays, int intervalMs)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _RequestHistoryRetentionDays = requestHistoryRetentionDays < 1 ? 1 : requestHistoryRetentionDays;
            _IntervalMs = intervalMs < 60000 ? 60000 : intervalMs;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the retention loop.
        /// </summary>
        /// <param name="token">Cancellation token linked to the loop lifetime.</param>
        public void Start(CancellationToken token = default)
        {
            _Cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Loop = Task.Run(() => RunAsync(_Cts.Token));
        }

        /// <summary>
        /// Stop the retention loop.
        /// </summary>
        /// <returns>Awaitable task.</returns>
        public async Task StopAsync()
        {
            if (_Cts != null) _Cts.Cancel();
            if (_Loop != null)
            {
                try
                {
                    await _Loop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            if (_Cts != null) _Cts.Dispose();
        }

        /// <summary>
        /// Run a single retention pass immediately.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        public async Task RunOnceAsync(CancellationToken token = default)
        {
            List<Tenant> tenants = await _Database.Tenants.EnumerateAsync(token).ConfigureAwait(false);
            foreach (Tenant tenant in tenants)
            {
                DateTime auditCutoff = DateTime.UtcNow.AddDays(-tenant.LockHistoryRetentionDays);
                await _Database.LockAudit.PruneAsync(tenant.Id, auditCutoff, token).ConfigureAwait(false);
            }

            DateTime requestCutoff = DateTime.UtcNow.AddDays(-_RequestHistoryRetentionDays);
            await _Database.RequestHistory.PruneAsync(requestCutoff, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_IntervalMs, token).ConfigureAwait(false);
                    await RunOnceAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Swallow transient failures; the next interval retries.
                }
            }
        }

        #endregion
    }
}

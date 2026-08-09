namespace Clutch.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;

    /// <summary>
    /// Lock holder data access, including the atomic acquire path. The acquire is a single database
    /// transaction that serializes per key with a row lock on the definition, so no two nodes can grant
    /// incompatible holds.
    /// </summary>
    public interface ILockHolderMethods
    {
        /// <summary>
        /// Attempt to acquire a lock in a single transaction. Creates the definition if the key is new,
        /// reclaims expired holders, evaluates compatibility, and — when compatible — assigns a fencing
        /// token and inserts the holder. Does not wait; waiting is orchestrated by the caller.
        /// </summary>
        /// <param name="request">The acquire request.</param>
        /// <param name="defaultLeaseMs">Fallback default lease when the caller and definition do not specify one.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The acquire outcome.</returns>
        Task<AcquireOutcome> TryAcquireAsync(AcquireRequest request, int defaultLeaseMs, CancellationToken token = default);

        /// <summary>
        /// Release a held lock, verifying ownership by session and fencing token. Idempotent.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="holderId">Holder identifier.</param>
        /// <param name="sessionId">Owning session identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a holder was released.</returns>
        Task<bool> ReleaseAsync(string tenantId, string holderId, string sessionId, CancellationToken token = default);

        /// <summary>
        /// Renew the leases of the given holders owned by a session, bounded by each key's maximum hold.
        /// </summary>
        /// <param name="sessionId">Owning session identifier.</param>
        /// <param name="holderIds">Holder identifiers to renew.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The holders successfully renewed.</returns>
        Task<List<LockHolder>> HeartbeatAsync(string sessionId, IEnumerable<string> holderIds, CancellationToken token = default);

        /// <summary>
        /// Release all holders owned by a session (for example on WebSocket disconnect).
        /// </summary>
        /// <param name="sessionId">Owning session identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The distinct keys affected, so the caller can wake waiters.</returns>
        Task<List<string>> ReleaseAllForSessionAsync(string sessionId, CancellationToken token = default);

        /// <summary>
        /// Force-release a holder as an administrator or because its principal was disabled.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="holderId">Holder identifier.</param>
        /// <param name="reason">Revocation reason.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The released holder, or null if not found.</returns>
        Task<LockHolder?> RevokeAsync(string tenantId, string holderId, string reason, CancellationToken token = default);

        /// <summary>
        /// Reclaim holders whose lease expired before the given cutoff, across all tenants.
        /// </summary>
        /// <param name="olderThanUtc">Cutoff timestamp.</param>
        /// <param name="nodeId">Node performing the sweep, for audit.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The distinct keys affected.</returns>
        Task<List<string>> PurgeExpiredAsync(DateTime olderThanUtc, string nodeId, CancellationToken token = default);

        /// <summary>
        /// Read a single holder by identifier.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="holderId">Holder identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The holder, or null if not found.</returns>
        Task<LockHolder?> ReadAsync(string tenantId, string holderId, CancellationToken token = default);

        /// <summary>
        /// Enumerate active holders within a tenant, optionally filtered by key substring and mode.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lockKeyContains">Optional case-insensitive key substring.</param>
        /// <param name="mode">Optional mode filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The active holders.</returns>
        Task<List<LockHolder>> EnumerateByTenantAsync(string tenantId, string? lockKeyContains, LockModeEnum? mode, CancellationToken token = default);

        /// <summary>
        /// Enumerate active holders on a specific key.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lockKey">Lock key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The active holders on the key.</returns>
        Task<List<LockHolder>> EnumerateByKeyAsync(string tenantId, string lockKey, CancellationToken token = default);
    }
}

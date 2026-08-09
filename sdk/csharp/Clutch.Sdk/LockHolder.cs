namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// An active holder of a lock on a key.
    /// </summary>
    public class LockHolder
    {
        /// <summary>
        /// The holder identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// The lock key.
        /// </summary>
        public string? LockKey { get; set; }

        /// <summary>
        /// The mode in which the lock is held.
        /// </summary>
        public LockMode Mode { get; set; }

        /// <summary>
        /// The identifier of the credential that acquired the lock.
        /// </summary>
        public string? CredentialId { get; set; }

        /// <summary>
        /// The identifier of the WebSocket session that owns the lock.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// The identifier of the node the holder connected to.
        /// </summary>
        public string? NodeId { get; set; }

        /// <summary>
        /// The per-key monotonic fencing token issued for this hold.
        /// </summary>
        public long FencingToken { get; set; }

        /// <summary>
        /// The UTC timestamp at which the lock was acquired.
        /// </summary>
        public DateTime AcquiredUtc { get; set; }

        /// <summary>
        /// The UTC timestamp at which the current lease expires.
        /// </summary>
        public DateTime LeaseExpiresUtc { get; set; }

        /// <summary>
        /// The UTC timestamp of the last heartbeat received for this hold, when available.
        /// </summary>
        public DateTime? LastHeartbeatUtc { get; set; }
    }
}

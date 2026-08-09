namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// A successfully acquired lock. Pass <see cref="FencingToken"/> to any downstream resource so a stale holder can be rejected.
    /// </summary>
    public class AcquiredLock
    {
        /// <summary>
        /// The key that was locked.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The mode in which the lock was acquired.
        /// </summary>
        public LockMode Mode { get; set; }

        /// <summary>
        /// The holder identifier, used to release the lock and to send heartbeats.
        /// </summary>
        public string HolderId { get; set; } = string.Empty;

        /// <summary>
        /// The per-key monotonic fencing token issued for this hold.
        /// </summary>
        public long FencingToken { get; set; }

        /// <summary>
        /// The UTC timestamp at which the lease expires unless renewed.
        /// </summary>
        public DateTime LeaseExpiresUtc { get; set; }
    }
}

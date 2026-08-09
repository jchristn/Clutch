namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// A Clutch tenant.
    /// </summary>
    public class Tenant
    {
        /// <summary>
        /// The tenant identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The tenant name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The number of days lock history is retained for this tenant.
        /// </summary>
        public int LockHistoryRetentionDays { get; set; }

        /// <summary>
        /// The default lease duration, in milliseconds, applied to new locks.
        /// </summary>
        public int DefaultLeaseMs { get; set; }

        /// <summary>
        /// The maximum lease duration, in milliseconds, permitted for locks.
        /// </summary>
        public int MaxLeaseMs { get; set; }

        /// <summary>
        /// Whether the tenant is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Whether the tenant is protected from deletion.
        /// </summary>
        public bool IsProtected { get; set; }

        /// <summary>
        /// The UTC timestamp at which the tenant was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// The UTC timestamp at which the tenant was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; }
    }
}

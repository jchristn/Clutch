namespace Clutch.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using Clutch.Core.Enums;

    /// <summary>
    /// Filter for the lock-activity chart summary. Buckets acquire events over a time range by lock
    /// name and/or mode.
    /// </summary>
    public class LockChartFilter
    {
        #region Public-Members

        /// <summary>
        /// Tenant scope. A system admin may leave null to span all tenants.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Optional case-insensitive substring match against the lock key.
        /// </summary>
        public string? LockNameContains { get; set; } = null;

        /// <summary>
        /// Optional set of modes to include. Null or empty includes all.
        /// </summary>
        public List<LockModeEnum>? Modes { get; set; } = null;

        /// <summary>
        /// Inclusive lower bound of the range.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-1);

        /// <summary>
        /// Exclusive upper bound of the range.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of buckets to divide the range into. Minimum 1, maximum 1000. Defaults to 96.
        /// </summary>
        public int BucketCount
        {
            get
            {
                return _BucketCount;
            }
            set
            {
                _BucketCount = Math.Clamp(value, 1, 1000);
            }
        }

        #endregion

        #region Private-Members

        private int _BucketCount = 96;

        #endregion
    }
}

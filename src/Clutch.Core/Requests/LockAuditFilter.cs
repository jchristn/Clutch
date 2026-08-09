namespace Clutch.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using Clutch.Core.Enums;

    /// <summary>
    /// Filter for paging through lock audit entries.
    /// </summary>
    public class LockAuditFilter
    {
        #region Public-Members

        /// <summary>
        /// Tenant scope. Required for tenant-scoped callers; a system admin may leave null to span tenants.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Optional case-insensitive substring match against the lock key.
        /// </summary>
        public string? LockKeyContains { get; set; } = null;

        /// <summary>
        /// Optional set of modes to include. Null or empty includes all.
        /// </summary>
        public List<LockModeEnum>? Modes { get; set; } = null;

        /// <summary>
        /// Optional set of event types to include. Null or empty includes all.
        /// </summary>
        public List<LockEventTypeEnum>? EventTypes { get; set; } = null;

        /// <summary>
        /// Optional inclusive lower bound on event time.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Optional exclusive upper bound on event time.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Page number, 1-based. Minimum 1. Defaults to 1.
        /// </summary>
        public int PageNumber
        {
            get
            {
                return _PageNumber;
            }
            set
            {
                _PageNumber = value < 1 ? 1 : value;
            }
        }

        /// <summary>
        /// Page size. Minimum 1, maximum 1000. Defaults to 25.
        /// </summary>
        public int PageSize
        {
            get
            {
                return _PageSize;
            }
            set
            {
                _PageSize = Math.Clamp(value, 1, 1000);
            }
        }

        #endregion

        #region Private-Members

        private int _PageNumber = 1;
        private int _PageSize = 25;

        #endregion
    }
}

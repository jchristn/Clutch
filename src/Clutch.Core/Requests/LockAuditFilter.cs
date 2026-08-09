namespace Clutch.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Enums;

    /// <summary>
    /// Filter for paging through lock audit entries. Pagination fields (MaxResults, Skip, Ordering) are
    /// inherited from <see cref="EnumerationQuery"/>.
    /// </summary>
    public class LockAuditFilter : EnumerationQuery
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

        #endregion
    }
}

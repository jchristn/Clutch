namespace Clutch.Core.Responses
{
    using System.Collections.Generic;
    using Clutch.Core.Models;

    /// <summary>
    /// A page of lock audit entries.
    /// </summary>
    public class LockAuditPage
    {
        #region Public-Members

        /// <summary>
        /// The entries on this page.
        /// </summary>
        public List<LockAuditEntry> Items { get; set; } = new List<LockAuditEntry>();

        /// <summary>
        /// 1-based page number.
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 25;

        /// <summary>
        /// Total matching entries across all pages.
        /// </summary>
        public long TotalCount { get; set; } = 0;

        #endregion
    }
}

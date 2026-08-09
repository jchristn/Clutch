namespace Clutch.Core.Responses
{
    using System.Collections.Generic;
    using Clutch.Core.Models;

    /// <summary>
    /// A page of request history entries. Bodies are omitted from list results to keep payloads small.
    /// </summary>
    public class RequestHistoryPage
    {
        #region Public-Members

        /// <summary>
        /// The entries on this page.
        /// </summary>
        public List<RequestHistoryEntry> Items { get; set; } = new List<RequestHistoryEntry>();

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

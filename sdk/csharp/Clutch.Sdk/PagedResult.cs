namespace Clutch.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// A page of results returned by a paginated endpoint.
    /// </summary>
    /// <typeparam name="T">The type of item contained in the page.</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// The items on this page.
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// The one-based page number.
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// The page size.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// The total number of items matching the query across all pages.
        /// </summary>
        public int TotalCount { get; set; }
    }
}

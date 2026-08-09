namespace Clutch.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single page returned by a paginated (EnumerationQuery) endpoint, with the metadata needed to
    /// fetch subsequent pages.
    /// </summary>
    /// <typeparam name="T">The type of object contained in the page.</typeparam>
    public class EnumerationResult<T>
    {
        /// <summary>
        /// Whether the enumeration succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// The requested maximum page size.
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// The requested skip offset.
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// The total number of records matching the filters, across all pages.
        /// </summary>
        public long TotalRecords { get; set; }

        /// <summary>
        /// The number of matching records remaining after this page.
        /// </summary>
        public long RecordsRemaining { get; set; }

        /// <summary>
        /// Whether this page is the last page of results.
        /// </summary>
        public bool EndOfResults { get; set; }

        /// <summary>
        /// The UTC time the result was produced.
        /// </summary>
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// The records on this page.
        /// </summary>
        public List<T> Objects { get; set; } = new List<T>();
    }
}

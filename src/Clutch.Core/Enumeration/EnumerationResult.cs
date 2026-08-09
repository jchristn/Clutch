namespace Clutch.Core.Enumeration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A single page of an enumeration, with the pagination metadata needed to fetch subsequent pages.
    /// </summary>
    /// <typeparam name="T">Object type.</typeparam>
    public class EnumerationResult<T>
    {
        #region Public-Members

        /// <summary>
        /// Whether the enumeration succeeded.
        /// </summary>
        [JsonPropertyOrder(0)]
        public bool Success { get; set; } = true;

        /// <summary>
        /// Echo of the requested maximum page size.
        /// </summary>
        [JsonPropertyOrder(1)]
        public int MaxResults { get; set; } = 25;

        /// <summary>
        /// Echo of the requested skip offset.
        /// </summary>
        [JsonPropertyOrder(2)]
        public int Skip { get; set; } = 0;

        /// <summary>
        /// Total number of records matching the filters, across all pages.
        /// </summary>
        [JsonPropertyOrder(3)]
        public long TotalRecords { get; set; } = 0;

        /// <summary>
        /// Number of matching records remaining after this page.
        /// </summary>
        [JsonPropertyOrder(4)]
        public long RecordsRemaining { get; set; } = 0;

        /// <summary>
        /// Whether this page is the last page of results.
        /// </summary>
        [JsonPropertyOrder(5)]
        public bool EndOfResults { get; set; } = true;

        /// <summary>
        /// UTC timestamp when the result was produced.
        /// </summary>
        [JsonPropertyOrder(6)]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The records on this page.
        /// </summary>
        [JsonPropertyOrder(999)]
        public List<T> Objects { get; set; } = new List<T>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build a result from a page of objects and the total matching count.
        /// </summary>
        /// <param name="query">The query that produced the page.</param>
        /// <param name="totalRecords">Total matching records across all pages.</param>
        /// <param name="objects">The objects on this page.</param>
        /// <returns>A populated enumeration result.</returns>
        public static EnumerationResult<T> Build(EnumerationQuery query, long totalRecords, List<T> objects)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            List<T> page = objects ?? new List<T>();
            long consumed = (long)query.Skip + page.Count;
            long remaining = totalRecords - consumed;
            if (remaining < 0) remaining = 0;
            return new EnumerationResult<T>
            {
                Success = true,
                MaxResults = query.MaxResults,
                Skip = query.Skip,
                TotalRecords = totalRecords,
                RecordsRemaining = remaining,
                EndOfResults = remaining <= 0,
                TimestampUtc = DateTime.UtcNow,
                Objects = page
            };
        }

        #endregion
    }
}

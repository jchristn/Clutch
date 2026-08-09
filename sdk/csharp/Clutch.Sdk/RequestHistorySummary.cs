namespace Clutch.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// A summary of request history over a time range, bucketed for charting.
    /// </summary>
    public class RequestHistorySummary
    {
        /// <summary>
        /// The total number of requests matching the query.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// The total number of successful requests.
        /// </summary>
        public int TotalSuccess { get; set; }

        /// <summary>
        /// The total number of failed requests.
        /// </summary>
        public int TotalFailure { get; set; }

        /// <summary>
        /// The average request duration across the range, in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; }

        /// <summary>
        /// The per-bucket breakdown.
        /// </summary>
        public List<RequestHistoryBucket> Buckets { get; set; } = new List<RequestHistoryBucket>();
    }
}

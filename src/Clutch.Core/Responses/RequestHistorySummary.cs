namespace Clutch.Core.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Time-bucketed request history counts and averages for chart rendering. Every bucket in the range
    /// is emitted, including empty ones.
    /// </summary>
    public class RequestHistorySummary
    {
        #region Public-Members

        /// <summary>
        /// Total matching requests.
        /// </summary>
        public long TotalCount { get; set; } = 0;

        /// <summary>
        /// Total successful (2xx/3xx) requests.
        /// </summary>
        public long TotalSuccess { get; set; } = 0;

        /// <summary>
        /// Total failed (4xx/5xx) requests.
        /// </summary>
        public long TotalFailure { get; set; } = 0;

        /// <summary>
        /// Average duration across all matching requests, in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; } = 0;

        /// <summary>
        /// The time buckets.
        /// </summary>
        public List<RequestHistoryBucket> Buckets { get; set; } = new List<RequestHistoryBucket>();

        #endregion
    }
}

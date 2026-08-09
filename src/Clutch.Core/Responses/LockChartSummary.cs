namespace Clutch.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Time-bucketed lock activity for the dashboard chart. The server emits every bucket, including
    /// empty ones, and one series per lock key and/or mode.
    /// </summary>
    public class LockChartSummary
    {
        #region Public-Members

        /// <summary>
        /// Inclusive lower bound of the range.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-1);

        /// <summary>
        /// Exclusive upper bound of the range.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of buckets.
        /// </summary>
        public int BucketCount { get; set; } = 0;

        /// <summary>
        /// The UTC start time of each bucket. Length equals BucketCount.
        /// </summary>
        public DateTime[] BucketStartsUtc { get; set; } = Array.Empty<DateTime>();

        /// <summary>
        /// The series, one per lock key and/or mode.
        /// </summary>
        public List<LockChartSeries> Series { get; set; } = new List<LockChartSeries>();

        #endregion
    }
}

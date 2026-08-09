namespace Clutch.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A time-bucketed summary of lock acquire activity, suitable for charting.
    /// </summary>
    public class LockAuditSummary
    {
        /// <summary>
        /// The inclusive start of the summarized time range, in UTC.
        /// </summary>
        public DateTime FromUtc { get; set; }

        /// <summary>
        /// The exclusive end of the summarized time range, in UTC.
        /// </summary>
        public DateTime ToUtc { get; set; }

        /// <summary>
        /// The number of buckets in the summary.
        /// </summary>
        public int BucketCount { get; set; }

        /// <summary>
        /// The UTC start timestamp of each bucket.
        /// </summary>
        public List<DateTime> BucketStartsUtc { get; set; } = new List<DateTime>();

        /// <summary>
        /// The per-key, per-mode count series.
        /// </summary>
        public List<LockAuditSeries> Series { get; set; } = new List<LockAuditSeries>();
    }
}

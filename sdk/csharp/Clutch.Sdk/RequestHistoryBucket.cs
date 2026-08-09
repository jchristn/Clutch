namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// A single time bucket in a request history summary.
    /// </summary>
    public class RequestHistoryBucket
    {
        /// <summary>
        /// The UTC start of the bucket.
        /// </summary>
        public DateTime BucketStartUtc { get; set; }

        /// <summary>
        /// The UTC end of the bucket.
        /// </summary>
        public DateTime BucketEndUtc { get; set; }

        /// <summary>
        /// The number of successful requests in the bucket.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// The number of failed requests in the bucket.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// The average request duration in the bucket, in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; }
    }
}

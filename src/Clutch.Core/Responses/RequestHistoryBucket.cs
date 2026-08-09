namespace Clutch.Core.Responses
{
    using System;

    /// <summary>
    /// A single time bucket in a request history summary.
    /// </summary>
    public class RequestHistoryBucket
    {
        #region Public-Members

        /// <summary>
        /// Inclusive bucket start.
        /// </summary>
        public DateTime BucketStartUtc { get; set; }

        /// <summary>
        /// Exclusive bucket end.
        /// </summary>
        public DateTime BucketEndUtc { get; set; }

        /// <summary>
        /// Count of 2xx/3xx responses in the bucket.
        /// </summary>
        public long SuccessCount { get; set; } = 0;

        /// <summary>
        /// Count of 4xx/5xx responses in the bucket.
        /// </summary>
        public long FailureCount { get; set; } = 0;

        /// <summary>
        /// Average response duration in the bucket, in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; } = 0;

        #endregion
    }
}

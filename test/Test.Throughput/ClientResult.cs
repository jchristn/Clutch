namespace Test.Throughput
{
    using System.Collections.Generic;

    /// <summary>
    /// Accumulated results for one emulated client. Indices 0/1/2 correspond to Read/Write/Delete.
    /// </summary>
    public class ClientResult
    {
        /// <summary>
        /// Acquire attempts by mode.
        /// </summary>
        public long[] AcquireAttempts { get; } = new long[3];

        /// <summary>
        /// Granted acquires by mode.
        /// </summary>
        public long[] AcquireGranted { get; } = new long[3];

        /// <summary>
        /// Denied acquires by mode.
        /// </summary>
        public long[] AcquireDenied { get; } = new long[3];

        /// <summary>
        /// Total releases.
        /// </summary>
        public long Releases { get; set; } = 0;

        /// <summary>
        /// Total errors.
        /// </summary>
        public long Errors { get; set; } = 0;

        /// <summary>
        /// Acquire round-trip latencies in milliseconds.
        /// </summary>
        public List<double> AcquireLatencyMs { get; } = new List<double>();
    }
}

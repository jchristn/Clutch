namespace Clutch.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// A single time-bucketed series in a lock audit summary.
    /// </summary>
    public class LockAuditSeries
    {
        /// <summary>
        /// The lock key the series pertains to.
        /// </summary>
        public string? LockKey { get; set; }

        /// <summary>
        /// The lock mode the series pertains to.
        /// </summary>
        public LockMode Mode { get; set; }

        /// <summary>
        /// A display label combining key and mode.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// The acquire counts per bucket, aligned to the summary's bucket starts.
        /// </summary>
        public List<int> Counts { get; set; } = new List<int>();
    }
}

namespace Clutch.Core.Responses
{
    using Clutch.Core.Enums;

    /// <summary>
    /// One series in a lock-activity chart: the per-bucket counts for a lock key and/or mode.
    /// </summary>
    public class LockChartSeries
    {
        #region Public-Members

        /// <summary>
        /// The lock key this series represents.
        /// </summary>
        public string LockKey { get; set; } = string.Empty;

        /// <summary>
        /// The lock mode this series represents, if the series is split by mode.
        /// </summary>
        public LockModeEnum? Mode { get; set; } = null;

        /// <summary>
        /// A display label for the series (for example "orders:Write").
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Per-bucket counts. Length equals the summary's bucket count.
        /// </summary>
        public long[] Counts { get; set; } = System.Array.Empty<long>();

        #endregion
    }
}

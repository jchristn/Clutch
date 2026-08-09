namespace Clutch.Core.Models
{
    /// <summary>
    /// A snapshot of the number of active holders on a key, by mode.
    /// </summary>
    public class HolderCounts
    {
        #region Public-Members

        /// <summary>
        /// Number of active read holders.
        /// </summary>
        public int Read { get; set; } = 0;

        /// <summary>
        /// Number of active write holders.
        /// </summary>
        public int Write { get; set; } = 0;

        /// <summary>
        /// Number of active delete holders.
        /// </summary>
        public int Delete { get; set; } = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Total active holders across all modes.
        /// </summary>
        /// <returns>Total count.</returns>
        public int Total()
        {
            return Read + Write + Delete;
        }

        #endregion
    }
}

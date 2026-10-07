namespace Clutch.Server.Responses
{
    /// <summary>
    /// Response for releasing a single holder.
    /// </summary>
    public class LockReleaseResponse
    {
        #region Public-Members

        /// <summary>
        /// Lock key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Holder identifier.
        /// </summary>
        public string? HolderId { get; set; } = null;

        /// <summary>
        /// True when the holder was released.
        /// </summary>
        public bool Released { get; set; } = false;

        #endregion
    }
}

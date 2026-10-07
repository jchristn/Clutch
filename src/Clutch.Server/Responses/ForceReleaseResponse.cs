namespace Clutch.Server.Responses
{
    /// <summary>
    /// Response for an administrative force-release of a key.
    /// </summary>
    public class ForceReleaseResponse
    {
        #region Public-Members

        /// <summary>
        /// Lock key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Number of holders revoked.
        /// </summary>
        public int Released { get; set; } = 0;

        #endregion
    }
}

namespace Clutch.Server.Responses
{
    /// <summary>
    /// Response for a denied HTTP lock acquire.
    /// </summary>
    public class LockAcquireDeniedResponse
    {
        #region Public-Members

        /// <summary>
        /// Acquire result.
        /// </summary>
        public string Result { get; set; } = string.Empty;

        /// <summary>
        /// Always false.
        /// </summary>
        public bool Granted { get; set; } = false;

        /// <summary>
        /// Lock key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Lock session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Denial reason.
        /// </summary>
        public string? Reason { get; set; } = null;

        #endregion
    }
}

namespace Clutch.Server.Responses
{
    using System;

    /// <summary>
    /// Response for a granted HTTP lock acquire.
    /// </summary>
    public class LockAcquireGrantedResponse
    {
        #region Public-Members

        /// <summary>
        /// Acquire result.
        /// </summary>
        public string Result { get; set; } = string.Empty;

        /// <summary>
        /// Always true.
        /// </summary>
        public bool Granted { get; set; } = true;

        /// <summary>
        /// Lock key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Granted lock mode.
        /// </summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>
        /// Holder identifier.
        /// </summary>
        public string HolderId { get; set; } = string.Empty;

        /// <summary>
        /// Lock session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Fencing token.
        /// </summary>
        public long FencingToken { get; set; } = 0;

        /// <summary>
        /// Lease expiry (UTC).
        /// </summary>
        public DateTime? LeaseExpiresUtc { get; set; } = null;

        #endregion
    }
}

namespace Clutch.Server.Responses
{
    using System;

    /// <summary>
    /// WebSocket frame confirming a granted lock.
    /// </summary>
    public class WsAcquiredMessage
    {
        #region Public-Members

        /// <summary>
        /// Frame type.
        /// </summary>
        public string Type { get; set; } = "acquired";

        /// <summary>
        /// Correlating request identifier.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Lock key.
        /// </summary>
        public string? Key { get; set; } = null;

        /// <summary>
        /// Granted lock mode.
        /// </summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>
        /// Holder identifier.
        /// </summary>
        public string HolderId { get; set; } = string.Empty;

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

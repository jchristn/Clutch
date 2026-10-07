namespace Clutch.Server.Responses
{
    /// <summary>
    /// WebSocket frame confirming a release.
    /// </summary>
    public class WsReleasedMessage
    {
        #region Public-Members

        /// <summary>
        /// Frame type.
        /// </summary>
        public string Type { get; set; } = "released";

        /// <summary>
        /// Correlating request identifier.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Lock key.
        /// </summary>
        public string? Key { get; set; } = null;

        /// <summary>
        /// Holder identifier.
        /// </summary>
        public string? HolderId { get; set; } = null;

        /// <summary>
        /// True when a holder was released.
        /// </summary>
        public bool Released { get; set; } = false;

        #endregion
    }
}

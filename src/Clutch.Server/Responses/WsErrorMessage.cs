namespace Clutch.Server.Responses
{
    /// <summary>
    /// WebSocket error frame.
    /// </summary>
    public class WsErrorMessage
    {
        #region Public-Members

        /// <summary>
        /// Frame type.
        /// </summary>
        public string Type { get; set; } = "error";

        /// <summary>
        /// Correlating request identifier, when known.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        #endregion
    }
}

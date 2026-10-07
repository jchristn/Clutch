namespace Clutch.Server.Responses
{
    /// <summary>
    /// WebSocket frame reporting a denied lock request.
    /// </summary>
    public class WsDeniedMessage
    {
        #region Public-Members

        /// <summary>
        /// Frame type.
        /// </summary>
        public string Type { get; set; } = "denied";

        /// <summary>
        /// Correlating request identifier.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Lock key.
        /// </summary>
        public string? Key { get; set; } = null;

        /// <summary>
        /// Acquire result.
        /// </summary>
        public string Result { get; set; } = string.Empty;

        /// <summary>
        /// Denial reason.
        /// </summary>
        public string? Reason { get; set; } = null;

        #endregion
    }
}

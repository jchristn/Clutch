namespace Clutch.Server.Responses
{
    /// <summary>
    /// WebSocket frame that carries only a type (for example, pong).
    /// </summary>
    public class WsTypeMessage
    {
        #region Public-Members

        /// <summary>
        /// Frame type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        #endregion
    }
}

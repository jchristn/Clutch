namespace Clutch.Server.Responses
{
    /// <summary>
    /// Server product, version, node, and caller details.
    /// </summary>
    public class ServerInfoResponse
    {
        #region Public-Members

        /// <summary>
        /// Product name.
        /// </summary>
        public string Product { get; set; } = "Clutch";

        /// <summary>
        /// Server version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Node identifier.
        /// </summary>
        public string Node { get; set; } = string.Empty;

        /// <summary>
        /// Database provider.
        /// </summary>
        public string Database { get; set; } = string.Empty;

        /// <summary>
        /// Open WebSocket lock sessions.
        /// </summary>
        public int WebSocketConnections { get; set; } = 0;

        /// <summary>
        /// Telemetry settings.
        /// </summary>
        public ServerTelemetryInfo Telemetry { get; set; } = new ServerTelemetryInfo();

        /// <summary>
        /// The calling principal.
        /// </summary>
        public ServerPrincipalInfo Principal { get; set; } = new ServerPrincipalInfo();

        #endregion
    }
}

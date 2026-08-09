namespace Clutch.Sdk
{
    /// <summary>
    /// Information about the running Clutch server.
    /// </summary>
    public class ServerInfo
    {
        /// <summary>
        /// The product name.
        /// </summary>
        public string? Product { get; set; }

        /// <summary>
        /// The server version.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// The identifier of the node that served the request.
        /// </summary>
        public string? Node { get; set; }

        /// <summary>
        /// The database backend in use, for example "Postgresql".
        /// </summary>
        public string? Database { get; set; }

        /// <summary>
        /// The number of active WebSocket lock connections.
        /// </summary>
        public int WebSocketConnections { get; set; }

        /// <summary>
        /// Telemetry configuration reported by the server.
        /// </summary>
        public TelemetryInfo? Telemetry { get; set; }

        /// <summary>
        /// A summary of the authenticated principal.
        /// </summary>
        public PrincipalInfo? Principal { get; set; }
    }
}

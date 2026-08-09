namespace Clutch.Server.Settings
{
    using System;

    /// <summary>
    /// Model Context Protocol (MCP) server settings. When enabled, Clutch hosts a native MCP endpoint
    /// (streamable HTTP with Server-Sent Events) that exposes read-only observability tools to AI agents.
    /// The MCP server binds its own port, alongside the REST/WebSocket server.
    /// </summary>
    public class McpSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the MCP server is enabled. Defaults to true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Hostname or IP address the MCP server binds to. Defaults to "localhost". Use "*" or "0.0.0.0"
        /// to listen on all interfaces (required inside a container).
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// TCP port the MCP server listens on. Minimum 1, maximum 65535. Defaults to 8100.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = Math.Clamp(value, 1, 65535);
            }
        }

        /// <summary>
        /// The streamable-HTTP MCP endpoint path (POST for JSON-RPC, GET for the SSE stream). Defaults to "/mcp".
        /// </summary>
        public string McpPath
        {
            get
            {
                return _McpPath;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(McpPath));
                _McpPath = value;
            }
        }

        /// <summary>
        /// The MCP server name advertised to clients. Defaults to "Clutch MCP".
        /// </summary>
        public string ServerName { get; set; } = "Clutch MCP";

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8100;
        private string _McpPath = "/mcp";

        #endregion
    }
}

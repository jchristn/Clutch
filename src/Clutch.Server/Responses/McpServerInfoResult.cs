namespace Clutch.Server.Responses
{
    /// <summary>
    /// Result of the clutch_server_info MCP tool.
    /// </summary>
    public class McpServerInfoResult
    {
        #region Public-Members

        /// <summary>
        /// Product name.
        /// </summary>
        public string Product { get; set; } = string.Empty;

        /// <summary>
        /// Server version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Node identifier.
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        #endregion
    }
}

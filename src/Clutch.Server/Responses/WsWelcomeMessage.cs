namespace Clutch.Server.Responses
{
    /// <summary>
    /// WebSocket welcome frame sent when a lock session connects.
    /// </summary>
    public class WsWelcomeMessage
    {
        #region Public-Members

        /// <summary>
        /// Frame type.
        /// </summary>
        public string Type { get; set; } = "welcome";

        /// <summary>
        /// Lock session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Tenant the session is bound to.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Default lease duration in milliseconds.
        /// </summary>
        public int DefaultLeaseMs { get; set; } = 0;

        /// <summary>
        /// Recommended heartbeat interval in milliseconds.
        /// </summary>
        public int HeartbeatIntervalMs { get; set; } = 0;

        #endregion
    }
}

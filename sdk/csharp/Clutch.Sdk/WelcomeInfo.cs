namespace Clutch.Sdk
{
    /// <summary>
    /// The welcome frame sent by the server immediately after a successful lock connection.
    /// </summary>
    public class WelcomeInfo
    {
        /// <summary>
        /// The session identifier that owns any locks acquired on this connection.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// The tenant identifier resolved from the application key.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// The default lease duration, in milliseconds.
        /// </summary>
        public int DefaultLeaseMs { get; set; }

        /// <summary>
        /// The recommended interval, in milliseconds, at which to send heartbeats to keep held leases alive.
        /// </summary>
        public int HeartbeatIntervalMs { get; set; }
    }
}

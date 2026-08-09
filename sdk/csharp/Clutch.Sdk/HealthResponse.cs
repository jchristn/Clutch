namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// The result of a server health check.
    /// </summary>
    public class HealthResponse
    {
        /// <summary>
        /// The health status, for example "healthy" or "degraded".
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// The identifier of the node that served the request.
        /// </summary>
        public string? Node { get; set; }

        /// <summary>
        /// Whether the database backing the server is reachable.
        /// </summary>
        public bool Database { get; set; }

        /// <summary>
        /// The UTC timestamp at which health was evaluated.
        /// </summary>
        public DateTime? Utc { get; set; }
    }
}

namespace Clutch.Sdk
{
    /// <summary>
    /// Telemetry configuration reported by the server.
    /// </summary>
    public class TelemetryInfo
    {
        /// <summary>
        /// Whether Prometheus telemetry is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The port on which Prometheus metrics are exposed.
        /// </summary>
        public int PrometheusPort { get; set; }

        /// <summary>
        /// The path on which Prometheus metrics are exposed.
        /// </summary>
        public string? PrometheusPath { get; set; }
    }
}

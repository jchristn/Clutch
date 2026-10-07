namespace Clutch.Server.Responses
{
    /// <summary>
    /// Telemetry details reported by server info.
    /// </summary>
    public class ServerTelemetryInfo
    {
        #region Public-Members

        /// <summary>
        /// True when telemetry is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Prometheus scrape port.
        /// </summary>
        public int PrometheusPort { get; set; } = 0;

        /// <summary>
        /// Prometheus scrape path.
        /// </summary>
        public string PrometheusPath { get; set; } = string.Empty;

        #endregion
    }
}

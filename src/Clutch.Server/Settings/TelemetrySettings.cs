namespace Clutch.Server.Settings
{
    using System;

    /// <summary>
    /// Telemetry and observability settings, modeled on the Radiant OpenTelemetry host. Metrics are
    /// exposed on a dedicated Prometheus scrape port, independent of the REST port.
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Whether telemetry is enabled. Defaults to true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// OpenTelemetry service name. Defaults to "clutch".
        /// </summary>
        public string ServiceName
        {
            get
            {
                return _ServiceName;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(ServiceName));
                _ServiceName = value;
            }
        }

        /// <summary>
        /// Whether the in-process Prometheus scrape endpoint is enabled. Defaults to true.
        /// </summary>
        public bool PrometheusEnable { get; set; } = true;

        /// <summary>
        /// Hostname the Prometheus scrape listener binds to. Defaults to "localhost". The listener binds the
        /// address this name resolves to and answers only requests addressed to the same name, so scrape it by
        /// this name (in Docker Compose, the service name). Wildcards are not supported by the underlying
        /// OpenTelemetry listener: "*", "+", "0.0.0.0", "::", and "[::]" are treated as "localhost".
        /// </summary>
        public string PrometheusHostname
        {
            get
            {
                return _PrometheusHostname;
            }
            set
            {
                _PrometheusHostname = IsWildcardHostname(value) ? "localhost" : value.Trim();
            }
        }

        /// <summary>
        /// Port the Prometheus scrape listener binds to. Minimum 1, maximum 65535. Defaults to 9464.
        /// </summary>
        public int PrometheusPort
        {
            get
            {
                return _PrometheusPort;
            }
            set
            {
                _PrometheusPort = Math.Clamp(value, 1, 65535);
            }
        }

        /// <summary>
        /// Path served by the Prometheus scrape listener. Defaults to "/metrics".
        /// </summary>
        public string PrometheusPath
        {
            get
            {
                return _PrometheusPath;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) _PrometheusPath = "/metrics";
                else _PrometheusPath = value;
            }
        }

        /// <summary>
        /// Whether OTLP push export is enabled. Defaults to false.
        /// </summary>
        public bool OtlpEnable { get; set; } = false;

        /// <summary>
        /// OTLP collector endpoint used when OTLP push export is enabled.
        /// </summary>
        public string? OtlpEndpoint { get; set; } = null;

        /// <summary>
        /// Whether .NET runtime instrumentation (GC/heap/JIT) is included. Defaults to true.
        /// </summary>
        public bool MetricsIncludeRuntime { get; set; } = true;

        /// <summary>
        /// Whether process instrumentation (memory/uptime/threads) is included. Defaults to true.
        /// </summary>
        public bool MetricsIncludeProcess { get; set; } = true;

        #endregion

        #region Private-Members

        private string _ServiceName = "clutch";
        private string _PrometheusHostname = "localhost";
        private int _PrometheusPort = 9464;
        private string _PrometheusPath = "/metrics";

        #endregion

        #region Private-Methods

        private static bool IsWildcardHostname(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return true;
            string trimmed = value.Trim();
            return trimmed == "*" || trimmed == "+" || trimmed == "0.0.0.0" || trimmed == "::" || trimmed == "[::]";
        }

        #endregion
    }
}

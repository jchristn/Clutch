namespace Clutch.Server.Telemetry
{
    using System;
    using Clutch.Server.Settings;
    using Radiant;

    /// <summary>
    /// Owns the Radiant OpenTelemetry host and centralizes Clutch metric emission. Emits through the
    /// Radiant client so metric definitions stay in one place. If the host cannot start (for example the
    /// Prometheus port is unavailable), telemetry degrades to a no-op rather than failing the server.
    /// </summary>
    public class ClutchTelemetry : IDisposable
    {
        #region Private-Members

        private readonly RadiantHost? _Host;
        private readonly RadiantClient? _Client;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate and start the telemetry host.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="nodeId">Node identifier, used as the service instance id.</param>
        /// <param name="diagnostic">Optional diagnostic callback for internal warnings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public ClutchTelemetry(TelemetrySettings settings, string nodeId, Action<string>? diagnostic)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            try
            {
                RadiantSettings radiant = new RadiantSettings();
                radiant.Enable = settings.Enabled;
                radiant.ServiceName = string.IsNullOrEmpty(settings.ServiceName) ? "clutch" : settings.ServiceName;
                radiant.ServiceInstanceId = string.IsNullOrEmpty(nodeId) ? null : nodeId;
                radiant.DiagnosticCallback = diagnostic;
                radiant.Metrics.IncludeRuntime = settings.MetricsIncludeRuntime;
                radiant.Metrics.IncludeProcess = settings.MetricsIncludeProcess;
                radiant.Prometheus.Enable = settings.Enabled && settings.PrometheusEnable;
                radiant.Prometheus.Hostname = settings.PrometheusHostname == "*" ? "+" : settings.PrometheusHostname;
                radiant.Prometheus.Port = settings.PrometheusPort;
                radiant.Prometheus.Path = settings.PrometheusPath;

                _Host = RadiantHost.Start(radiant);
                _Client = _Host.Client;
            }
            catch (Exception e)
            {
                if (diagnostic != null) diagnostic("telemetry disabled: " + e.Message);
                _Host = null;
                _Client = null;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Record an HTTP request outcome.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="statusCode">Response status code.</param>
        /// <param name="durationSeconds">Request duration in seconds.</param>
        public void RecordHttp(string method, int statusCode, double durationSeconds)
        {
            if (_Client == null) return;
            _Client.Increment("clutch.http.request", 1.0, new RadiantTag("method", method), new RadiantTag("status", statusCode));
            _Client.Record("http.server.request.duration", durationSeconds, new RadiantTag("method", method), new RadiantTag("status", statusCode));
        }

        /// <summary>
        /// Record an acquire outcome.
        /// </summary>
        /// <param name="mode">Lock mode.</param>
        /// <param name="outcome">Outcome (granted, denied, timeout, error, conflict).</param>
        /// <param name="durationSeconds">Total acquire duration in seconds, including any waiting.</param>
        public void RecordAcquire(string mode, string outcome, double durationSeconds)
        {
            if (_Client == null) return;
            _Client.Increment("clutch.lock.acquire", 1.0, new RadiantTag("mode", mode), new RadiantTag("outcome", outcome));
            _Client.Record("clutch.lock.acquire.duration", durationSeconds, new RadiantTag("mode", mode), new RadiantTag("outcome", outcome));
        }

        /// <summary>
        /// Record a release.
        /// </summary>
        /// <param name="mode">Lock mode, if known.</param>
        public void RecordRelease(string mode)
        {
            if (_Client == null) return;
            _Client.Increment("clutch.lock.release", 1.0, new RadiantTag("mode", mode));
        }

        /// <summary>
        /// Register observable gauges for live connection and waiter counts.
        /// </summary>
        /// <param name="wsConnections">Callback returning active WebSocket connections.</param>
        /// <param name="waiters">Callback returning blocked waiters.</param>
        public void RegisterGauges(Func<double> wsConnections, Func<double> waiters)
        {
            if (_Client == null) return;
            _Client.RegisterGauge("clutch.ws.connections", wsConnections, "{connection}");
            _Client.RegisterGauge("clutch.lock.waiters", waiters, "{waiter}");
        }

        /// <summary>
        /// Dispose the telemetry host.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            if (_Host != null) _Host.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

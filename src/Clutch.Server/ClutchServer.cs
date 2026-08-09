namespace Clutch.Server
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core;
    using Clutch.Core.Database;
    using Clutch.Core.Database.Postgresql;
    using Clutch.Core.Database.Postgresql.Notifications;
    using Clutch.Core.Services;
    using Clutch.Server.Routes;
    using Clutch.Server.Services;
    using Clutch.Server.Settings;
    using Clutch.Server.Telemetry;
    using Clutch.Server.WebSocket;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Clutch server host. Owns the Watson webserver, the lock engine and its background services, and the
    /// WebSocket lock endpoint.
    /// </summary>
    public class ClutchServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Effective server settings.
        /// </summary>
        public ClutchSettings Settings { get; }

        #endregion

        #region Private-Members

        private readonly LoggingModule _Logging;
        private readonly DatabaseDriverBase _Database;
        private readonly AuthenticationService _AuthenticationService;
        private readonly AuthorizationService _AuthorizationService;
        private readonly Webserver _Server;

        private readonly LockCoordinator _Coordinator;
        private readonly LockEngine _Engine;
        private readonly LeaseSweeper _Sweeper;
        private readonly RetentionService _Retention;
        private readonly WebSocketConnectionManager _WsManager;
        private readonly LockWebSocketHandler _WsHandler;
        private readonly PostgresNotificationListener? _Listener;
        private readonly RequestHistoryCaptureService _Capture;
        private readonly ClutchTelemetry _Telemetry;
        private readonly ClutchMcpServer _Mcp;

        private readonly string _Header = "[ClutchServer] ";
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the server host.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="database">Initialized database driver.</param>
        /// <param name="authenticationService">Authentication service.</param>
        /// <param name="authorizationService">Authorization service.</param>
        /// <param name="telemetry">Telemetry host.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public ClutchServer(
            ClutchSettings settings,
            LoggingModule logging,
            DatabaseDriverBase database,
            AuthenticationService authenticationService,
            AuthorizationService authorizationService,
            ClutchTelemetry telemetry)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (authenticationService == null) throw new ArgumentNullException(nameof(authenticationService));
            if (authorizationService == null) throw new ArgumentNullException(nameof(authorizationService));
            if (telemetry == null) throw new ArgumentNullException(nameof(telemetry));

            Settings = settings;
            _Logging = logging;
            _Database = database;
            _AuthenticationService = authenticationService;
            _AuthorizationService = authorizationService;
            _Telemetry = telemetry;

            _Coordinator = new LockCoordinator();

            LockEngineOptions options = new LockEngineOptions();
            options.NodeId = settings.NodeId;
            options.DefaultLeaseMs = settings.Lock.DefaultLeaseMs;
            options.MaxWaitMs = settings.Lock.MaxWaitMs;
            options.WaiterPollMs = settings.Lock.WaiterPollMs;
            _Engine = new LockEngine(database.LockHolders, database.LockAudit, _Coordinator, options);

            _Sweeper = new LeaseSweeper(database.LockHolders, _Coordinator, settings.NodeId, settings.Lock.SweepIntervalMs);
            _Retention = new RetentionService(database, settings.RequestHistory.RetentionDays, 3600000);
            _WsManager = new WebSocketConnectionManager();

            int heartbeatInterval = Math.Max(1000, settings.Lock.DefaultLeaseMs / 3);
            _WsHandler = new LockWebSocketHandler(authenticationService, _Engine, _WsManager, logging, telemetry, settings.NodeId, settings.Lock.DefaultLeaseMs, heartbeatInterval);
            _Capture = new RequestHistoryCaptureService(database, settings.RequestHistory, logging);

            if (database is PostgresqlDatabaseDriver postgres)
            {
                _Listener = new PostgresNotificationListener(postgres, Constants.LockReleaseChannel);
                _Listener.KeyReleased += (tenantId, lockKey) => _Coordinator.SignalKey(tenantId, lockKey);
            }

            WebserverSettings webserverSettings = new WebserverSettings();
            webserverSettings.Hostname = Settings.Rest.Hostname;
            webserverSettings.Port = Settings.Rest.Port;
            webserverSettings.Ssl.Enable = Settings.Rest.Ssl;
            webserverSettings.WebSockets.Enable = true;

            _Server = new Webserver(webserverSettings, DefaultRouteAsync);

            _Mcp = new ClutchMcpServer(settings.Mcp, database, logging, settings.NodeId, "Clutch", "v1.0");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Configure the pipeline and routes, then start listening and the background services.
        /// </summary>
        public void Start()
        {
            ConfigureServer();
            ConfigureRoutes();
            _Server.Start();

            _Telemetry.RegisterGauges(() => _WsManager.Count, () => _Coordinator.WaiterCount);
            _Sweeper.Start();
            _Retention.Start();
            if (_Listener != null)
            {
                _Listener.StartAsync().GetAwaiter().GetResult();
                _Logging.Debug(_Header + "notification listener started");
            }
            _Mcp.Start();
        }

        /// <summary>
        /// Stop listening and the background services.
        /// </summary>
        public void Stop()
        {
            _Mcp.Stop();
            _Server.Stop();
            _Sweeper.StopAsync().GetAwaiter().GetResult();
            _Retention.StopAsync().GetAwaiter().GetResult();
            if (_Listener != null) _Listener.DisposeAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Dispose the server host.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private void ConfigureServer()
        {
            _Server.Routes.AuthenticateRequest = _AuthenticationService.AuthenticateRequestAsync;
            _Server.Routes.Preflight = PreflightRouteAsync;
            _Server.Routes.PostRouting = PostRoutingRouteAsync;

            _Server.UseOpenApi(openApi =>
            {
                openApi.Info.Title = "Clutch API";
                openApi.Info.Version = "v1.0";
                openApi.Info.Description = "Clutch distributed lock management platform.";
            });
        }

        private void ConfigureRoutes()
        {
            new HealthRoutes(_Database, Settings.NodeId).Register(_Server);
            new AuthRoutes(_AuthenticationService).Register(_Server);
            new TenantRoutes(_Database, _AuthorizationService).Register(_Server);
            new AdminRoutes(_Database, _AuthorizationService).Register(_Server);
            new UserRoutes(_Database, _AuthorizationService).Register(_Server);
            new CredentialRoutes(_Database, _AuthorizationService).Register(_Server);
            new LockRoutes(_Database, _AuthorizationService, _Engine, _Telemetry, Settings.NodeId).Register(_Server);
            new LockAuditRoutes(_Database, _AuthorizationService).Register(_Server);
            new RequestHistoryRoutes(_Database, _AuthorizationService).Register(_Server);
            new ServerInfoRoutes(Settings, _WsManager).Register(_Server);
            new SettingsRoutes(Settings, _AuthorizationService, _Logging).Register(_Server);

            _Server.WebSocket("/v1.0/lock/connect", _WsHandler.HandleAsync);
        }

        private static async Task DefaultRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "application/json";
            await context.Response.Send("{\"error\":\"NotFound\"}").ConfigureAwait(false);
        }

        private static async Task PreflightRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, HEAD");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Key, x-token, x-access-key, x-clutch-access-key");
            context.Response.Headers.Add("Access-Control-Max-Age", "86400");
            await context.Response.Send().ConfigureAwait(false);
        }

        private async Task PostRoutingRouteAsync(HttpContextBase context)
        {
            context.Timestamp.End = DateTime.UtcNow;

            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, HEAD");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Key, x-token, x-access-key, x-clutch-access-key");

            _Logging.Debug(
                _Header +
                context.Request.Method + " " +
                context.Request.Url.RawWithQuery + " " +
                context.Response.StatusCode);

            if (Settings.RequestHistory.Enabled) _Capture.Capture(context);

            double durationSeconds = (context.Timestamp.TotalMs ?? 0) / 1000.0;
            _Telemetry.RecordHttp(context.Request.Method.ToString(), context.Response.StatusCode, durationSeconds);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                _Mcp.Dispose();
                if (_Server is IDisposable disposableServer) disposableServer.Dispose();
            }
            _Disposed = true;
        }

        #endregion
    }
}

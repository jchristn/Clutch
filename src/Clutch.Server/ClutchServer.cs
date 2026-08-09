namespace Clutch.Server
{
    using System;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Server.Services;
    using Clutch.Server.Settings;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Clutch server host. Owns the Watson webserver and wires the request pipeline and routes.
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
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public ClutchServer(
            ClutchSettings settings,
            LoggingModule logging,
            DatabaseDriverBase database,
            AuthenticationService authenticationService,
            AuthorizationService authorizationService)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (authenticationService == null) throw new ArgumentNullException(nameof(authenticationService));
            if (authorizationService == null) throw new ArgumentNullException(nameof(authorizationService));

            Settings = settings;
            _Logging = logging;
            _Database = database;
            _AuthenticationService = authenticationService;
            _AuthorizationService = authorizationService;

            WebserverSettings webserverSettings = new WebserverSettings();
            webserverSettings.Hostname = Settings.Rest.Hostname;
            webserverSettings.Port = Settings.Rest.Port;
            webserverSettings.Ssl.Enable = Settings.Rest.Ssl;
            webserverSettings.WebSockets.Enable = true;

            _Server = new Webserver(webserverSettings, DefaultRouteAsync);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Configure the pipeline and routes, then start listening.
        /// </summary>
        public void Start()
        {
            ConfigureServer();
            ConfigureRoutes();
            _Server.Start();
        }

        /// <summary>
        /// Stop listening.
        /// </summary>
        public void Stop()
        {
            _Server.Stop();
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
            _Server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/v1.0/api/health",
                HealthRouteAsync,
                null,
                openApiMetadata: OpenApiRouteMetadata.Create("Server health check", "System"));
        }

        private async Task HealthRouteAsync(HttpContextBase context)
        {
            bool databaseHealthy;
            try
            {
                databaseHealthy = await _Database.PingAsync(context.Token).ConfigureAwait(false);
            }
            catch
            {
                databaseHealthy = false;
            }

            context.Response.StatusCode = databaseHealthy ? 200 : 503;
            context.Response.ContentType = "application/json";
            string body =
                "{\"status\":\"" + (databaseHealthy ? "healthy" : "degraded") + "\"," +
                "\"node\":\"" + Settings.NodeId + "\"," +
                "\"database\":" + (databaseHealthy ? "true" : "false") + "," +
                "\"utc\":\"" + DateTime.UtcNow.ToString("o") + "\"}";
            await context.Response.Send(body).ConfigureAwait(false);
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
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Key");
            context.Response.Headers.Add("Access-Control-Max-Age", "86400");
            await context.Response.Send().ConfigureAwait(false);
        }

        private async Task PostRoutingRouteAsync(HttpContextBase context)
        {
            context.Timestamp.End = DateTime.UtcNow;

            _Logging.Debug(
                _Header +
                context.Request.Method + " " +
                context.Request.Url.RawWithQuery + " " +
                context.Response.StatusCode);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                if (_Server is IDisposable disposableServer) disposableServer.Dispose();
            }
            _Disposed = true;
        }

        #endregion
    }
}

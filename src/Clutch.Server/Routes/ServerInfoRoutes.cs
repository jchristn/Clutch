namespace Clutch.Server.Routes
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Clutch.Core.Security;
    using Clutch.Server.Settings;
    using Clutch.Server.WebSocket;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Server info route. Reports node identity, version, endpoint, and live connection counts.
    /// </summary>
    public class ServerInfoRoutes
    {
        #region Private-Members

        private readonly ClutchSettings _Settings;
        private readonly WebSocketConnectionManager _WsManager;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="wsManager">WebSocket connection manager.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public ServerInfoRoutes(ClutchSettings settings, WebSocketConnectionManager wsManager)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _WsManager = wsManager ?? throw new ArgumentNullException(nameof(wsManager));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register routes.
        /// </summary>
        /// <param name="server">Webserver.</param>
        public void Register(Webserver server)
        {
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/server-info", InfoAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Server info", "System"));
        }

        #endregion

        #region Private-Methods

        private async Task InfoAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";

            await RouteHelpers.JsonAsync(context, 200, new
            {
                product = "Clutch",
                version = version,
                node = _Settings.NodeId,
                database = _Settings.Database.Type.ToString(),
                webSocketConnections = _WsManager.Count,
                telemetry = new
                {
                    enabled = _Settings.Telemetry.Enabled,
                    prometheusPort = _Settings.Telemetry.PrometheusPort,
                    prometheusPath = _Settings.Telemetry.PrometheusPath
                },
                principal = new
                {
                    authenticated = ctx.IsAuthenticated,
                    tenantId = ctx.TenantId,
                    isAdmin = ctx.IsAdmin,
                    isTenantAdmin = ctx.IsTenantAdmin,
                    principalName = ctx.PrincipalName
                }
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

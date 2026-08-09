namespace Clutch.Server.Routes
{
    using System;
    using System.Threading.Tasks;
    using Clutch.Core.Security;
    using Clutch.Server.Serialization;
    using Clutch.Server.Services;
    using Clutch.Server.Settings;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Server settings routes. A system administrator can read the running settings, update them (which
    /// also rewrites the on-disk JSON file), and trigger a restart so changes that require one take effect.
    /// </summary>
    public class SettingsRoutes
    {
        #region Private-Members

        private const string _Redacted = "***";
        private readonly ClutchSettings _Settings;
        private readonly AuthorizationService _Authorization;
        private readonly LoggingModule _Logging;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Live settings instance.</param>
        /// <param name="authorization">Authorization service.</param>
        /// <param name="logging">Logging module.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public SettingsRoutes(ClutchSettings settings, AuthorizationService authorization, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register routes.
        /// </summary>
        /// <param name="server">Webserver.</param>
        public void Register(Webserver server)
        {
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/v1.0/api/settings", GetAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Read server settings", "Settings"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, "/v1.0/api/settings", UpdateAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Update server settings", "Settings"));
            server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/v1.0/api/settings/restart", RestartAsync, null, openApiMetadata: OpenApiRouteMetadata.Create("Restart the node", "Settings"));
        }

        #endregion

        #region Private-Methods

        private async Task GetAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            if (!ctx.IsAdmin)
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Only a system administrator may read server settings.").ConfigureAwait(false);
                return;
            }
            await RouteHelpers.JsonAsync(context, 200, Redacted()).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            if (!ctx.IsAdmin)
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Only a system administrator may update server settings.").ConfigureAwait(false);
                return;
            }

            ClutchSettings? incoming = RouteHelpers.Body<ClutchSettings>(context);
            if (incoming == null)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "A settings body is required.").ConfigureAwait(false);
                return;
            }

            try
            {
                // Preserve secrets when the client echoes the redacted placeholder or leaves them blank.
                if (IsBlankOrRedacted(incoming.Auth.SigningKey)) incoming.Auth.SigningKey = _Settings.Auth.SigningKey;
                if (IsBlankOrRedacted(incoming.Auth.AdminApiKey)) incoming.Auth.AdminApiKey = _Settings.Auth.AdminApiKey;
                if (IsBlankOrRedacted(incoming.Database.Password)) incoming.Database.Password = _Settings.Database.Password;

                // Apply to the live instance so a subsequent read reflects the change.
                if (!string.IsNullOrWhiteSpace(incoming.NodeId)) _Settings.NodeId = incoming.NodeId;
                _Settings.Rest = incoming.Rest;
                _Settings.Database = incoming.Database;
                _Settings.Logging = incoming.Logging;
                _Settings.Auth = incoming.Auth;
                _Settings.Lock = incoming.Lock;
                _Settings.RequestHistory = incoming.RequestHistory;
                _Settings.Telemetry = incoming.Telemetry;

                _Settings.Save();
                _Logging.Info("[Clutch] settings updated and written to " + _Settings.SourceFile);
            }
            catch (Exception e)
            {
                await RouteHelpers.ErrorAsync(context, 400, "BadRequest", "Invalid settings: " + e.Message).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.Send(Json.Serialize(new
            {
                saved = true,
                restartRequired = true,
                message = "Settings saved to disk. Most changes take effect after a restart.",
                settings = Redacted()
            })).ConfigureAwait(false);
        }

        private async Task RestartAsync(HttpContextBase context)
        {
            RequestContext ctx = RouteHelpers.Context(context);
            if (!ctx.IsAdmin)
            {
                await RouteHelpers.ErrorAsync(context, 403, "Forbidden", "Only a system administrator may restart the node.").ConfigureAwait(false);
                return;
            }

            _Logging.Warn("[Clutch] restart requested by administrator; exiting so the process manager relaunches with updated settings.");
            context.Response.StatusCode = 202;
            context.Response.ContentType = "application/json";
            await context.Response.Send(Json.Serialize(new { restarting = true, node = _Settings.NodeId })).ConfigureAwait(false);

            // Exit shortly after the response is flushed. Under Docker (restart: unless-stopped) the
            // container relaunches and reloads the settings file written above.
            _ = Task.Run(async () =>
            {
                await Task.Delay(750).ConfigureAwait(false);
                Environment.Exit(0);
            });
        }

        private ClutchSettings Redacted()
        {
            ClutchSettings clone = Json.Deserialize<ClutchSettings>(Json.Serialize(_Settings)) ?? new ClutchSettings();
            clone.Auth.SigningKey = _Redacted;
            clone.Auth.AdminApiKey = _Redacted;
            clone.Database.Password = _Redacted;
            return clone;
        }

        private static bool IsBlankOrRedacted(string? value)
        {
            return string.IsNullOrWhiteSpace(value) || value == _Redacted;
        }

        #endregion
    }
}

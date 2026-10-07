namespace Test.Aot
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enums;
    using Clutch.Core.Security;
    using Clutch.Core.Services;
    using Clutch.Server;
    using Clutch.Server.Services;
    using Clutch.Server.Settings;
    using Clutch.Server.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Runs a complete Clutch node in-process (REST, WebSocket, OpenAPI, and MCP) against a temporary SQLite
    /// database, composed the same way the server's bootstrapper composes it.
    /// </summary>
    internal sealed class AotServerHost : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// REST base URL, for example http://localhost:51234.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// MCP streamable-HTTP URL.
        /// </summary>
        public string McpUrl { get; }

        /// <summary>
        /// The settings file the node was loaded from.
        /// </summary>
        public string SettingsFile { get; }

        /// <summary>
        /// The node's settings.
        /// </summary>
        public ClutchSettings Settings { get; }

        #endregion

        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ClutchTelemetry _Telemetry;
        private readonly ClutchServer _Server;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        private AotServerHost(ClutchSettings settings, string settingsFile, DatabaseDriverBase database, ClutchTelemetry telemetry, ClutchServer server)
        {
            Settings = settings;
            SettingsFile = settingsFile;
            _Database = database;
            _Telemetry = telemetry;
            _Server = server;
            Endpoint = "http://localhost:" + settings.Rest.Port;
            McpUrl = "http://" + settings.Mcp.Hostname + ":" + settings.Mcp.Port + settings.Mcp.McpPath;
        }

        /// <summary>
        /// Write a settings file for a throwaway node, start the node, and wait until it answers health checks.
        /// </summary>
        /// <param name="workDirectory">Directory for the settings file and SQLite database.</param>
        /// <returns>The running host.</returns>
        public static async Task<AotServerHost> StartAsync(string workDirectory)
        {
            string settingsFile = Path.Combine(workDirectory, "clutch.json");

            // FromFile writes a default settings file when none exists; Save writes the edits back. Both go through
            // the source-generated settings metadata.
            ClutchSettings settings = ClutchSettings.FromFile(settingsFile);
            settings.NodeId = "aot-node";
            settings.Rest.Hostname = "localhost";
            settings.Rest.Port = FreeTcpPort();
            settings.Database.Type = DatabaseTypeEnum.Sqlite;
            settings.Database.FilePath = Path.Combine(workDirectory, "clutch.db");
            settings.Mcp.Enable = true;
            settings.Mcp.Hostname = "127.0.0.1";
            settings.Mcp.Port = FreeTcpPort();
            settings.Telemetry.Enabled = false;
            settings.Logging.ConsoleLogging = false;
            settings.Logging.FileLogging = false;
            settings.Save();

            settings = ClutchSettings.FromFile(settingsFile);

            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            DatabaseDriverBase database = DatabaseDriverFactory.Create(settings.Database);
            await database.InitializeAsync().ConfigureAwait(false);
            await DefaultSeeder.SeedAsync(database, null).ConfigureAwait(false);

            TokenService tokenService = new TokenService(settings.Auth.SigningKey, settings.Auth.Issuer, settings.Auth.TokenLifetimeMinutes);
            AuthenticationService authentication = new AuthenticationService(database, tokenService, settings.Auth);
            AuthorizationService authorization = new AuthorizationService();
            ClutchTelemetry telemetry = new ClutchTelemetry(settings.Telemetry, settings.NodeId, null);

            ClutchServer server = new ClutchServer(settings, logging, database, authentication, authorization, telemetry);
            server.Start();

            AotServerHost host = new AotServerHost(settings, settingsFile, database, telemetry, server);
            await host.WaitForHealthAsync().ConfigureAwait(false);
            return host;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Stop the node and release its database.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            try
            {
                _Server.Stop();
                _Server.Dispose();
            }
            catch
            {
                // best effort
            }
            _Telemetry.Dispose();
            _Database.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task WaitForHealthAsync()
        {
            using HttpClient http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(5);
            Exception? last = null;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                try
                {
                    using HttpResponseMessage response = await http.GetAsync(Endpoint + "/v1.0/api/health").ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) return;
                }
                catch (Exception e)
                {
                    last = e;
                }
                await Task.Delay(100).ConfigureAwait(false);
            }
            throw new InvalidOperationException("The Clutch node did not become healthy at " + Endpoint + ": " + last?.Message);
        }

        private static int FreeTcpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        #endregion
    }
}

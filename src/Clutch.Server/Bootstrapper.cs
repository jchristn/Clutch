namespace Clutch.Server
{
    using System;
    using System.Threading;
    using Clutch.Core.Database;
    using Clutch.Core.Enums;
    using Clutch.Core.Security;
    using Clutch.Core.Services;
    using Clutch.Server.Services;
    using Clutch.Server.Settings;
    using Clutch.Server.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// Composition root. Loads settings, applies environment overrides, persists settings back to disk,
    /// constructs logging and the server host, and blocks until shutdown.
    /// </summary>
    public static class Bootstrapper
    {
        #region Public-Methods

        /// <summary>
        /// Run the server until interrupted.
        /// </summary>
        /// <param name="args">Command-line arguments. The first argument, if present, is the settings file path.</param>
        public static void Run(string[] args)
        {
            string settingsFile = ResolveSettingsFile(args);
            ClutchSettings settings = ClutchSettings.FromFile(settingsFile);

            // Persist settings back so any properties added since the last build are written with defaults.
            // This happens before environment overrides so runtime/container values are not written to disk.
            settings.ToFile(settingsFile);

            ApplyEnvironmentOverrides(settings);

            LoggingModule logging = BuildLogging(settings);
            logging.Info("[Clutch] starting node '" + settings.NodeId + "' using settings file '" + settingsFile + "'");

            DatabaseDriverBase database = DatabaseDriverFactory.Create(settings.Database);
            try
            {
                database.InitializeAsync().GetAwaiter().GetResult();
                if (settings.Database.Type == DatabaseTypeEnum.Sqlite)
                {
                    logging.Info("[Clutch] database initialized (SQLite at " + settings.Database.FilePath + ")");
                    logging.Warn("[Clutch] SQLite is single-node only. Do not run more than one Clutch node against a shared SQLite database; use PostgreSQL, MySQL, or SQL Server for multi-node clustering.");
                }
                else
                {
                    logging.Info("[Clutch] database initialized (" + settings.Database.Type + " at " + settings.Database.Host + ":" + settings.Database.Port + "/" + settings.Database.DatabaseName + ")");
                }
                DefaultSeeder.SeedAsync(database, message => logging.Info("[Clutch] " + message)).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                logging.Alert("[Clutch] database initialization failed: " + e.Message);
                database.Dispose();
                return;
            }

            TokenService tokenService = new TokenService(settings.Auth.SigningKey, settings.Auth.Issuer, settings.Auth.TokenLifetimeMinutes);
            AuthenticationService authenticationService = new AuthenticationService(database, tokenService, settings.Auth);
            AuthorizationService authorizationService = new AuthorizationService();
            ClutchTelemetry telemetry = new ClutchTelemetry(settings.Telemetry, settings.NodeId, message => logging.Debug("[Clutch.Telemetry] " + message));
            if (settings.Telemetry.Enabled && settings.Telemetry.PrometheusEnable)
            {
                logging.Info("[Clutch] telemetry metrics on :" + settings.Telemetry.PrometheusPort + settings.Telemetry.PrometheusPath);
            }

            ClutchServer server = new ClutchServer(settings, logging, database, authenticationService, authorizationService, telemetry);
            server.Start();
            logging.Info("[Clutch] node '" + settings.NodeId + "' listening on " + settings.Rest.Hostname + ":" + settings.Rest.Port);

            ManualResetEventSlim shutdown = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                shutdown.Set();
            };
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => shutdown.Set();

            shutdown.Wait();

            logging.Info("[Clutch] node '" + settings.NodeId + "' stopping");
            server.Stop();
            server.Dispose();
            telemetry.Dispose();
            database.Dispose();
        }

        #endregion

        #region Private-Methods

        private static string ResolveSettingsFile(string[] args)
        {
            string? fromEnv = Environment.GetEnvironmentVariable("CLUTCH_SETTINGS_FILE");
            if (!String.IsNullOrEmpty(fromEnv)) return fromEnv;
            if (args != null && args.Length > 0 && !String.IsNullOrEmpty(args[0])) return args[0];
            return "clutch.json";
        }

        private static void ApplyEnvironmentOverrides(ClutchSettings settings)
        {
            string? nodeId = Environment.GetEnvironmentVariable("CLUTCH_NODE_ID");
            if (!String.IsNullOrEmpty(nodeId)) settings.NodeId = nodeId;

            string? dbType = Environment.GetEnvironmentVariable("CLUTCH_DB_TYPE");
            if (!String.IsNullOrEmpty(dbType) && Enum.TryParse<DatabaseTypeEnum>(dbType, true, out DatabaseTypeEnum parsedType)) settings.Database.Type = parsedType;

            string? dbFilePath = Environment.GetEnvironmentVariable("CLUTCH_DB_FILEPATH");
            if (!String.IsNullOrEmpty(dbFilePath)) settings.Database.FilePath = dbFilePath;

            string? dbSchema = Environment.GetEnvironmentVariable("CLUTCH_DB_SCHEMA");
            if (!String.IsNullOrEmpty(dbSchema)) settings.Database.Schema = dbSchema;

            string? dbManageSchema = Environment.GetEnvironmentVariable("CLUTCH_DB_MANAGE_SCHEMA");
            if (!String.IsNullOrEmpty(dbManageSchema) && Boolean.TryParse(dbManageSchema, out bool manageSchema)) settings.Database.ManageSchema = manageSchema;

            string? dbHost = Environment.GetEnvironmentVariable("CLUTCH_DB_HOST");
            if (!String.IsNullOrEmpty(dbHost)) settings.Database.Host = dbHost;

            string? dbPort = Environment.GetEnvironmentVariable("CLUTCH_DB_PORT");
            if (!String.IsNullOrEmpty(dbPort) && Int32.TryParse(dbPort, out int port)) settings.Database.Port = port;

            string? dbName = Environment.GetEnvironmentVariable("CLUTCH_DB_DATABASE");
            if (!String.IsNullOrEmpty(dbName)) settings.Database.DatabaseName = dbName;

            string? dbUser = Environment.GetEnvironmentVariable("CLUTCH_DB_USERNAME");
            if (!String.IsNullOrEmpty(dbUser)) settings.Database.Username = dbUser;

            string? dbPass = Environment.GetEnvironmentVariable("CLUTCH_DB_PASSWORD");
            if (!String.IsNullOrEmpty(dbPass)) settings.Database.Password = dbPass;

            string? signingKey = Environment.GetEnvironmentVariable("CLUTCH_AUTH_SIGNING_KEY");
            if (!String.IsNullOrEmpty(signingKey)) settings.Auth.SigningKey = signingKey;

            string? restPort = Environment.GetEnvironmentVariable("CLUTCH_REST_PORT");
            if (!String.IsNullOrEmpty(restPort) && Int32.TryParse(restPort, out int rp)) settings.Rest.Port = rp;

            string? mcpEnable = Environment.GetEnvironmentVariable("CLUTCH_MCP_ENABLE");
            if (!String.IsNullOrEmpty(mcpEnable) && Boolean.TryParse(mcpEnable, out bool me)) settings.Mcp.Enable = me;

            string? mcpPort = Environment.GetEnvironmentVariable("CLUTCH_MCP_PORT");
            if (!String.IsNullOrEmpty(mcpPort) && Int32.TryParse(mcpPort, out int mp)) settings.Mcp.Port = mp;
        }

        private static LoggingModule BuildLogging(ClutchSettings settings)
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = settings.Logging.ConsoleLogging;
            logging.Settings.MinimumSeverity = (Severity)settings.Logging.MinimumSeverity;

            if (settings.Logging.FileLogging)
            {
                logging.Settings.FileLogging = FileLoggingMode.FileWithDate;
                logging.Settings.LogFilename = settings.Logging.LogDirectory + settings.Logging.LogFilename;
            }

            return logging;
        }

        #endregion
    }
}

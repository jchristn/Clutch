namespace Clutch.Server.Settings
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Clutch.Core.Database;

    /// <summary>
    /// Top-level server settings. Loaded from a JSON file at startup and re-serialized back to disk so
    /// that properties added since the last build are persisted with their defaults.
    /// </summary>
    public class ClutchSettings
    {
        #region Public-Members

        /// <summary>
        /// Settings creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Node identifier. Distinguishes nodes sharing one database. Overridable via CLUTCH_NODE_ID.
        /// </summary>
        public string NodeId
        {
            get
            {
                return _NodeId;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(NodeId));
                _NodeId = value;
            }
        }

        /// <summary>
        /// REST and WebSocket transport settings.
        /// </summary>
        public RestSettings Rest
        {
            get
            {
                return _Rest;
            }
            set
            {
                _Rest = value ?? new RestSettings();
            }
        }

        /// <summary>
        /// Database connection settings.
        /// </summary>
        public DatabaseSettings Database
        {
            get
            {
                return _Database;
            }
            set
            {
                _Database = value ?? new DatabaseSettings();
            }
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get
            {
                return _Logging;
            }
            set
            {
                _Logging = value ?? new LoggingSettings();
            }
        }

        /// <summary>
        /// Authentication settings.
        /// </summary>
        public AuthSettings Auth
        {
            get
            {
                return _Auth;
            }
            set
            {
                _Auth = value ?? new AuthSettings();
            }
        }

        /// <summary>
        /// Lock engine settings.
        /// </summary>
        public LockSettings Lock
        {
            get
            {
                return _Lock;
            }
            set
            {
                _Lock = value ?? new LockSettings();
            }
        }

        /// <summary>
        /// Request history settings.
        /// </summary>
        public RequestHistorySettings RequestHistory
        {
            get
            {
                return _RequestHistory;
            }
            set
            {
                _RequestHistory = value ?? new RequestHistorySettings();
            }
        }

        /// <summary>
        /// Telemetry settings.
        /// </summary>
        public TelemetrySettings Telemetry
        {
            get
            {
                return _Telemetry;
            }
            set
            {
                _Telemetry = value ?? new TelemetrySettings();
            }
        }

        #endregion

        #region Private-Members

        private string _NodeId = "node1";
        private RestSettings _Rest = new RestSettings();
        private DatabaseSettings _Database = new DatabaseSettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private AuthSettings _Auth = new AuthSettings();
        private LockSettings _Lock = new LockSettings();
        private RequestHistorySettings _RequestHistory = new RequestHistorySettings();
        private TelemetrySettings _Telemetry = new TelemetrySettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with defaults.
        /// </summary>
        public ClutchSettings()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load settings from a file. If the file does not exist, a default settings file is written.
        /// </summary>
        /// <param name="filename">Settings file path.</param>
        /// <returns>Loaded settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when filename is null or empty.</exception>
        public static ClutchSettings FromFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            if (!File.Exists(filename))
            {
                ClutchSettings defaults = new ClutchSettings();
                defaults.ToFile(filename);
                return defaults;
            }

            string json = File.ReadAllText(filename);
            ClutchSettings? settings = JsonSerializer.Deserialize<ClutchSettings>(json, GetJsonSerializerOptions());
            return settings ?? new ClutchSettings();
        }

        /// <summary>
        /// Serialize settings to a file. Called on startup to persist newly added properties with defaults.
        /// </summary>
        /// <param name="filename">Settings file path.</param>
        /// <exception cref="ArgumentNullException">Thrown when filename is null or empty.</exception>
        public void ToFile(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            string json = JsonSerializer.Serialize(this, GetJsonSerializerOptions());
            File.WriteAllText(filename, json);
        }

        #endregion

        #region Private-Methods

        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        #endregion
    }
}

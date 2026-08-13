namespace Clutch.Core.Database
{
    using System;
    using Clutch.Core.Enums;

    /// <summary>
    /// Database connection and provider settings. Clutch supports PostgreSQL, MySQL, SQL Server, and
    /// SQLite behind one provider-neutral abstraction. Table names and schema are configurable while the
    /// column layout is owned by Clutch.
    /// </summary>
    public class DatabaseSettings
    {
        #region Public-Members

        /// <summary>
        /// Database provider type. Defaults to Postgresql.
        /// </summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Postgresql;

        /// <summary>
        /// Database server hostname or IP address. Ignored for SQLite (which uses <see cref="FilePath"/>).
        /// </summary>
        public string Host
        {
            get
            {
                return _Host;
            }
            set
            {
                _Host = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Database server port. Minimum 1, maximum 65535. Defaults to 5432 (PostgreSQL). MySQL commonly
        /// uses 3306 and SQL Server 1433.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = Math.Clamp(value, 1, 65535);
            }
        }

        /// <summary>
        /// Database name. Ignored for SQLite.
        /// </summary>
        public string DatabaseName
        {
            get
            {
                return _DatabaseName;
            }
            set
            {
                _DatabaseName = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Database username. Ignored for SQLite.
        /// </summary>
        public string Username
        {
            get
            {
                return _Username;
            }
            set
            {
                _Username = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Database password. May be overridden via environment variable in the server bootstrapper.
        /// Ignored for SQLite.
        /// </summary>
        public string Password { get; set; } = "postgres";

        /// <summary>
        /// SQLite database file path. Used only when <see cref="Type"/> is Sqlite. Defaults to clutch.db.
        /// </summary>
        public string FilePath
        {
            get
            {
                return _FilePath;
            }
            set
            {
                _FilePath = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Optional schema or namespace that qualifies every Clutch table. PostgreSQL and SQL Server honor
        /// this; MySQL and SQLite ignore it. Null or empty means the provider default schema.
        /// </summary>
        public string? Schema { get; set; } = null;

        /// <summary>
        /// Whether Clutch is allowed to create and migrate its tables. Defaults to true. When false, Clutch
        /// issues no DDL and instead verifies that every configured table already exists, failing startup if
        /// one is missing.
        /// </summary>
        public bool ManageSchema { get; set; } = true;

        /// <summary>
        /// Per-purpose table naming. Each purpose defaults to a clutch_-prefixed name; a non-empty override
        /// replaces the default for that purpose only.
        /// </summary>
        public TableNamingSettings Tables
        {
            get
            {
                return _Tables;
            }
            set
            {
                _Tables = value ?? new TableNamingSettings();
            }
        }

        /// <summary>
        /// Optional provider-specific connection string options appended verbatim to the built connection
        /// string. Use to set knobs Clutch does not model directly. Null or empty means none.
        /// </summary>
        public string? AdditionalOptions { get; set; } = null;

        /// <summary>
        /// Maximum connection pool size. Minimum 1, maximum 1024. Defaults to 100. Ignored for SQLite.
        /// </summary>
        public int MaxPoolSize
        {
            get
            {
                return _MaxPoolSize;
            }
            set
            {
                _MaxPoolSize = Math.Clamp(value, 1, 1024);
            }
        }

        #endregion

        #region Private-Members

        private string _Host = "localhost";
        private int _Port = 5432;
        private string _DatabaseName = "clutch";
        private string _Username = "postgres";
        private string _FilePath = "clutch.db";
        private int _MaxPoolSize = 100;
        private TableNamingSettings _Tables = new TableNamingSettings();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate that the settings are internally coherent for the selected provider.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a required field for the selected provider is missing.</exception>
        public void Validate()
        {
            if (Type == DatabaseTypeEnum.Sqlite)
            {
                if (String.IsNullOrEmpty(_FilePath)) throw new ArgumentException("SQLite requires a database FilePath.", nameof(FilePath));
                return;
            }

            if (String.IsNullOrEmpty(_Host)) throw new ArgumentException("A database Host is required for provider '" + Type + "'.", nameof(Host));
            if (String.IsNullOrEmpty(_DatabaseName)) throw new ArgumentException("A DatabaseName is required for provider '" + Type + "'.", nameof(DatabaseName));
            if (String.IsNullOrEmpty(_Username)) throw new ArgumentException("A Username is required for provider '" + Type + "'.", nameof(Username));
        }

        /// <summary>
        /// Build a Npgsql-compatible connection string from these settings.
        /// </summary>
        /// <returns>Connection string.</returns>
        public string ToPostgresConnectionString()
        {
            return
                "Host=" + _Host + ";" +
                "Port=" + _Port + ";" +
                "Database=" + _DatabaseName + ";" +
                "Username=" + _Username + ";" +
                "Password=" + Password + ";" +
                "Maximum Pool Size=" + _MaxPoolSize + ";" +
                AppendOptions();
        }

        /// <summary>
        /// Build a Microsoft.Data.Sqlite-compatible connection string from these settings.
        /// </summary>
        /// <returns>Connection string.</returns>
        public string ToSqliteConnectionString()
        {
            return
                "Data Source=" + _FilePath + ";" +
                "Foreign Keys=False;" +
                AppendOptions();
        }

        /// <summary>
        /// Build a MySqlConnector-compatible connection string from these settings.
        /// </summary>
        /// <returns>Connection string.</returns>
        public string ToMysqlConnectionString()
        {
            return
                "Server=" + _Host + ";" +
                "Port=" + _Port + ";" +
                "Database=" + _DatabaseName + ";" +
                "User ID=" + _Username + ";" +
                "Password=" + Password + ";" +
                "Maximum Pool Size=" + _MaxPoolSize + ";" +
                "DateTimeKind=Utc;" +
                AppendOptions();
        }

        /// <summary>
        /// Build a Microsoft.Data.SqlClient-compatible connection string from these settings.
        /// </summary>
        /// <returns>Connection string.</returns>
        public string ToSqlServerConnectionString()
        {
            return
                "Server=" + _Host + "," + _Port + ";" +
                "Database=" + _DatabaseName + ";" +
                "User ID=" + _Username + ";" +
                "Password=" + Password + ";" +
                "Max Pool Size=" + _MaxPoolSize + ";" +
                "Encrypt=False;" +
                "TrustServerCertificate=True;" +
                AppendOptions();
        }

        #endregion

        #region Private-Methods

        private string AppendOptions()
        {
            if (String.IsNullOrEmpty(AdditionalOptions)) return string.Empty;
            string trimmed = AdditionalOptions.Trim();
            if (trimmed.EndsWith(";", StringComparison.Ordinal)) return trimmed;
            return trimmed + ";";
        }

        #endregion
    }
}

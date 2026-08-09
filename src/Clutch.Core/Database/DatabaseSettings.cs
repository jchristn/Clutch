namespace Clutch.Core.Database
{
    using System;
    using Clutch.Core.Enums;

    /// <summary>
    /// Database connection settings. Clutch targets PostgreSQL; the provider abstraction remains in place
    /// so additional providers can be added without changing callers.
    /// </summary>
    public class DatabaseSettings
    {
        #region Public-Members

        /// <summary>
        /// Database provider type. Defaults to Postgresql.
        /// </summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Postgresql;

        /// <summary>
        /// Database server hostname or IP address.
        /// </summary>
        public string Host
        {
            get
            {
                return _Host;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Host));
                _Host = value;
            }
        }

        /// <summary>
        /// Database server port. Minimum 1, maximum 65535. Defaults to 5432 (PostgreSQL).
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
        /// Database name.
        /// </summary>
        public string DatabaseName
        {
            get
            {
                return _DatabaseName;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(DatabaseName));
                _DatabaseName = value;
            }
        }

        /// <summary>
        /// Database username.
        /// </summary>
        public string Username
        {
            get
            {
                return _Username;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Username));
                _Username = value;
            }
        }

        /// <summary>
        /// Database password. May be overridden via environment variable in the server bootstrapper.
        /// </summary>
        public string Password { get; set; } = "postgres";

        /// <summary>
        /// Maximum connection pool size. Minimum 1, maximum 1024. Defaults to 100.
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
        private int _MaxPoolSize = 100;

        #endregion

        #region Public-Methods

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
                "Maximum Pool Size=" + _MaxPoolSize + ";";
        }

        #endregion
    }
}

namespace Clutch.Server.Settings
{
    using System;

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether to log to the console. Defaults to true.
        /// </summary>
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>
        /// Whether to log to a file. Defaults to false.
        /// </summary>
        public bool FileLogging { get; set; } = false;

        /// <summary>
        /// Directory for log files when file logging is enabled.
        /// </summary>
        public string LogDirectory
        {
            get
            {
                return _LogDirectory;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) _LogDirectory = "./logs/";
                else _LogDirectory = value;
            }
        }

        /// <summary>
        /// Log file name prefix.
        /// </summary>
        public string LogFilename
        {
            get
            {
                return _LogFilename;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) _LogFilename = "clutch.log";
                else _LogFilename = value;
            }
        }

        /// <summary>
        /// Minimum severity to emit, mapped to SyslogLogging severity. 0 = Debug, higher = more severe.
        /// Minimum 0, maximum 7. Defaults to 1 (Info).
        /// </summary>
        public int MinimumSeverity
        {
            get
            {
                return _MinimumSeverity;
            }
            set
            {
                _MinimumSeverity = Math.Clamp(value, 0, 7);
            }
        }

        #endregion

        #region Private-Members

        private string _LogDirectory = "./logs/";
        private string _LogFilename = "clutch.log";
        private int _MinimumSeverity = 1;

        #endregion
    }
}

namespace Clutch.Server.Settings
{
    using System;

    /// <summary>
    /// REST and WebSocket transport settings. WebSockets are hosted on the same port as REST.
    /// </summary>
    public class RestSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname or IP address the server binds to. Defaults to "*" for all interfaces.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// TCP port the server listens on. Minimum 1, maximum 65535. Defaults to 8080.
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
        /// Whether TLS is enabled. Defaults to false.
        /// </summary>
        public bool Ssl { get; set; } = false;

        #endregion

        #region Private-Members

        private string _Hostname = "*";
        private int _Port = 8080;

        #endregion
    }
}

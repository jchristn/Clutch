namespace Clutch.Server.Settings
{
    using System;

    /// <summary>
    /// Authentication settings, including the session-token signing key. The signing key should be
    /// overridden via the CLUTCH_AUTH_SIGNING_KEY environment variable in production.
    /// </summary>
    public class AuthSettings
    {
        #region Public-Members

        /// <summary>
        /// Token issuer identifier embedded in session tokens.
        /// </summary>
        public string Issuer
        {
            get
            {
                return _Issuer;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Issuer));
                _Issuer = value;
            }
        }

        /// <summary>
        /// AES-256 signing/encryption key for session tokens. Must be overridden in production.
        /// </summary>
        public string SigningKey
        {
            get
            {
                return _SigningKey;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(SigningKey));
                _SigningKey = value;
            }
        }

        /// <summary>
        /// Session-token lifetime in minutes. Minimum 1, maximum 1440. Defaults to 60.
        /// </summary>
        public int TokenLifetimeMinutes
        {
            get
            {
                return _TokenLifetimeMinutes;
            }
            set
            {
                _TokenLifetimeMinutes = Math.Clamp(value, 1, 1440);
            }
        }

        /// <summary>
        /// System administrator API key accepted via the x-api-key header for platform administration.
        /// </summary>
        public string AdminApiKey
        {
            get
            {
                return _AdminApiKey;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(AdminApiKey));
                _AdminApiKey = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Issuer = "clutch";
        private string _SigningKey = "clutch-default-signing-key-override-me";
        private int _TokenLifetimeMinutes = 60;
        private string _AdminApiKey = "clutchadmin";

        #endregion
    }
}

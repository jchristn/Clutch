namespace Clutch.Core.Models
{
    using System;
    using Clutch.Core.Enums;
    using Clutch.Core.Helpers;

    /// <summary>
    /// An application key (credential). A non-interactive principal used by client applications to
    /// connect over WebSockets and by automation to call the REST API.
    /// </summary>
    public class Credential
    {
        #region Public-Members

        /// <summary>
        /// Credential identifier (prefix "crd_").
        /// </summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Owning tenant identifier.
        /// </summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// Owning user identifier, if any.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Human-readable name for this credential.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Public access key (format: "access_" + random characters).
        /// </summary>
        public string AccessKey
        {
            get
            {
                return _AccessKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(AccessKey));
                _AccessKey = value;
            }
        }

        /// <summary>
        /// How the credential presents itself when authenticating. The access key is the sole credential.
        /// </summary>
        public CredentialAuthModeEnum AuthMode { get; set; } = CredentialAuthModeEnum.DirectHeader;

        /// <summary>
        /// UTC timestamp when the credential was last used, if ever.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// UTC expiration timestamp, if any.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether the credential is protected from deletion.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// UTC creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC last update timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateCredentialId();
        private string _TenantId = String.Empty;
        private string _Name = String.Empty;
        private string _AccessKey = String.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Credential()
        {
        }

        #endregion
    }
}

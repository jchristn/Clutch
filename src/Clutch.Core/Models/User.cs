namespace Clutch.Core.Models
{
    using System;
    using Clutch.Core.Helpers;

    /// <summary>
    /// A tenant user. May authenticate to the dashboard or REST API by email and password.
    /// </summary>
    public class User
    {
        #region Public-Members

        /// <summary>
        /// User identifier (prefix "usr_").
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
        /// First name.
        /// </summary>
        public string FirstName { get; set; } = String.Empty;

        /// <summary>
        /// Last name.
        /// </summary>
        public string LastName { get; set; } = String.Empty;

        /// <summary>
        /// Email address. Unique within a tenant.
        /// </summary>
        public string Email
        {
            get
            {
                return _Email;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Email));
                _Email = value;
            }
        }

        /// <summary>
        /// SHA-256 hash of the password, hex-encoded.
        /// </summary>
        public string PasswordSha256
        {
            get
            {
                return _PasswordSha256;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(PasswordSha256));
                _PasswordSha256 = value;
            }
        }

        /// <summary>
        /// Whether the user has system-wide administrative access (manage all tenants). Bypasses all checks.
        /// </summary>
        public bool IsSystemAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user has full administrative access within their own tenant.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether the user is protected from deletion.
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

        private string _Id = IdGenerator.GenerateUserId();
        private string _TenantId = String.Empty;
        private string _Email = String.Empty;
        private string _PasswordSha256 = String.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public User()
        {
        }

        #endregion
    }
}

namespace Clutch.Core.Models
{
    using System;
    using Clutch.Core.Enums;
    using Clutch.Core.Helpers;

    /// <summary>
    /// A revocable authentication session backing an issued session token.
    /// </summary>
    public class AuthSession
    {
        #region Public-Members

        /// <summary>
        /// Session identifier (prefix "ses_").
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
        /// User identifier bound to the session, if the principal is a user.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential identifier bound to the session, if the principal is a credential.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// The type of principal bound to the session.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.User;

        /// <summary>
        /// Random token identifier / nonce embedded in the issued token.
        /// </summary>
        public string TokenId
        {
            get
            {
                return _TokenId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TokenId));
                _TokenId = value;
            }
        }

        /// <summary>
        /// Source IP captured at issuance, if available.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// User agent captured at issuance, if available.
        /// </summary>
        public string? UserAgent { get; set; } = null;

        /// <summary>
        /// UTC expiration timestamp.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddHours(1);

        /// <summary>
        /// UTC timestamp the session was last used, if ever.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// UTC timestamp the session was revoked, if ever.
        /// </summary>
        public DateTime? RevokedUtc { get; set; } = null;

        /// <summary>
        /// Human-readable revocation reason, if revoked.
        /// </summary>
        public string? RevocationReason { get; set; } = null;

        /// <summary>
        /// Whether the session is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether the session is protected from deletion.
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

        private string _Id = IdGenerator.GenerateAuthSessionId();
        private string _TenantId = String.Empty;
        private string _TokenId = Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthSession()
        {
        }

        #endregion
    }
}

namespace Clutch.Core.Security
{
    using System;
    using Clutch.Core.Enums;

    /// <summary>
    /// The decrypted contents of a session token. Carries only platform-controlled internal identifiers.
    /// </summary>
    public class TokenPayload
    {
        #region Public-Members

        /// <summary>
        /// The backing session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// The random token identifier / nonce, matching the session's token id.
        /// </summary>
        public string TokenId { get; set; } = string.Empty;

        /// <summary>
        /// The principal type.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.User;

        /// <summary>
        /// The tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// The user identifier, when the principal is a user.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// The credential identifier, when the principal is a credential.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// The issuer identifier.
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// UTC issuance timestamp.
        /// </summary>
        public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC expiration timestamp.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddHours(1);

        #endregion
    }
}

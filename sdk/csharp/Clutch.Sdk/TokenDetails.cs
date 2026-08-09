namespace Clutch.Sdk
{
    /// <summary>
    /// The resolved principal context for the current bearer token.
    /// </summary>
    public class TokenDetails
    {
        /// <summary>
        /// Whether the token is authenticated.
        /// </summary>
        public bool Authenticated { get; set; }

        /// <summary>
        /// The type of principal, for example "Credential" or "User".
        /// </summary>
        public string? PrincipalType { get; set; }

        /// <summary>
        /// The tenant identifier the principal belongs to.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// The user identifier when the principal is a user; otherwise null.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// The credential identifier when the principal is an application key; otherwise null.
        /// </summary>
        public string? CredentialId { get; set; }

        /// <summary>
        /// Whether the principal is a system administrator.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the principal is an administrator of its own tenant.
        /// </summary>
        public bool IsTenantAdmin { get; set; }

        /// <summary>
        /// A human-readable name for the principal.
        /// </summary>
        public string? PrincipalName { get; set; }
    }
}

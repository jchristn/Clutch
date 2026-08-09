namespace Clutch.Sdk
{
    /// <summary>
    /// The result of a successful login. Contains the bearer token and resolved principal context.
    /// </summary>
    public class TokenResponse
    {
        /// <summary>
        /// The opaque bearer token to send on subsequent authenticated requests.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// The type of principal that authenticated, for example "Credential" or "User".
        /// </summary>
        public string? PrincipalType { get; set; }

        /// <summary>
        /// The tenant identifier the principal belongs to.
        /// </summary>
        public string? TenantId { get; set; }
    }
}

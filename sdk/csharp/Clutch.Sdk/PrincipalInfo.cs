namespace Clutch.Sdk
{
    /// <summary>
    /// A summary of the authenticated principal, as embedded in server info responses.
    /// </summary>
    public class PrincipalInfo
    {
        /// <summary>
        /// Whether the principal is authenticated.
        /// </summary>
        public bool Authenticated { get; set; }

        /// <summary>
        /// The tenant identifier the principal belongs to.
        /// </summary>
        public string? TenantId { get; set; }

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

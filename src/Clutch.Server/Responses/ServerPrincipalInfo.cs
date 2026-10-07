namespace Clutch.Server.Responses
{
    /// <summary>
    /// Caller details reported by server info.
    /// </summary>
    public class ServerPrincipalInfo
    {
        #region Public-Members

        /// <summary>
        /// True when the caller is authenticated.
        /// </summary>
        public bool Authenticated { get; set; } = false;

        /// <summary>
        /// Caller tenant.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// True for a system administrator.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// True for a tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Caller display name.
        /// </summary>
        public string? PrincipalName { get; set; } = null;

        #endregion
    }
}

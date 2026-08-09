namespace Clutch.Core.Requests
{
    /// <summary>
    /// A login request. Supply either tenant/email/password for a user login, or an access key for a
    /// credential login. A credential's secret key is never sent to the server.
    /// </summary>
    public class LoginRequest
    {
        #region Public-Members

        /// <summary>
        /// Tenant identifier, required for a user login.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Email address, for a user login.
        /// </summary>
        public string? Email { get; set; } = null;

        /// <summary>
        /// Password, for a user login.
        /// </summary>
        public string? Password { get; set; } = null;

        /// <summary>
        /// Access key, for a credential login.
        /// </summary>
        public string? AccessKey { get; set; } = null;

        #endregion
    }
}

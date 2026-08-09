namespace Clutch.Core.Requests
{
    /// <summary>
    /// Request to create or update a user.
    /// </summary>
    public class CreateUserRequest
    {
        #region Public-Members

        /// <summary>
        /// Email address, unique within the tenant.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Plaintext password. Hashed before storage. Optional on update.
        /// </summary>
        public string? Password { get; set; } = null;

        /// <summary>
        /// First name.
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Last name.
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Whether the user is a system administrator.
        /// </summary>
        public bool IsSystemAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user is a tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user is active. Defaults to true.
        /// </summary>
        public bool Active { get; set; } = true;

        #endregion
    }
}

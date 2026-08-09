namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// A user within a tenant. Password hashes are never returned by the server.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The user identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The tenant identifier the user belongs to.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// The user's email address.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// The user's first name.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// The user's last name.
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Whether the user is a system administrator.
        /// </summary>
        public bool IsSystemAdmin { get; set; }

        /// <summary>
        /// Whether the user is an administrator of its tenant.
        /// </summary>
        public bool IsTenantAdmin { get; set; }

        /// <summary>
        /// Whether the user is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// The UTC timestamp at which the user was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// The UTC timestamp at which the user was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; }
    }
}

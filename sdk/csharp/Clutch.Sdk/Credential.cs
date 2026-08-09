namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// An application key (credential) scoped to a tenant. Authentication uses the access key alone.
    /// </summary>
    public class Credential
    {
        /// <summary>
        /// The credential identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The tenant identifier the credential belongs to.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// The user identifier the credential is associated with, when applicable.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// The credential name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The access key. Present when the credential is listed or read.
        /// </summary>
        public string? AccessKey { get; set; }

        /// <summary>
        /// The authentication mode for the credential.
        /// </summary>
        public string? AuthMode { get; set; }

        /// <summary>
        /// The UTC timestamp at which the credential expires, when set.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; }

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// The UTC timestamp at which the credential was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// The UTC timestamp at which the credential was last used, when available.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; }
    }
}

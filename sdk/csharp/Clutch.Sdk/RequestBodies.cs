namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// Request body for authenticating with an application key.
    /// </summary>
    internal sealed class AccessKeyLoginBody
    {
        public string? AccessKey { get; set; }
    }

    /// <summary>
    /// Request body for authenticating with a tenant, email, and password.
    /// </summary>
    internal sealed class PasswordLoginBody
    {
        public string? TenantId { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    /// <summary>
    /// Request body for creating or updating a tenant. Null values are omitted from the JSON.
    /// </summary>
    internal sealed class TenantBody
    {
        public string? Name { get; set; }
        public int? LockHistoryRetentionDays { get; set; }
        public int? DefaultLeaseMs { get; set; }
        public int? MaxLeaseMs { get; set; }
        public bool? Active { get; set; }
    }

    /// <summary>
    /// Request body for creating a user.
    /// </summary>
    internal sealed class CreateUserBody
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool IsSystemAdmin { get; set; }
        public bool IsTenantAdmin { get; set; }
        public bool Active { get; set; }
    }

    /// <summary>
    /// Request body for updating a user. Null values are omitted from the JSON.
    /// </summary>
    internal sealed class UpdateUserBody
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool? IsTenantAdmin { get; set; }
        public bool? Active { get; set; }
    }

    /// <summary>
    /// Request body for creating an application key.
    /// </summary>
    internal sealed class CreateCredentialBody
    {
        public string? Name { get; set; }
        public string? UserId { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }
}

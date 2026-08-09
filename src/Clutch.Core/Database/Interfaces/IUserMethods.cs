namespace Clutch.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Models;

    /// <summary>
    /// User data access methods. All reads are tenant-scoped except the cross-tenant lookup used during
    /// authentication.
    /// </summary>
    public interface IUserMethods
    {
        /// <summary>
        /// Create a user.
        /// </summary>
        /// <param name="user">User to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created user.</returns>
        Task<User> CreateAsync(User user, CancellationToken token = default);

        /// <summary>
        /// Read a user by identifier within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The user, or null if not found.</returns>
        Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a user by email within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="email">Email address.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The user, or null if not found.</returns>
        Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default);

        /// <summary>
        /// Enumerate users within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The users in the tenant.</returns>
        Task<List<User>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Enumerate a paginated page of users within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="query">Pagination query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of users.</returns>
        Task<EnumerationResult<User>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Update a user.
        /// </summary>
        /// <param name="user">User to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated user.</returns>
        Task<User> UpdateAsync(User user, CancellationToken token = default);

        /// <summary>
        /// Delete a user and its credentials.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">User identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a user was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}

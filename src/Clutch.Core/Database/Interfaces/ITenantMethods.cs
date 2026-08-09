namespace Clutch.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Models;

    /// <summary>
    /// Tenant data access methods.
    /// </summary>
    public interface ITenantMethods
    {
        /// <summary>
        /// Create a tenant.
        /// </summary>
        /// <param name="tenant">Tenant to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created tenant.</returns>
        Task<Tenant> CreateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>
        /// Read a tenant by identifier.
        /// </summary>
        /// <param name="id">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The tenant, or null if not found.</returns>
        Task<Tenant?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read a tenant by name.
        /// </summary>
        /// <param name="name">Tenant name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The tenant, or null if not found.</returns>
        Task<Tenant?> ReadByNameAsync(string name, CancellationToken token = default);

        /// <summary>
        /// Enumerate all tenants.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>All tenants.</returns>
        Task<List<Tenant>> EnumerateAsync(CancellationToken token = default);

        /// <summary>
        /// Update a tenant.
        /// </summary>
        /// <param name="tenant">Tenant to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated tenant.</returns>
        Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken token = default);

        /// <summary>
        /// Delete a tenant and its subordinate records.
        /// </summary>
        /// <param name="id">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a tenant was deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Determine whether a tenant exists.
        /// </summary>
        /// <param name="id">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the tenant exists.</returns>
        Task<bool> ExistsAsync(string id, CancellationToken token = default);
    }
}

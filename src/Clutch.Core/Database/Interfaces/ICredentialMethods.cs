namespace Clutch.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Models;

    /// <summary>
    /// Credential (application key) data access methods.
    /// </summary>
    public interface ICredentialMethods
    {
        /// <summary>
        /// Create a credential.
        /// </summary>
        /// <param name="credential">Credential to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created credential.</returns>
        Task<Credential> CreateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Read a credential by identifier within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The credential, or null if not found.</returns>
        Task<Credential?> ReadAsync(string tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Read a credential by access key. Used during authentication; not tenant-scoped because the
        /// access key resolves the tenant.
        /// </summary>
        /// <param name="accessKey">Access key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The credential, or null if not found.</returns>
        Task<Credential?> ReadByAccessKeyAsync(string accessKey, CancellationToken token = default);

        /// <summary>
        /// Enumerate credentials within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The credentials in the tenant.</returns>
        Task<List<Credential>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Enumerate a paginated page of credentials within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="query">Pagination query.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of credentials.</returns>
        Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default);

        /// <summary>
        /// Update a credential.
        /// </summary>
        /// <param name="credential">Credential to update.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The updated credential.</returns>
        Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default);

        /// <summary>
        /// Update only the last-used timestamp for a credential.
        /// </summary>
        /// <param name="id">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        Task TouchLastUsedAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Delete a credential.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="id">Credential identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a credential was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default);
    }
}

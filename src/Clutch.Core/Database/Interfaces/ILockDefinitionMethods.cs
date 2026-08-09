namespace Clutch.Core.Database.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Models;

    /// <summary>
    /// Lock definition (policy) read access. Definitions are created transactionally by the first
    /// acquirer through the lock holder methods; these methods support inspection and administration.
    /// </summary>
    public interface ILockDefinitionMethods
    {
        /// <summary>
        /// Read a definition by tenant and key.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lockKey">Lock key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The definition, or null if the key has never been acquired.</returns>
        Task<LockDefinition?> ReadAsync(string tenantId, string lockKey, CancellationToken token = default);

        /// <summary>
        /// Enumerate definitions within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The definitions in the tenant.</returns>
        Task<List<LockDefinition>> EnumerateAsync(string tenantId, CancellationToken token = default);

        /// <summary>
        /// Delete a definition and any holders on the key.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lockKey">Lock key.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a definition was deleted.</returns>
        Task<bool> DeleteAsync(string tenantId, string lockKey, CancellationToken token = default);
    }
}

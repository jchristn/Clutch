namespace Clutch.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;

    /// <summary>
    /// Request history data access methods.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>
        /// Insert a captured request record.
        /// </summary>
        /// <param name="entry">Entry to insert.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        Task CreateAsync(RequestHistoryEntry entry, CancellationToken token = default);

        /// <summary>
        /// Read a single entry, including headers and bodies.
        /// </summary>
        /// <param name="tenantId">Tenant scope, or null for a system-admin cross-tenant read.</param>
        /// <param name="id">Entry identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The entry, or null if not found.</returns>
        Task<RequestHistoryEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Page through entries matching a filter. Bodies are omitted.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of entries.</returns>
        Task<RequestHistoryPage> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Produce time-bucketed counts and averages for chart rendering.
        /// </summary>
        /// <param name="filter">Filter, including bucket size.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The summary.</returns>
        Task<RequestHistorySummary> SummarizeAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Delete a single entry.
        /// </summary>
        /// <param name="tenantId">Tenant scope, or null for a system-admin cross-tenant delete.</param>
        /// <param name="id">Entry identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if an entry was deleted.</returns>
        Task<bool> DeleteAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Delete all entries matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of entries deleted.</returns>
        Task<int> DeleteManyAsync(RequestHistoryFilter filter, CancellationToken token = default);

        /// <summary>
        /// Prune entries older than the given cutoff, across all tenants.
        /// </summary>
        /// <param name="olderThanUtc">Cutoff timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of entries pruned.</returns>
        Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken token = default);
    }
}

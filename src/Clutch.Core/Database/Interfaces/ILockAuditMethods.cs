namespace Clutch.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;

    /// <summary>
    /// Lock audit data access. The audit trail is append-only and powers both the audit view and the
    /// lock-activity chart.
    /// </summary>
    public interface ILockAuditMethods
    {
        /// <summary>
        /// Append an audit entry.
        /// </summary>
        /// <param name="entry">Entry to append.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        Task CreateAsync(LockAuditEntry entry, CancellationToken token = default);

        /// <summary>
        /// Read a single audit entry.
        /// </summary>
        /// <param name="tenantId">Tenant scope, or null for a system-admin cross-tenant read.</param>
        /// <param name="id">Entry identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The entry, or null if not found.</returns>
        Task<LockAuditEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default);

        /// <summary>
        /// Page through audit entries matching a filter.
        /// </summary>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A page of entries.</returns>
        Task<EnumerationResult<LockAuditEntry>> EnumerateAsync(LockAuditFilter filter, CancellationToken token = default);

        /// <summary>
        /// Produce time-bucketed lock activity for the chart.
        /// </summary>
        /// <param name="filter">Chart filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The chart summary.</returns>
        Task<LockChartSummary> SummarizeAsync(LockChartFilter filter, CancellationToken token = default);

        /// <summary>
        /// Prune audit entries for a tenant older than the given cutoff, honoring the tenant's retention.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="olderThanUtc">Cutoff timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of entries pruned.</returns>
        Task<int> PruneAsync(string tenantId, DateTime olderThanUtc, CancellationToken token = default);
    }
}

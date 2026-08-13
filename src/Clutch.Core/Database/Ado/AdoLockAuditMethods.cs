namespace Clutch.Core.Database.Ado
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;

    /// <summary>
    /// Provider-neutral lock audit data access, including the chart summary.
    /// </summary>
    public class AdoLockAuditMethods : ILockAuditMethods
    {
        #region Private-Members

        private readonly AdoDatabaseDriver _Driver;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Owning driver.</param>
        /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
        public AdoLockAuditMethods(AdoDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task CreateAsync(LockAuditEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string sql =
                "INSERT INTO " + _Driver.Catalog.LockAudit + " (id, tenantid, lockkey, mode, eventtype, credentialid, sessionid, nodeid, fencingtoken, reason, createdutc) " +
                "VALUES (@id, @tid, @key, @mode, @event, @cid, @sid, @node, @fence, @reason, @created);";

            await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", entry.Id);
                AdoDatabaseDriver.Add(command, "tid", entry.TenantId);
                AdoDatabaseDriver.Add(command, "key", entry.LockKey);
                AdoDatabaseDriver.Add(command, "mode", entry.Mode?.ToString());
                AdoDatabaseDriver.Add(command, "event", entry.EventType.ToString());
                AdoDatabaseDriver.Add(command, "cid", entry.CredentialId);
                AdoDatabaseDriver.Add(command, "sid", entry.SessionId);
                AdoDatabaseDriver.Add(command, "node", entry.NodeId);
                AdoDatabaseDriver.Add(command, "fence", entry.FencingToken);
                AdoDatabaseDriver.Add(command, "reason", entry.Reason);
                AdoDatabaseDriver.Add(command, "created", entry.CreatedUtc);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<LockAuditEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string sql = "SELECT * FROM " + _Driver.Catalog.LockAudit + " WHERE id = @id";
            if (!string.IsNullOrEmpty(tenantId)) sql += " AND tenantid = @tid";
            sql += ";";

            return await _Driver.QuerySingleAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", id);
                if (!string.IsNullOrEmpty(tenantId)) AdoDatabaseDriver.Add(command, "tid", tenantId);
            }, AdoConverters.ToLockAuditEntry, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<LockAuditEntry>> EnumerateAsync(LockAuditFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            List<string> modeParams = new List<string>();
            List<string> eventParams = new List<string>();
            List<string> clauses = new List<string>();
            if (!string.IsNullOrEmpty(filter.TenantId)) clauses.Add("tenantid = @tid");
            if (!string.IsNullOrEmpty(filter.LockKeyContains)) clauses.Add(_Driver.Dialect.CaseInsensitiveLike("lockkey", "keyfilter"));
            if (filter.Modes != null && filter.Modes.Count > 0) clauses.Add("mode IN (" + InList("m", filter.Modes.Count, modeParams) + ")");
            if (filter.EventTypes != null && filter.EventTypes.Count > 0) clauses.Add("eventtype IN (" + InList("e", filter.EventTypes.Count, eventParams) + ")");
            if (filter.FromUtc.HasValue) clauses.Add("createdutc >= @from");
            if (filter.ToUtc.HasValue) clauses.Add("createdutc < @to");
            string where = clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : string.Empty;

            Action<DbCommand> bind = command =>
            {
                if (!string.IsNullOrEmpty(filter.TenantId)) AdoDatabaseDriver.Add(command, "tid", filter.TenantId);
                if (!string.IsNullOrEmpty(filter.LockKeyContains)) AdoDatabaseDriver.Add(command, "keyfilter", "%" + filter.LockKeyContains + "%");
                if (filter.Modes != null) for (int i = 0; i < filter.Modes.Count; i++) AdoDatabaseDriver.Add(command, modeParams[i], filter.Modes[i].ToString());
                if (filter.EventTypes != null) for (int i = 0; i < filter.EventTypes.Count; i++) AdoDatabaseDriver.Add(command, eventParams[i], filter.EventTypes[i].ToString());
                if (filter.FromUtc.HasValue) AdoDatabaseDriver.Add(command, "from", filter.FromUtc.Value);
                if (filter.ToUtc.HasValue) AdoDatabaseDriver.Add(command, "to", filter.ToUtc.Value);
            };

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM " + _Driver.Catalog.LockAudit + where + ";", bind, token).ConfigureAwait(false);
            long total = countResult == null ? 0 : Convert.ToInt64(countResult);

            string listSql = "SELECT * FROM " + _Driver.Catalog.LockAudit + where + AdoEnumerationSql.OrderClause(filter, "createdutc", "lockkey") + _Driver.Dialect.LimitOffsetClause() + ";";
            List<LockAuditEntry> objects = await _Driver.QueryAsync(listSql, command =>
            {
                bind(command);
                AdoDatabaseDriver.Add(command, "skip", filter.Skip);
                AdoDatabaseDriver.Add(command, "max", filter.MaxResults);
            }, AdoConverters.ToLockAuditEntry, token).ConfigureAwait(false);

            return EnumerationResult<LockAuditEntry>.Build(filter, total, objects);
        }

        /// <inheritdoc />
        public async Task<LockChartSummary> SummarizeAsync(LockChartFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            LockChartSummary summary = new LockChartSummary();
            summary.FromUtc = filter.FromUtc;
            summary.ToUtc = filter.ToUtc;
            summary.BucketCount = filter.BucketCount;

            double totalMs = (filter.ToUtc - filter.FromUtc).TotalMilliseconds;
            if (totalMs <= 0) totalMs = 1;
            double bucketMs = totalMs / filter.BucketCount;

            DateTime[] starts = new DateTime[filter.BucketCount];
            for (int i = 0; i < filter.BucketCount; i++) starts[i] = filter.FromUtc.AddMilliseconds(bucketMs * i);
            summary.BucketStartsUtc = starts;

            List<string> modeParams = new List<string>();
            List<string> clauses = new List<string>();
            clauses.Add("createdutc >= @from");
            clauses.Add("createdutc < @to");
            if (!string.IsNullOrEmpty(filter.TenantId)) clauses.Add("tenantid = @tid");
            if (!string.IsNullOrEmpty(filter.LockNameContains)) clauses.Add(_Driver.Dialect.CaseInsensitiveLike("lockkey", "keyfilter"));
            if (filter.Modes != null && filter.Modes.Count > 0) clauses.Add("mode IN (" + InList("m", filter.Modes.Count, modeParams) + ")");
            string where = " WHERE " + string.Join(" AND ", clauses);

            List<LockAuditEntry> events = await _Driver.QueryAsync(
                "SELECT id, tenantid, lockkey, mode, eventtype, credentialid, sessionid, nodeid, fencingtoken, reason, createdutc FROM " + _Driver.Catalog.LockAudit + where + ";",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "from", filter.FromUtc);
                    AdoDatabaseDriver.Add(command, "to", filter.ToUtc);
                    if (!string.IsNullOrEmpty(filter.TenantId)) AdoDatabaseDriver.Add(command, "tid", filter.TenantId);
                    if (!string.IsNullOrEmpty(filter.LockNameContains)) AdoDatabaseDriver.Add(command, "keyfilter", "%" + filter.LockNameContains + "%");
                    if (filter.Modes != null) for (int i = 0; i < filter.Modes.Count; i++) AdoDatabaseDriver.Add(command, modeParams[i], filter.Modes[i].ToString());
                },
                AdoConverters.ToLockAuditEntry,
                token).ConfigureAwait(false);

            Dictionary<string, LockChartSeries> seriesByEvent = new Dictionary<string, LockChartSeries>();
            foreach (LockAuditEntry entry in events)
            {
                string label = entry.EventType.ToString();
                if (!seriesByEvent.TryGetValue(label, out LockChartSeries? series))
                {
                    series = new LockChartSeries();
                    series.Label = label;
                    series.Counts = new long[filter.BucketCount];
                    seriesByEvent[label] = series;
                }

                int index = (int)((entry.CreatedUtc - filter.FromUtc).TotalMilliseconds / bucketMs);
                if (index < 0) index = 0;
                if (index >= filter.BucketCount) index = filter.BucketCount - 1;
                series.Counts[index] = series.Counts[index] + 1;
            }

            List<LockChartSeries> ordered = new List<LockChartSeries>();
            foreach (LockEventTypeEnum eventType in Enum.GetValues<LockEventTypeEnum>())
            {
                if (seriesByEvent.TryGetValue(eventType.ToString(), out LockChartSeries? series))
                {
                    ordered.Add(series);
                    seriesByEvent.Remove(eventType.ToString());
                }
            }
            ordered.AddRange(seriesByEvent.Values);

            summary.Series = ordered;
            return summary;
        }

        /// <inheritdoc />
        public async Task<int> PruneAsync(string tenantId, DateTime olderThanUtc, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            return await _Driver.NonQueryAsync(
                "DELETE FROM " + _Driver.Catalog.LockAudit + " WHERE tenantid = @tid AND createdutc < @cutoff;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "cutoff", olderThanUtc);
                },
                token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static string InList(string prefix, int count, List<string> names)
        {
            List<string> placeholders = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string name = prefix + i;
                names.Add(name);
                placeholders.Add("@" + name);
            }
            return string.Join(", ", placeholders);
        }

        #endregion
    }
}

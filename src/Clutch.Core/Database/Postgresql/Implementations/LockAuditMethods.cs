namespace Clutch.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of lock audit data access, including the chart summary.
    /// </summary>
    public class LockAuditMethods : ILockAuditMethods
    {
        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Owning driver.</param>
        /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
        public LockAuditMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task CreateAsync(LockAuditEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string sql = @"
INSERT INTO lock_audit (id, tenantid, lockkey, mode, eventtype, credentialid, sessionid, nodeid, fencingtoken, reason, createdutc)
VALUES (@id, @tid, @key, @mode, @event, @cid, @sid, @node, @fence, @reason, @created);";

            await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", entry.Id);
                parameters.AddWithValue("tid", entry.TenantId);
                parameters.AddWithValue("key", entry.LockKey);
                parameters.AddWithValue("mode", (object?)(entry.Mode?.ToString()) ?? DBNull.Value);
                parameters.AddWithValue("event", entry.EventType.ToString());
                parameters.AddWithValue("cid", (object?)entry.CredentialId ?? DBNull.Value);
                parameters.AddWithValue("sid", (object?)entry.SessionId ?? DBNull.Value);
                parameters.AddWithValue("node", entry.NodeId);
                parameters.AddWithValue("fence", (object?)entry.FencingToken ?? DBNull.Value);
                parameters.AddWithValue("reason", (object?)entry.Reason ?? DBNull.Value);
                parameters.AddWithValue("created", entry.CreatedUtc);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<LockAuditEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string sql = "SELECT * FROM lock_audit WHERE id = @id";
            if (!string.IsNullOrEmpty(tenantId)) sql += " AND tenantid = @tid";
            sql += ";";

            return await _Driver.QuerySingleAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", id);
                if (!string.IsNullOrEmpty(tenantId)) parameters.AddWithValue("tid", tenantId);
            }, Converters.ToLockAuditEntry, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<LockAuditEntry>> EnumerateAsync(LockAuditFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            List<string> clauses = new List<string>();
            if (!string.IsNullOrEmpty(filter.TenantId)) clauses.Add("tenantid = @tid");
            if (!string.IsNullOrEmpty(filter.LockKeyContains)) clauses.Add("lockkey ILIKE @keyfilter");
            if (filter.Modes != null && filter.Modes.Count > 0) clauses.Add("mode = ANY(@modes)");
            if (filter.EventTypes != null && filter.EventTypes.Count > 0) clauses.Add("eventtype = ANY(@events)");
            if (filter.FromUtc.HasValue) clauses.Add("createdutc >= @from");
            if (filter.ToUtc.HasValue) clauses.Add("createdutc < @to");
            string where = clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : string.Empty;

            Action<NpgsqlParameterCollection> bind = parameters =>
            {
                if (!string.IsNullOrEmpty(filter.TenantId)) parameters.AddWithValue("tid", filter.TenantId);
                if (!string.IsNullOrEmpty(filter.LockKeyContains)) parameters.AddWithValue("keyfilter", "%" + filter.LockKeyContains + "%");
                if (filter.Modes != null && filter.Modes.Count > 0) parameters.AddWithValue("modes", ModeStrings(filter.Modes));
                if (filter.EventTypes != null && filter.EventTypes.Count > 0) parameters.AddWithValue("events", EventStrings(filter.EventTypes));
                if (filter.FromUtc.HasValue) parameters.AddWithValue("from", filter.FromUtc.Value);
                if (filter.ToUtc.HasValue) parameters.AddWithValue("to", filter.ToUtc.Value);
            };

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM lock_audit" + where + ";", bind, token).ConfigureAwait(false);
            long total = countResult == null ? 0 : (long)countResult;

            string listSql = "SELECT * FROM lock_audit" + where + EnumerationSql.OrderClause(filter, "createdutc", "lockkey") + " OFFSET @skip LIMIT @max;";
            List<LockAuditEntry> objects = await _Driver.QueryAsync(listSql, parameters =>
            {
                bind(parameters);
                parameters.AddWithValue("skip", filter.Skip);
                parameters.AddWithValue("max", filter.MaxResults);
            }, Converters.ToLockAuditEntry, token).ConfigureAwait(false);

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

            List<string> clauses = new List<string>();
            clauses.Add("createdutc >= @from");
            clauses.Add("createdutc < @to");
            if (!string.IsNullOrEmpty(filter.TenantId)) clauses.Add("tenantid = @tid");
            if (!string.IsNullOrEmpty(filter.LockNameContains)) clauses.Add("lockkey ILIKE @keyfilter");
            if (filter.Modes != null && filter.Modes.Count > 0) clauses.Add("mode = ANY(@modes)");
            string where = " WHERE " + string.Join(" AND ", clauses);

            List<LockAuditEntry> events = await _Driver.QueryAsync(
                "SELECT id, tenantid, lockkey, mode, eventtype, credentialid, sessionid, nodeid, fencingtoken, reason, createdutc FROM lock_audit" + where + ";",
                parameters =>
                {
                    parameters.AddWithValue("from", filter.FromUtc);
                    parameters.AddWithValue("to", filter.ToUtc);
                    if (!string.IsNullOrEmpty(filter.TenantId)) parameters.AddWithValue("tid", filter.TenantId);
                    if (!string.IsNullOrEmpty(filter.LockNameContains)) parameters.AddWithValue("keyfilter", "%" + filter.LockNameContains + "%");
                    if (filter.Modes != null && filter.Modes.Count > 0) parameters.AddWithValue("modes", ModeStrings(filter.Modes));
                },
                Converters.ToLockAuditEntry,
                token).ConfigureAwait(false);

            // Group into one series per operation (event) type, e.g. Acquired, Released, Denied, Expired.
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

            // Emit series in a stable event-type order so colors and stacking stay consistent across refreshes.
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
                "DELETE FROM lock_audit WHERE tenantid = @tid AND createdutc < @cutoff;",
                parameters =>
                {
                    parameters.AddWithValue("tid", tenantId);
                    parameters.AddWithValue("cutoff", olderThanUtc);
                },
                token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static string[] ModeStrings(List<LockModeEnum> modes)
        {
            string[] result = new string[modes.Count];
            for (int i = 0; i < modes.Count; i++) result[i] = modes[i].ToString();
            return result;
        }

        private static string[] EventStrings(List<LockEventTypeEnum> events)
        {
            string[] result = new string[events.Count];
            for (int i = 0; i < events.Count; i++) result[i] = events[i].ToString();
            return result;
        }

        #endregion
    }
}

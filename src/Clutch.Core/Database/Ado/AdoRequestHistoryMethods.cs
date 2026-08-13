namespace Clutch.Core.Database.Ado
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;

    /// <summary>
    /// Provider-neutral request history data access.
    /// </summary>
    public class AdoRequestHistoryMethods : IRequestHistoryMethods
    {
        #region Private-Members

        private readonly AdoDatabaseDriver _Driver;

        private const string ListColumns =
            "id, tenantid, userid, principalname, method, path, url, statuscode, durationms, sourceip, " +
            "requestbodybytes, requestbodytruncated, responsebodybytes, responsebodytruncated, createdutc, completedutc";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Owning driver.</param>
        /// <exception cref="ArgumentNullException">Thrown when driver is null.</exception>
        public AdoRequestHistoryMethods(AdoDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task CreateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string jsonCast = _Driver.Dialect.JsonInsertCast;
            string sql =
                "INSERT INTO " + _Driver.Catalog.RequestHistory + " (id, tenantid, userid, principalname, method, path, url, statuscode, durationms, sourceip, requestheaders, requestbody, requestbodybytes, requestbodytruncated, responseheaders, responsebody, responsebodybytes, responsebodytruncated, createdutc, completedutc) " +
                "VALUES (@id, @tid, @uid, @pname, @method, @path, @url, @status, @duration, @ip, @reqhdr" + jsonCast + ", @reqbody, @reqbytes, @reqtrunc, @resphdr" + jsonCast + ", @respbody, @respbytes, @resptrunc, @created, @completed);";

            await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", entry.Id);
                AdoDatabaseDriver.Add(command, "tid", entry.TenantId);
                AdoDatabaseDriver.Add(command, "uid", entry.UserId);
                AdoDatabaseDriver.Add(command, "pname", entry.PrincipalName);
                AdoDatabaseDriver.Add(command, "method", entry.Method);
                AdoDatabaseDriver.Add(command, "path", entry.Path);
                AdoDatabaseDriver.Add(command, "url", entry.Url);
                AdoDatabaseDriver.Add(command, "status", entry.StatusCode);
                AdoDatabaseDriver.Add(command, "duration", entry.DurationMs);
                AdoDatabaseDriver.Add(command, "ip", entry.SourceIp);
                AdoDatabaseDriver.Add(command, "reqhdr", AdoConverters.SerializeHeaders(entry.RequestHeaders));
                AdoDatabaseDriver.Add(command, "reqbody", entry.RequestBody);
                AdoDatabaseDriver.Add(command, "reqbytes", entry.RequestBodyBytes);
                AdoDatabaseDriver.Add(command, "reqtrunc", entry.RequestBodyTruncated);
                AdoDatabaseDriver.Add(command, "resphdr", AdoConverters.SerializeHeaders(entry.ResponseHeaders));
                AdoDatabaseDriver.Add(command, "respbody", entry.ResponseBody);
                AdoDatabaseDriver.Add(command, "respbytes", entry.ResponseBodyBytes);
                AdoDatabaseDriver.Add(command, "resptrunc", entry.ResponseBodyTruncated);
                AdoDatabaseDriver.Add(command, "created", entry.CreatedUtc);
                AdoDatabaseDriver.Add(command, "completed", entry.CompletedUtc);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<RequestHistoryEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string sql = "SELECT * FROM " + _Driver.Catalog.RequestHistory + " WHERE id = @id";
            if (!string.IsNullOrEmpty(tenantId)) sql += " AND tenantid = @tid";
            sql += ";";

            return await _Driver.QuerySingleAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", id);
                if (!string.IsNullOrEmpty(tenantId)) AdoDatabaseDriver.Add(command, "tid", tenantId);
            }, reader => AdoConverters.ToRequestHistoryEntry(reader, true), token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            string where = BuildWhere(filter, out Action<DbCommand> bind);

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM " + _Driver.Catalog.RequestHistory + where + ";", bind, token).ConfigureAwait(false);
            long total = countResult == null ? 0 : Convert.ToInt64(countResult);

            string listSql = "SELECT " + ListColumns + " FROM " + _Driver.Catalog.RequestHistory + where + AdoEnumerationSql.OrderClause(filter, "createdutc", "path") + _Driver.Dialect.LimitOffsetClause() + ";";
            List<RequestHistoryEntry> objects = await _Driver.QueryAsync(listSql, command =>
            {
                bind(command);
                AdoDatabaseDriver.Add(command, "skip", filter.Skip);
                AdoDatabaseDriver.Add(command, "max", filter.MaxResults);
            }, reader => AdoConverters.ToRequestHistoryEntry(reader, false), token).ConfigureAwait(false);

            return EnumerationResult<RequestHistoryEntry>.Build(filter, total, objects);
        }

        /// <inheritdoc />
        public async Task<RequestHistorySummary> SummarizeAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            DateTime from = filter.FromUtc ?? DateTime.UtcNow.AddDays(-1);
            DateTime to = filter.ToUtc ?? DateTime.UtcNow;
            double bucketMs = filter.BucketMinutes * 60000.0;
            double totalMs = (to - from).TotalMilliseconds;
            if (totalMs <= 0) totalMs = bucketMs;
            int bucketCount = (int)Math.Ceiling(totalMs / bucketMs);
            if (bucketCount < 1) bucketCount = 1;

            RequestHistorySummary summary = new RequestHistorySummary();
            List<RequestHistoryBucket> buckets = new List<RequestHistoryBucket>();
            double[] durationSums = new double[bucketCount];
            long[] durationCounts = new long[bucketCount];
            for (int i = 0; i < bucketCount; i++)
            {
                RequestHistoryBucket bucket = new RequestHistoryBucket();
                bucket.BucketStartUtc = from.AddMilliseconds(bucketMs * i);
                bucket.BucketEndUtc = from.AddMilliseconds(bucketMs * (i + 1));
                buckets.Add(bucket);
            }

            RequestHistoryFilter rangeFilter = filter;
            rangeFilter.FromUtc = from;
            rangeFilter.ToUtc = to;
            string where = BuildWhere(rangeFilter, out Action<DbCommand> bind);

            List<RequestHistoryEntry> rows = await _Driver.QueryAsync(
                "SELECT " + ListColumns + " FROM " + _Driver.Catalog.RequestHistory + where + ";",
                bind,
                reader => AdoConverters.ToRequestHistoryEntry(reader, false),
                token).ConfigureAwait(false);

            double totalDuration = 0;
            foreach (RequestHistoryEntry row in rows)
            {
                int index = (int)((row.CreatedUtc - from).TotalMilliseconds / bucketMs);
                if (index < 0) index = 0;
                if (index >= bucketCount) index = bucketCount - 1;

                bool success = row.StatusCode >= 200 && row.StatusCode < 400;
                if (success) buckets[index].SuccessCount = buckets[index].SuccessCount + 1;
                else buckets[index].FailureCount = buckets[index].FailureCount + 1;

                durationSums[index] = durationSums[index] + row.DurationMs;
                durationCounts[index] = durationCounts[index] + 1;
                totalDuration += row.DurationMs;

                summary.TotalCount = summary.TotalCount + 1;
                if (success) summary.TotalSuccess = summary.TotalSuccess + 1;
                else summary.TotalFailure = summary.TotalFailure + 1;
            }

            for (int i = 0; i < bucketCount; i++)
            {
                buckets[i].AverageDurationMs = durationCounts[i] > 0 ? durationSums[i] / durationCounts[i] : 0;
            }
            summary.AverageDurationMs = summary.TotalCount > 0 ? totalDuration / summary.TotalCount : 0;
            summary.Buckets = buckets;
            return summary;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string sql = "DELETE FROM " + _Driver.Catalog.RequestHistory + " WHERE id = @id";
            if (!string.IsNullOrEmpty(tenantId)) sql += " AND tenantid = @tid";
            sql += ";";

            int affected = await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", id);
                if (!string.IsNullOrEmpty(tenantId)) AdoDatabaseDriver.Add(command, "tid", tenantId);
            }, token).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<int> DeleteManyAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            string where = BuildWhere(filter, out Action<DbCommand> bind);
            return await _Driver.NonQueryAsync("DELETE FROM " + _Driver.Catalog.RequestHistory + where + ";", bind, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            return await _Driver.NonQueryAsync(
                "DELETE FROM " + _Driver.Catalog.RequestHistory + " WHERE createdutc < @cutoff;",
                command => AdoDatabaseDriver.Add(command, "cutoff", olderThanUtc),
                token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private string BuildWhere(RequestHistoryFilter filter, out Action<DbCommand> bind)
        {
            List<string> clauses = new List<string>();
            if (!string.IsNullOrEmpty(filter.TenantId)) clauses.Add("tenantid = @tid");
            if (!string.IsNullOrEmpty(filter.UserId)) clauses.Add("userid = @uid");
            if (!string.IsNullOrEmpty(filter.Method)) clauses.Add("method = @method");
            if (filter.StatusCode.HasValue) clauses.Add("statuscode = @status");
            if (!string.IsNullOrEmpty(filter.PathContains)) clauses.Add(_Driver.Dialect.CaseInsensitiveLike("path", "pathfilter"));
            if (filter.FromUtc.HasValue) clauses.Add("createdutc >= @from");
            if (filter.ToUtc.HasValue) clauses.Add("createdutc < @to");
            string where = clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : string.Empty;

            bind = command =>
            {
                if (!string.IsNullOrEmpty(filter.TenantId)) AdoDatabaseDriver.Add(command, "tid", filter.TenantId);
                if (!string.IsNullOrEmpty(filter.UserId)) AdoDatabaseDriver.Add(command, "uid", filter.UserId);
                if (!string.IsNullOrEmpty(filter.Method)) AdoDatabaseDriver.Add(command, "method", filter.Method);
                if (filter.StatusCode.HasValue) AdoDatabaseDriver.Add(command, "status", filter.StatusCode.Value);
                if (!string.IsNullOrEmpty(filter.PathContains)) AdoDatabaseDriver.Add(command, "pathfilter", "%" + filter.PathContains + "%");
                if (filter.FromUtc.HasValue) AdoDatabaseDriver.Add(command, "from", filter.FromUtc.Value);
                if (filter.ToUtc.HasValue) AdoDatabaseDriver.Add(command, "to", filter.ToUtc.Value);
            };

            return where;
        }

        #endregion
    }
}

namespace Clutch.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of request history data access.
    /// </summary>
    public class RequestHistoryMethods : IRequestHistoryMethods
    {
        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;

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
        public RequestHistoryMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task CreateAsync(RequestHistoryEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string sql = @"
INSERT INTO request_history (id, tenantid, userid, principalname, method, path, url, statuscode, durationms, sourceip, requestheaders, requestbody, requestbodybytes, requestbodytruncated, responseheaders, responsebody, responsebodybytes, responsebodytruncated, createdutc, completedutc)
VALUES (@id, @tid, @uid, @pname, @method, @path, @url, @status, @duration, @ip, @reqhdr, @reqbody, @reqbytes, @reqtrunc, @resphdr, @respbody, @respbytes, @resptrunc, @created, @completed);";

            await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", entry.Id);
                parameters.AddWithValue("tid", (object?)entry.TenantId ?? DBNull.Value);
                parameters.AddWithValue("uid", (object?)entry.UserId ?? DBNull.Value);
                parameters.AddWithValue("pname", (object?)entry.PrincipalName ?? DBNull.Value);
                parameters.AddWithValue("method", entry.Method);
                parameters.AddWithValue("path", entry.Path);
                parameters.AddWithValue("url", entry.Url);
                parameters.AddWithValue("status", entry.StatusCode);
                parameters.AddWithValue("duration", entry.DurationMs);
                parameters.AddWithValue("ip", (object?)entry.SourceIp ?? DBNull.Value);
                NpgsqlParameter reqHdr = new NpgsqlParameter("reqhdr", NpgsqlTypes.NpgsqlDbType.Jsonb);
                reqHdr.Value = Converters.SerializeHeaders(entry.RequestHeaders);
                parameters.Add(reqHdr);
                parameters.AddWithValue("reqbody", (object?)entry.RequestBody ?? DBNull.Value);
                parameters.AddWithValue("reqbytes", entry.RequestBodyBytes);
                parameters.AddWithValue("reqtrunc", entry.RequestBodyTruncated);
                NpgsqlParameter respHdr = new NpgsqlParameter("resphdr", NpgsqlTypes.NpgsqlDbType.Jsonb);
                respHdr.Value = Converters.SerializeHeaders(entry.ResponseHeaders);
                parameters.Add(respHdr);
                parameters.AddWithValue("respbody", (object?)entry.ResponseBody ?? DBNull.Value);
                parameters.AddWithValue("respbytes", entry.ResponseBodyBytes);
                parameters.AddWithValue("resptrunc", entry.ResponseBodyTruncated);
                parameters.AddWithValue("created", entry.CreatedUtc);
                parameters.AddWithValue("completed", (object?)entry.CompletedUtc ?? DBNull.Value);
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<RequestHistoryEntry?> ReadAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string sql = "SELECT * FROM request_history WHERE id = @id";
            if (!string.IsNullOrEmpty(tenantId)) sql += " AND tenantid = @tid";
            sql += ";";

            return await _Driver.QuerySingleAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", id);
                if (!string.IsNullOrEmpty(tenantId)) parameters.AddWithValue("tid", tenantId);
            }, reader => Converters.ToRequestHistoryEntry(reader, true), token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            string where = BuildWhere(filter, out Action<NpgsqlParameterCollection> bind);

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM request_history" + where + ";", bind, token).ConfigureAwait(false);
            long total = countResult == null ? 0 : (long)countResult;

            string listSql = "SELECT " + ListColumns + " FROM request_history" + where + EnumerationSql.OrderClause(filter, "createdutc", "path") + " OFFSET @skip LIMIT @max;";
            List<RequestHistoryEntry> objects = await _Driver.QueryAsync(listSql, parameters =>
            {
                bind(parameters);
                parameters.AddWithValue("skip", filter.Skip);
                parameters.AddWithValue("max", filter.MaxResults);
            }, reader => Converters.ToRequestHistoryEntry(reader, false), token).ConfigureAwait(false);

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
            string where = BuildWhere(rangeFilter, out Action<NpgsqlParameterCollection> bind);

            List<RequestHistoryEntry> rows = await _Driver.QueryAsync(
                "SELECT " + ListColumns + " FROM request_history" + where + ";",
                bind,
                reader => Converters.ToRequestHistoryEntry(reader, false),
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

            string sql = "DELETE FROM request_history WHERE id = @id";
            if (!string.IsNullOrEmpty(tenantId)) sql += " AND tenantid = @tid";
            sql += ";";

            int affected = await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", id);
                if (!string.IsNullOrEmpty(tenantId)) parameters.AddWithValue("tid", tenantId);
            }, token).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task<int> DeleteManyAsync(RequestHistoryFilter filter, CancellationToken token = default)
        {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            string where = BuildWhere(filter, out Action<NpgsqlParameterCollection> bind);
            return await _Driver.NonQueryAsync("DELETE FROM request_history" + where + ";", bind, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> PruneAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            return await _Driver.NonQueryAsync(
                "DELETE FROM request_history WHERE createdutc < @cutoff;",
                parameters => parameters.AddWithValue("cutoff", olderThanUtc),
                token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static string BuildWhere(RequestHistoryFilter filter, out Action<NpgsqlParameterCollection> bind)
        {
            List<string> clauses = new List<string>();
            if (!string.IsNullOrEmpty(filter.TenantId)) clauses.Add("tenantid = @tid");
            if (!string.IsNullOrEmpty(filter.UserId)) clauses.Add("userid = @uid");
            if (!string.IsNullOrEmpty(filter.Method)) clauses.Add("method = @method");
            if (filter.StatusCode.HasValue) clauses.Add("statuscode = @status");
            if (!string.IsNullOrEmpty(filter.PathContains)) clauses.Add("path ILIKE @pathfilter");
            if (filter.FromUtc.HasValue) clauses.Add("createdutc >= @from");
            if (filter.ToUtc.HasValue) clauses.Add("createdutc < @to");
            string where = clauses.Count > 0 ? " WHERE " + string.Join(" AND ", clauses) : string.Empty;

            bind = parameters =>
            {
                if (!string.IsNullOrEmpty(filter.TenantId)) parameters.AddWithValue("tid", filter.TenantId);
                if (!string.IsNullOrEmpty(filter.UserId)) parameters.AddWithValue("uid", filter.UserId);
                if (!string.IsNullOrEmpty(filter.Method)) parameters.AddWithValue("method", filter.Method);
                if (filter.StatusCode.HasValue) parameters.AddWithValue("status", filter.StatusCode.Value);
                if (!string.IsNullOrEmpty(filter.PathContains)) parameters.AddWithValue("pathfilter", "%" + filter.PathContains + "%");
                if (filter.FromUtc.HasValue) parameters.AddWithValue("from", filter.FromUtc.Value);
                if (filter.ToUtc.HasValue) parameters.AddWithValue("to", filter.ToUtc.Value);
            };

            return where;
        }

        #endregion
    }
}

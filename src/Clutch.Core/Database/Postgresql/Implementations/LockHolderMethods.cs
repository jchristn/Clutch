namespace Clutch.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Enums;
    using Clutch.Core.Helpers;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Services;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of lock holder data access, including the atomic acquire path. The
    /// acquire serializes concurrent attempts on a key by taking a row lock on the definition row.
    /// </summary>
    public class LockHolderMethods : ILockHolderMethods
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
        public LockHolderMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<AcquireOutcome> TryAcquireAsync(AcquireRequest request, int defaultLeaseMs, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            AcquireOutcome outcome = new AcquireOutcome();

            await using (NpgsqlConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    // 1. Lock the definition row (serializes concurrent acquires on this key across nodes).
                    LockDefinition? definition = await ReadDefinitionForUpdateAsync(connection, transaction, request.TenantId, request.LockKey, token).ConfigureAwait(false);
                    bool created = false;

                    if (definition == null)
                    {
                        LockPolicySpec spec = request.Policy ?? new LockPolicySpec();
                        definition = spec.ToDefinition(request.TenantId, request.LockKey, string.IsNullOrEmpty(request.CredentialId) ? null : request.CredentialId);
                        await InsertDefinitionAsync(connection, transaction, definition, token).ConfigureAwait(false);
                        created = true;
                        await InsertAuditAsync(connection, transaction,
                            BuildAudit(request.TenantId, request.LockKey, request.Mode, LockEventTypeEnum.PolicyCreated, request, null, "First acquirer created the lock policy."), token).ConfigureAwait(false);
                    }
                    else if (request.StrictPolicy && request.Policy != null && PolicyConflicts(definition, request.Policy))
                    {
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                        outcome.Result = AcquireResultEnum.PolicyConflict;
                        outcome.Definition = definition;
                        outcome.Reason = "The supplied policy conflicts with the existing lock definition.";
                        return outcome;
                    }

                    // 2. Reclaim expired holders on this key.
                    DateTime now = DateTime.UtcNow;
                    List<LockHolder> expired = await DeleteExpiredForKeyAsync(connection, transaction, request.TenantId, request.LockKey, now, token).ConfigureAwait(false);
                    foreach (LockHolder dead in expired)
                    {
                        await InsertAuditAsync(connection, transaction,
                            BuildAuditFromHolder(dead, LockEventTypeEnum.Expired, request.NodeId, "Lease expired; reclaimed during acquire."), token).ConfigureAwait(false);
                    }
                    if (expired.Count > 0)
                    {
                        await NotifyAsync(connection, transaction, request.TenantId, request.LockKey, token).ConfigureAwait(false);
                    }

                    // 3. Count current holders by mode.
                    HolderCounts counts = await CountHoldersAsync(connection, transaction, request.TenantId, request.LockKey, token).ConfigureAwait(false);

                    // 4. Evaluate compatibility under the key's policy.
                    CompatibilityResult compatibility = LockCompatibilityEvaluator.Evaluate(definition, counts, request.Mode);
                    if (!compatibility.Compatible)
                    {
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                        outcome.Result = AcquireResultEnum.Incompatible;
                        outcome.Definition = definition;
                        outcome.Reason = compatibility.Reason;
                        return outcome;
                    }

                    // 5. Assign a fencing token.
                    long fencing = await IncrementFencingAsync(connection, transaction, definition.Id, now, token).ConfigureAwait(false);
                    definition.FencingCounter = fencing;

                    // 6. Compute the lease.
                    int effectiveDefault = definition.DefaultLeaseMs > 0 ? definition.DefaultLeaseMs : defaultLeaseMs;
                    int leaseMs = request.RequestedLeaseMs.HasValue && request.RequestedLeaseMs.Value > 0
                        ? request.RequestedLeaseMs.Value
                        : effectiveDefault;
                    leaseMs = Math.Clamp(leaseMs, 1000, definition.MaxLeaseMs);
                    DateTime leaseExpires = now.AddMilliseconds(leaseMs);

                    // 7. Insert the holder.
                    LockHolder holder = new LockHolder();
                    holder.TenantId = request.TenantId;
                    holder.LockKey = request.LockKey;
                    holder.LockDefinitionId = definition.Id;
                    holder.Mode = request.Mode;
                    holder.CredentialId = request.CredentialId;
                    holder.SessionId = request.SessionId;
                    holder.NodeId = request.NodeId;
                    holder.FencingToken = fencing;
                    holder.AcquiredUtc = now;
                    holder.LeaseExpiresUtc = leaseExpires;
                    holder.LastHeartbeatUtc = now;
                    holder.CreatedUtc = now;
                    holder.LastUpdateUtc = now;
                    await InsertHolderAsync(connection, transaction, holder, token).ConfigureAwait(false);

                    // 8. Audit and commit.
                    await InsertAuditAsync(connection, transaction,
                        BuildAuditFromHolder(holder, LockEventTypeEnum.Acquired, request.NodeId, created ? "Granted (policy created)." : "Granted."), token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);

                    outcome.Result = AcquireResultEnum.Granted;
                    outcome.Holder = holder;
                    outcome.FencingToken = fencing;
                    outcome.LeaseExpiresUtc = leaseExpires;
                    outcome.Definition = definition;
                    return outcome;
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> ReleaseAsync(string tenantId, string holderId, string sessionId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(holderId)) throw new ArgumentNullException(nameof(holderId));

            await using (NpgsqlConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    List<LockHolder> deleted = await DeleteHoldersReturningAsync(connection, transaction,
                        "DELETE FROM lock_holders WHERE tenantid = @tid AND id = @id AND sessionid = @sid RETURNING *;",
                        parameters =>
                        {
                            parameters.AddWithValue("tid", tenantId);
                            parameters.AddWithValue("id", holderId);
                            parameters.AddWithValue("sid", (object?)sessionId ?? DBNull.Value);
                        }, token).ConfigureAwait(false);

                    foreach (LockHolder holder in deleted)
                    {
                        await InsertAuditAsync(connection, transaction, BuildAuditFromHolder(holder, LockEventTypeEnum.Released, holder.NodeId, "Released by owner."), token).ConfigureAwait(false);
                        await NotifyAsync(connection, transaction, holder.TenantId, holder.LockKey, token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return deleted.Count > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<List<LockHolder>> HeartbeatAsync(string sessionId, IEnumerable<string> holderIds, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            if (holderIds == null) throw new ArgumentNullException(nameof(holderIds));

            List<string> ids = new List<string>(holderIds);
            if (ids.Count == 0) return new List<LockHolder>();

            DateTime now = DateTime.UtcNow;
            string sql = @"
UPDATE lock_holders h
SET leaseexpiresutc = LEAST(
        @now + make_interval(secs => d.defaultleasems / 1000.0),
        h.acquiredutc + make_interval(secs => d.maxholdms / 1000.0)),
    lastheartbeatutc = @now,
    lastupdateutc = @now
FROM lock_definitions d
WHERE h.lockdefinitionid = d.id
  AND h.sessionid = @sid
  AND h.id = ANY(@ids)
RETURNING h.*;";

            return await _Driver.QueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("now", now);
                parameters.AddWithValue("sid", sessionId);
                parameters.AddWithValue("ids", ids.ToArray());
            }, Converters.ToLockHolder, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<string>> ReleaseAllForSessionAsync(string sessionId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));

            await using (NpgsqlConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    List<LockHolder> deleted = await DeleteHoldersReturningAsync(connection, transaction,
                        "DELETE FROM lock_holders WHERE sessionid = @sid RETURNING *;",
                        parameters => parameters.AddWithValue("sid", sessionId), token).ConfigureAwait(false);

                    HashSet<string> keys = new HashSet<string>();
                    foreach (LockHolder holder in deleted)
                    {
                        await InsertAuditAsync(connection, transaction, BuildAuditFromHolder(holder, LockEventTypeEnum.Released, holder.NodeId, "Session closed."), token).ConfigureAwait(false);
                        await NotifyAsync(connection, transaction, holder.TenantId, holder.LockKey, token).ConfigureAwait(false);
                        keys.Add(holder.TenantId + "|" + holder.LockKey);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return new List<string>(keys);
                }
            }
        }

        /// <inheritdoc />
        public async Task<LockHolder?> RevokeAsync(string tenantId, string holderId, string reason, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(holderId)) throw new ArgumentNullException(nameof(holderId));

            await using (NpgsqlConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    List<LockHolder> deleted = await DeleteHoldersReturningAsync(connection, transaction,
                        "DELETE FROM lock_holders WHERE tenantid = @tid AND id = @id RETURNING *;",
                        parameters =>
                        {
                            parameters.AddWithValue("tid", tenantId);
                            parameters.AddWithValue("id", holderId);
                        }, token).ConfigureAwait(false);

                    LockHolder? result = deleted.Count > 0 ? deleted[0] : null;
                    if (result != null)
                    {
                        await InsertAuditAsync(connection, transaction, BuildAuditFromHolder(result, LockEventTypeEnum.Revoked, result.NodeId, string.IsNullOrEmpty(reason) ? "Revoked." : reason), token).ConfigureAwait(false);
                        await NotifyAsync(connection, transaction, result.TenantId, result.LockKey, token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return result;
                }
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> PurgeExpiredAsync(DateTime olderThanUtc, string nodeId, CancellationToken token = default)
        {
            await using (NpgsqlConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    List<LockHolder> deleted = await DeleteHoldersReturningAsync(connection, transaction,
                        "DELETE FROM lock_holders WHERE leaseexpiresutc < @cutoff RETURNING *;",
                        parameters => parameters.AddWithValue("cutoff", olderThanUtc), token).ConfigureAwait(false);

                    HashSet<string> keys = new HashSet<string>();
                    foreach (LockHolder holder in deleted)
                    {
                        await InsertAuditAsync(connection, transaction, BuildAuditFromHolder(holder, LockEventTypeEnum.Expired, nodeId, "Lease expired; reclaimed by sweep."), token).ConfigureAwait(false);
                        await NotifyAsync(connection, transaction, holder.TenantId, holder.LockKey, token).ConfigureAwait(false);
                        keys.Add(holder.TenantId + "|" + holder.LockKey);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return new List<string>(keys);
                }
            }
        }

        /// <inheritdoc />
        public async Task<LockHolder?> ReadAsync(string tenantId, string holderId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(holderId)) throw new ArgumentNullException(nameof(holderId));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM lock_holders WHERE tenantid = @tid AND id = @id;",
                parameters =>
                {
                    parameters.AddWithValue("tid", tenantId);
                    parameters.AddWithValue("id", holderId);
                },
                Converters.ToLockHolder,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<LockHolder>> EnumerateByTenantAsync(string tenantId, string? lockKeyContains, LockModeEnum? mode, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string sql = "SELECT * FROM lock_holders WHERE tenantid = @tid";
            if (!string.IsNullOrEmpty(lockKeyContains)) sql += " AND lockkey ILIKE @keyfilter";
            if (mode.HasValue) sql += " AND mode = @mode";
            sql += " ORDER BY acquiredutc DESC;";

            return await _Driver.QueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("tid", tenantId);
                if (!string.IsNullOrEmpty(lockKeyContains)) parameters.AddWithValue("keyfilter", "%" + lockKeyContains + "%");
                if (mode.HasValue) parameters.AddWithValue("mode", mode.Value.ToString());
            }, Converters.ToLockHolder, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Clutch.Core.Enumeration.EnumerationResult<LockHolder>> EnumerateByTenantAsync(string tenantId, string? lockKeyContains, LockModeEnum? mode, Clutch.Core.Enumeration.EnumerationQuery query, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            query ??= new Clutch.Core.Enumeration.EnumerationQuery();

            string where = " WHERE tenantid = @tid";
            if (!string.IsNullOrEmpty(lockKeyContains)) where += " AND lockkey ILIKE @keyfilter";
            if (mode.HasValue) where += " AND mode = @mode";

            Action<NpgsqlParameterCollection> bindFilters = parameters =>
            {
                parameters.AddWithValue("tid", tenantId);
                if (!string.IsNullOrEmpty(lockKeyContains)) parameters.AddWithValue("keyfilter", "%" + lockKeyContains + "%");
                if (mode.HasValue) parameters.AddWithValue("mode", mode.Value.ToString());
            };

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM lock_holders" + where + ";", bindFilters, token).ConfigureAwait(false);
            long total = countResult == null ? 0 : (long)countResult;

            string sql = "SELECT * FROM lock_holders" + where + EnumerationSql.OrderClause(query, "acquiredutc", "lockkey") + " OFFSET @skip LIMIT @max;";
            List<LockHolder> objects = await _Driver.QueryAsync(sql, parameters =>
            {
                bindFilters(parameters);
                parameters.AddWithValue("skip", query.Skip);
                parameters.AddWithValue("max", query.MaxResults);
            }, Converters.ToLockHolder, token).ConfigureAwait(false);

            return Clutch.Core.Enumeration.EnumerationResult<LockHolder>.Build(query, total, objects);
        }

        /// <inheritdoc />
        public async Task<List<LockHolder>> EnumerateByKeyAsync(string tenantId, string lockKey, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(lockKey)) throw new ArgumentNullException(nameof(lockKey));

            return await _Driver.QueryAsync(
                "SELECT * FROM lock_holders WHERE tenantid = @tid AND lockkey = @key ORDER BY acquiredutc ASC;",
                parameters =>
                {
                    parameters.AddWithValue("tid", tenantId);
                    parameters.AddWithValue("key", lockKey);
                },
                Converters.ToLockHolder,
                token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static async Task<LockDefinition?> ReadDefinitionForUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tenantId, string lockKey, CancellationToken token)
        {
            await using (NpgsqlCommand command = new NpgsqlCommand("SELECT * FROM lock_definitions WHERE tenantid = @tid AND lockkey = @key FOR UPDATE;", connection, transaction))
            {
                command.Parameters.AddWithValue("tid", tenantId);
                command.Parameters.AddWithValue("key", lockKey);
                await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        return Converters.ToLockDefinition(reader);
                    }
                }
            }
            return null;
        }

        private static async Task InsertDefinitionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, LockDefinition definition, CancellationToken token)
        {
            string sql = @"
INSERT INTO lock_definitions (id, tenantid, lockkey, readmaxholders, writeexclusivity, writemaxholders, writeblocksreads, defaultleasems, maxleasems, maxholdms, fencingcounter, firstacquiredbycredentialid, active, createdutc, lastupdateutc)
VALUES (@id, @tid, @key, @readmax, @wexcl, @wmax, @wblocks, @deflease, @maxlease, @maxhold, 0, @firstby, TRUE, @created, @updated);";
            await using (NpgsqlCommand command = new NpgsqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("id", definition.Id);
                command.Parameters.AddWithValue("tid", definition.TenantId);
                command.Parameters.AddWithValue("key", definition.LockKey);
                command.Parameters.AddWithValue("readmax", definition.ReadMaxHolders);
                command.Parameters.AddWithValue("wexcl", definition.WriteExclusivity.ToString());
                command.Parameters.AddWithValue("wmax", definition.WriteMaxHolders);
                command.Parameters.AddWithValue("wblocks", definition.WriteBlocksReads);
                command.Parameters.AddWithValue("deflease", definition.DefaultLeaseMs);
                command.Parameters.AddWithValue("maxlease", definition.MaxLeaseMs);
                command.Parameters.AddWithValue("maxhold", definition.MaxHoldMs);
                command.Parameters.AddWithValue("firstby", (object?)definition.FirstAcquiredByCredentialId ?? DBNull.Value);
                command.Parameters.AddWithValue("created", definition.CreatedUtc);
                command.Parameters.AddWithValue("updated", definition.LastUpdateUtc);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task<List<LockHolder>> DeleteExpiredForKeyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tenantId, string lockKey, DateTime now, CancellationToken token)
        {
            List<LockHolder> holders = new List<LockHolder>();
            await using (NpgsqlCommand command = new NpgsqlCommand("DELETE FROM lock_holders WHERE tenantid = @tid AND lockkey = @key AND leaseexpiresutc < @now RETURNING *;", connection, transaction))
            {
                command.Parameters.AddWithValue("tid", tenantId);
                command.Parameters.AddWithValue("key", lockKey);
                command.Parameters.AddWithValue("now", now);
                await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) holders.Add(Converters.ToLockHolder(reader));
                }
            }
            return holders;
        }

        private static async Task<HolderCounts> CountHoldersAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tenantId, string lockKey, CancellationToken token)
        {
            HolderCounts counts = new HolderCounts();
            await using (NpgsqlCommand command = new NpgsqlCommand("SELECT mode, COUNT(*) AS cnt FROM lock_holders WHERE tenantid = @tid AND lockkey = @key GROUP BY mode;", connection, transaction))
            {
                command.Parameters.AddWithValue("tid", tenantId);
                command.Parameters.AddWithValue("key", lockKey);
                await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string mode = reader.GetString(0);
                        int count = (int)reader.GetInt64(1);
                        if (mode == LockModeEnum.Read.ToString()) counts.Read = count;
                        else if (mode == LockModeEnum.Write.ToString()) counts.Write = count;
                        else if (mode == LockModeEnum.Delete.ToString()) counts.Delete = count;
                    }
                }
            }
            return counts;
        }

        private static async Task<long> IncrementFencingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string definitionId, DateTime now, CancellationToken token)
        {
            await using (NpgsqlCommand command = new NpgsqlCommand("UPDATE lock_definitions SET fencingcounter = fencingcounter + 1, lastupdateutc = @now WHERE id = @id RETURNING fencingcounter;", connection, transaction))
            {
                command.Parameters.AddWithValue("id", definitionId);
                command.Parameters.AddWithValue("now", now);
                object? result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return result == null ? 0 : (long)result;
            }
        }

        private static async Task InsertHolderAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, LockHolder holder, CancellationToken token)
        {
            string sql = @"
INSERT INTO lock_holders (id, tenantid, lockkey, lockdefinitionid, mode, credentialid, sessionid, nodeid, fencingtoken, acquiredutc, leaseexpiresutc, lastheartbeatutc, active, createdutc, lastupdateutc)
VALUES (@id, @tid, @key, @defid, @mode, @cid, @sid, @node, @fence, @acq, @lease, @hb, TRUE, @created, @updated);";
            await using (NpgsqlCommand command = new NpgsqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("id", holder.Id);
                command.Parameters.AddWithValue("tid", holder.TenantId);
                command.Parameters.AddWithValue("key", holder.LockKey);
                command.Parameters.AddWithValue("defid", holder.LockDefinitionId);
                command.Parameters.AddWithValue("mode", holder.Mode.ToString());
                command.Parameters.AddWithValue("cid", holder.CredentialId);
                command.Parameters.AddWithValue("sid", holder.SessionId);
                command.Parameters.AddWithValue("node", holder.NodeId);
                command.Parameters.AddWithValue("fence", holder.FencingToken);
                command.Parameters.AddWithValue("acq", holder.AcquiredUtc);
                command.Parameters.AddWithValue("lease", holder.LeaseExpiresUtc);
                command.Parameters.AddWithValue("hb", holder.LastHeartbeatUtc);
                command.Parameters.AddWithValue("created", holder.CreatedUtc);
                command.Parameters.AddWithValue("updated", holder.LastUpdateUtc);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private async Task<List<LockHolder>> DeleteHoldersReturningAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Action<NpgsqlParameterCollection> bind, CancellationToken token)
        {
            List<LockHolder> holders = new List<LockHolder>();
            await using (NpgsqlCommand command = new NpgsqlCommand(sql, connection, transaction))
            {
                bind(command.Parameters);
                await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) holders.Add(Converters.ToLockHolder(reader));
                }
            }
            return holders;
        }

        private static async Task InsertAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, LockAuditEntry entry, CancellationToken token)
        {
            string sql = @"
INSERT INTO lock_audit (id, tenantid, lockkey, mode, eventtype, credentialid, sessionid, nodeid, fencingtoken, reason, createdutc)
VALUES (@id, @tid, @key, @mode, @event, @cid, @sid, @node, @fence, @reason, @created);";
            await using (NpgsqlCommand command = new NpgsqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("id", entry.Id);
                command.Parameters.AddWithValue("tid", entry.TenantId);
                command.Parameters.AddWithValue("key", entry.LockKey);
                command.Parameters.AddWithValue("mode", (object?)(entry.Mode?.ToString()) ?? DBNull.Value);
                command.Parameters.AddWithValue("event", entry.EventType.ToString());
                command.Parameters.AddWithValue("cid", (object?)entry.CredentialId ?? DBNull.Value);
                command.Parameters.AddWithValue("sid", (object?)entry.SessionId ?? DBNull.Value);
                command.Parameters.AddWithValue("node", entry.NodeId);
                command.Parameters.AddWithValue("fence", (object?)entry.FencingToken ?? DBNull.Value);
                command.Parameters.AddWithValue("reason", (object?)entry.Reason ?? DBNull.Value);
                command.Parameters.AddWithValue("created", entry.CreatedUtc);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task NotifyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tenantId, string lockKey, CancellationToken token)
        {
            await using (NpgsqlCommand command = new NpgsqlCommand("SELECT pg_notify(@channel, @payload);", connection, transaction))
            {
                command.Parameters.AddWithValue("channel", Constants.LockReleaseChannel);
                command.Parameters.AddWithValue("payload", tenantId + "|" + lockKey);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static LockAuditEntry BuildAudit(string tenantId, string lockKey, LockModeEnum? mode, LockEventTypeEnum eventType, AcquireRequest request, long? fencingToken, string reason)
        {
            LockAuditEntry entry = new LockAuditEntry();
            entry.Id = IdGenerator.GenerateLockAuditId();
            entry.TenantId = tenantId;
            entry.LockKey = lockKey;
            entry.Mode = mode;
            entry.EventType = eventType;
            entry.CredentialId = string.IsNullOrEmpty(request.CredentialId) ? null : request.CredentialId;
            entry.SessionId = string.IsNullOrEmpty(request.SessionId) ? null : request.SessionId;
            entry.NodeId = request.NodeId;
            entry.FencingToken = fencingToken;
            entry.Reason = reason;
            entry.CreatedUtc = DateTime.UtcNow;
            return entry;
        }

        private static LockAuditEntry BuildAuditFromHolder(LockHolder holder, LockEventTypeEnum eventType, string nodeId, string reason)
        {
            LockAuditEntry entry = new LockAuditEntry();
            entry.Id = IdGenerator.GenerateLockAuditId();
            entry.TenantId = holder.TenantId;
            entry.LockKey = holder.LockKey;
            entry.Mode = holder.Mode;
            entry.EventType = eventType;
            entry.CredentialId = string.IsNullOrEmpty(holder.CredentialId) ? null : holder.CredentialId;
            entry.SessionId = string.IsNullOrEmpty(holder.SessionId) ? null : holder.SessionId;
            entry.NodeId = string.IsNullOrEmpty(nodeId) ? holder.NodeId : nodeId;
            entry.FencingToken = holder.FencingToken;
            entry.Reason = reason;
            entry.CreatedUtc = DateTime.UtcNow;
            return entry;
        }

        private static bool PolicyConflicts(LockDefinition definition, LockPolicySpec spec)
        {
            return definition.ReadMaxHolders != spec.ReadMaxHolders
                || definition.WriteExclusivity != spec.WriteExclusivity
                || definition.WriteMaxHolders != spec.WriteMaxHolders
                || definition.WriteBlocksReads != spec.WriteBlocksReads;
        }

        #endregion
    }
}

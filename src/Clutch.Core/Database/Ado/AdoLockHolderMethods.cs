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
    using Clutch.Core.Helpers;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Services;

    /// <summary>
    /// Provider-neutral lock holder data access, including the atomic acquire path. The acquire serializes
    /// concurrent attempts on a key using the dialect's definition-row lock (FOR UPDATE, UPDLOCK/HOLDLOCK,
    /// or an IMMEDIATE transaction on SQLite). Mutations that must return the affected rows use RETURNING,
    /// OUTPUT, or a read-then-write pair depending on provider capability.
    /// </summary>
    public class AdoLockHolderMethods : ILockHolderMethods
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
        public AdoLockHolderMethods(AdoDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<AcquireOutcome> TryAcquireAsync(AcquireRequest request, int defaultLeaseMs, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Providers that serialize acquirers with row/range locks (MySQL, SQL Server, and PostgreSQL
            // under contention) can raise a transient deadlock or serialization failure; the correct
            // response is to roll back and retry the whole transaction.
            return await _Driver.RetryOnConflictAsync(t => AcquireOnceAsync(request, defaultLeaseMs, t), token).ConfigureAwait(false);
        }

        private async Task<AcquireOutcome> AcquireOnceAsync(AcquireRequest request, int defaultLeaseMs, CancellationToken token)
        {
            AcquireOutcome outcome = new AcquireOutcome();

            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbTransaction transaction = await _Driver.BeginTransactionAsync(connection, true, token).ConfigureAwait(false))
            {
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

                DateTime now = DateTime.UtcNow;
                List<LockHolder> expired = await DeleteExpiredForKeyAsync(connection, transaction, request.TenantId, request.LockKey, now, token).ConfigureAwait(false);
                foreach (LockHolder dead in expired)
                {
                    await InsertAuditAsync(connection, transaction,
                        BuildAuditFromHolder(dead, LockEventTypeEnum.Expired, request.NodeId, "Lease expired; reclaimed during acquire."), token).ConfigureAwait(false);
                }

                HolderCounts counts = await CountHoldersAsync(connection, transaction, request.TenantId, request.LockKey, token).ConfigureAwait(false);

                CompatibilityResult compatibility = LockCompatibilityEvaluator.Evaluate(definition, counts, request.Mode);
                if (!compatibility.Compatible)
                {
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    outcome.Result = AcquireResultEnum.Incompatible;
                    outcome.Definition = definition;
                    outcome.Reason = compatibility.Reason;
                    return outcome;
                }

                long fencing = await IncrementFencingAsync(connection, transaction, definition.Id, now, token).ConfigureAwait(false);
                definition.FencingCounter = fencing;

                int effectiveDefault = definition.DefaultLeaseMs > 0 ? definition.DefaultLeaseMs : defaultLeaseMs;
                int leaseMs = request.RequestedLeaseMs.HasValue && request.RequestedLeaseMs.Value > 0
                    ? request.RequestedLeaseMs.Value
                    : effectiveDefault;
                leaseMs = Math.Clamp(leaseMs, 1000, definition.MaxLeaseMs);
                DateTime leaseExpires = now.AddMilliseconds(leaseMs);

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

        /// <inheritdoc />
        public async Task<bool> ReleaseAsync(string tenantId, string holderId, string sessionId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(holderId)) throw new ArgumentNullException(nameof(holderId));

            return await _Driver.RetryOnConflictAsync(t => ReleaseOnceAsync(tenantId, holderId, sessionId, t), token).ConfigureAwait(false);
        }

        private async Task<bool> ReleaseOnceAsync(string tenantId, string holderId, string sessionId, CancellationToken token)
        {
            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbTransaction transaction = await _Driver.BeginTransactionAsync(connection, false, token).ConfigureAwait(false))
            {
                List<LockHolder> deleted = await DeleteReturningAsync(connection, transaction,
                    "WHERE tenantid = @tid AND id = @id AND sessionid = @sid",
                    command =>
                    {
                        AdoDatabaseDriver.Add(command, "tid", tenantId);
                        AdoDatabaseDriver.Add(command, "id", holderId);
                        AdoDatabaseDriver.Add(command, "sid", sessionId);
                    }, token).ConfigureAwait(false);

                foreach (LockHolder holder in deleted)
                {
                    await InsertAuditAsync(connection, transaction, BuildAuditFromHolder(holder, LockEventTypeEnum.Released, holder.NodeId, "Released by owner."), token).ConfigureAwait(false);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                return deleted.Count > 0;
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
            List<string> idParams = new List<string>();
            string inList = BuildInList("hid", ids.Count, idParams);

            // Read the targeted holders joined with their definition lease bounds, then compute the new lease
            // in C# and update each row. This avoids provider-specific interval arithmetic and RETURNING.
            string selectSql =
                "SELECT h.*, d.defaultleasems AS d_defaultleasems, d.maxholdms AS d_maxholdms " +
                "FROM " + _Driver.Catalog.LockHolders + " h JOIN " + _Driver.Catalog.LockDefinitions + " d ON h.lockdefinitionid = d.id " +
                "WHERE h.sessionid = @sid AND h.id IN (" + inList + ");";

            List<LockHolder> holders = new List<LockHolder>();
            List<int> defaultLeases = new List<int>();
            List<int> maxHolds = new List<int>();

            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = selectSql;
                    AdoDatabaseDriver.Add(command, "sid", sessionId);
                    for (int i = 0; i < ids.Count; i++) AdoDatabaseDriver.Add(command, idParams[i], ids[i]);
                    await using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            holders.Add(AdoConverters.ToLockHolder(reader));
                            defaultLeases.Add(AdoConverters.Int(reader, "d_defaultleasems"));
                            maxHolds.Add(AdoConverters.Int(reader, "d_maxholdms"));
                        }
                    }
                }

                for (int i = 0; i < holders.Count; i++)
                {
                    LockHolder holder = holders[i];
                    DateTime byDefault = now.AddMilliseconds(defaultLeases[i]);
                    DateTime byMaxHold = holder.AcquiredUtc.AddMilliseconds(maxHolds[i]);
                    DateTime newLease = byDefault < byMaxHold ? byDefault : byMaxHold;
                    holder.LeaseExpiresUtc = newLease;
                    holder.LastHeartbeatUtc = now;
                    holder.LastUpdateUtc = now;

                    await using (DbCommand update = connection.CreateCommand())
                    {
                        update.CommandText = "UPDATE " + _Driver.Catalog.LockHolders + " SET leaseexpiresutc = @lease, lastheartbeatutc = @now, lastupdateutc = @now WHERE id = @id;";
                        AdoDatabaseDriver.Add(update, "lease", newLease);
                        AdoDatabaseDriver.Add(update, "now", now);
                        AdoDatabaseDriver.Add(update, "id", holder.Id);
                        await update.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }
            }

            return holders;
        }

        /// <inheritdoc />
        public async Task<List<string>> ReleaseAllForSessionAsync(string sessionId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));

            return await _Driver.RetryOnConflictAsync(t => ReleaseAllForSessionOnceAsync(sessionId, t), token).ConfigureAwait(false);
        }

        private async Task<List<string>> ReleaseAllForSessionOnceAsync(string sessionId, CancellationToken token)
        {
            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbTransaction transaction = await _Driver.BeginTransactionAsync(connection, false, token).ConfigureAwait(false))
            {
                List<LockHolder> deleted = await DeleteReturningAsync(connection, transaction,
                    "WHERE sessionid = @sid",
                    command => AdoDatabaseDriver.Add(command, "sid", sessionId), token).ConfigureAwait(false);

                HashSet<string> keys = new HashSet<string>();
                foreach (LockHolder holder in deleted)
                {
                    await InsertAuditAsync(connection, transaction, BuildAuditFromHolder(holder, LockEventTypeEnum.Released, holder.NodeId, "Session closed."), token).ConfigureAwait(false);
                    keys.Add(holder.TenantId + "|" + holder.LockKey);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                return new List<string>(keys);
            }
        }

        /// <inheritdoc />
        public async Task<LockHolder?> RevokeAsync(string tenantId, string holderId, string reason, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(holderId)) throw new ArgumentNullException(nameof(holderId));

            return await _Driver.RetryOnConflictAsync(t => RevokeOnceAsync(tenantId, holderId, reason, t), token).ConfigureAwait(false);
        }

        private async Task<LockHolder?> RevokeOnceAsync(string tenantId, string holderId, string reason, CancellationToken token)
        {
            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbTransaction transaction = await _Driver.BeginTransactionAsync(connection, false, token).ConfigureAwait(false))
            {
                List<LockHolder> deleted = await DeleteReturningAsync(connection, transaction,
                    "WHERE tenantid = @tid AND id = @id",
                    command =>
                    {
                        AdoDatabaseDriver.Add(command, "tid", tenantId);
                        AdoDatabaseDriver.Add(command, "id", holderId);
                    }, token).ConfigureAwait(false);

                LockHolder? result = deleted.Count > 0 ? deleted[0] : null;
                if (result != null)
                {
                    await InsertAuditAsync(connection, transaction, BuildAuditFromHolder(result, LockEventTypeEnum.Revoked, result.NodeId, string.IsNullOrEmpty(reason) ? "Revoked." : reason), token).ConfigureAwait(false);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                return result;
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> PurgeExpiredAsync(DateTime olderThanUtc, string nodeId, CancellationToken token = default)
        {
            return await _Driver.RetryOnConflictAsync(t => PurgeExpiredOnceAsync(olderThanUtc, nodeId, t), token).ConfigureAwait(false);
        }

        private async Task<List<string>> PurgeExpiredOnceAsync(DateTime olderThanUtc, string nodeId, CancellationToken token)
        {
            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbTransaction transaction = await _Driver.BeginTransactionAsync(connection, false, token).ConfigureAwait(false))
            {
                List<LockHolder> deleted = await DeleteReturningAsync(connection, transaction,
                    "WHERE leaseexpiresutc < @cutoff",
                    command => AdoDatabaseDriver.Add(command, "cutoff", olderThanUtc), token).ConfigureAwait(false);

                HashSet<string> keys = new HashSet<string>();
                foreach (LockHolder holder in deleted)
                {
                    await InsertAuditAsync(connection, transaction, BuildAuditFromHolder(holder, LockEventTypeEnum.Expired, nodeId, "Lease expired; reclaimed by sweep."), token).ConfigureAwait(false);
                    keys.Add(holder.TenantId + "|" + holder.LockKey);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                return new List<string>(keys);
            }
        }

        /// <inheritdoc />
        public async Task<LockHolder?> ReadAsync(string tenantId, string holderId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(holderId)) throw new ArgumentNullException(nameof(holderId));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM " + _Driver.Catalog.LockHolders + " WHERE tenantid = @tid AND id = @id;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "id", holderId);
                },
                AdoConverters.ToLockHolder,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<LockHolder>> EnumerateByTenantAsync(string tenantId, string? lockKeyContains, LockModeEnum? mode, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            string sql = "SELECT * FROM " + _Driver.Catalog.LockHolders + " WHERE tenantid = @tid";
            if (!string.IsNullOrEmpty(lockKeyContains)) sql += " AND " + _Driver.Dialect.CaseInsensitiveLike("lockkey", "keyfilter");
            if (mode.HasValue) sql += " AND mode = @mode";
            sql += " ORDER BY acquiredutc DESC;";

            return await _Driver.QueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "tid", tenantId);
                if (!string.IsNullOrEmpty(lockKeyContains)) AdoDatabaseDriver.Add(command, "keyfilter", "%" + lockKeyContains + "%");
                if (mode.HasValue) AdoDatabaseDriver.Add(command, "mode", mode.Value.ToString());
            }, AdoConverters.ToLockHolder, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<LockHolder>> EnumerateByTenantAsync(string tenantId, string? lockKeyContains, LockModeEnum? mode, EnumerationQuery query, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            query ??= new EnumerationQuery();

            string where = " WHERE tenantid = @tid";
            if (!string.IsNullOrEmpty(lockKeyContains)) where += " AND " + _Driver.Dialect.CaseInsensitiveLike("lockkey", "keyfilter");
            if (mode.HasValue) where += " AND mode = @mode";

            Action<DbCommand> bindFilters = command =>
            {
                AdoDatabaseDriver.Add(command, "tid", tenantId);
                if (!string.IsNullOrEmpty(lockKeyContains)) AdoDatabaseDriver.Add(command, "keyfilter", "%" + lockKeyContains + "%");
                if (mode.HasValue) AdoDatabaseDriver.Add(command, "mode", mode.Value.ToString());
            };

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM " + _Driver.Catalog.LockHolders + where + ";", bindFilters, token).ConfigureAwait(false);
            long total = countResult == null ? 0 : Convert.ToInt64(countResult);

            string sql = "SELECT * FROM " + _Driver.Catalog.LockHolders + where + AdoEnumerationSql.OrderClause(query, "acquiredutc", "lockkey") + _Driver.Dialect.LimitOffsetClause() + ";";
            List<LockHolder> objects = await _Driver.QueryAsync(sql, command =>
            {
                bindFilters(command);
                AdoDatabaseDriver.Add(command, "skip", query.Skip);
                AdoDatabaseDriver.Add(command, "max", query.MaxResults);
            }, AdoConverters.ToLockHolder, token).ConfigureAwait(false);

            return EnumerationResult<LockHolder>.Build(query, total, objects);
        }

        /// <inheritdoc />
        public async Task<List<LockHolder>> EnumerateByKeyAsync(string tenantId, string lockKey, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(lockKey)) throw new ArgumentNullException(nameof(lockKey));

            return await _Driver.QueryAsync(
                "SELECT * FROM " + _Driver.Catalog.LockHolders + " WHERE tenantid = @tid AND lockkey = @key ORDER BY acquiredutc ASC;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "key", lockKey);
                },
                AdoConverters.ToLockHolder,
                token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private async Task<LockDefinition?> ReadDefinitionForUpdateAsync(DbConnection connection, DbTransaction transaction, string tenantId, string lockKey, CancellationToken token)
        {
            await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, _Driver.Dialect.DefinitionLockSelect(_Driver.Catalog.LockDefinitions) + ";"))
            {
                AdoDatabaseDriver.Add(command, "tid", tenantId);
                AdoDatabaseDriver.Add(command, "key", lockKey);
                await using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(token).ConfigureAwait(false)) return AdoConverters.ToLockDefinition(reader);
                }
            }
            return null;
        }

        private async Task InsertDefinitionAsync(DbConnection connection, DbTransaction transaction, LockDefinition definition, CancellationToken token)
        {
            string sql =
                "INSERT INTO " + _Driver.Catalog.LockDefinitions + " (id, tenantid, lockkey, readmaxholders, writeexclusivity, writemaxholders, writeblocksreads, defaultleasems, maxleasems, maxholdms, fencingcounter, firstacquiredbycredentialid, active, createdutc, lastupdateutc) " +
                "VALUES (@id, @tid, @key, @readmax, @wexcl, @wmax, @wblocks, @deflease, @maxlease, @maxhold, 0, @firstby, @active, @created, @updated);";
            await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, sql))
            {
                AdoDatabaseDriver.Add(command, "id", definition.Id);
                AdoDatabaseDriver.Add(command, "tid", definition.TenantId);
                AdoDatabaseDriver.Add(command, "key", definition.LockKey);
                AdoDatabaseDriver.Add(command, "readmax", definition.ReadMaxHolders);
                AdoDatabaseDriver.Add(command, "wexcl", definition.WriteExclusivity.ToString());
                AdoDatabaseDriver.Add(command, "wmax", definition.WriteMaxHolders);
                AdoDatabaseDriver.Add(command, "wblocks", definition.WriteBlocksReads);
                AdoDatabaseDriver.Add(command, "deflease", definition.DefaultLeaseMs);
                AdoDatabaseDriver.Add(command, "maxlease", definition.MaxLeaseMs);
                AdoDatabaseDriver.Add(command, "maxhold", definition.MaxHoldMs);
                AdoDatabaseDriver.Add(command, "firstby", definition.FirstAcquiredByCredentialId);
                AdoDatabaseDriver.Add(command, "active", true);
                AdoDatabaseDriver.Add(command, "created", definition.CreatedUtc);
                AdoDatabaseDriver.Add(command, "updated", definition.LastUpdateUtc);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private async Task<List<LockHolder>> DeleteExpiredForKeyAsync(DbConnection connection, DbTransaction transaction, string tenantId, string lockKey, DateTime now, CancellationToken token)
        {
            return await DeleteReturningAsync(connection, transaction,
                "WHERE tenantid = @tid AND lockkey = @key AND leaseexpiresutc < @now",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "key", lockKey);
                    AdoDatabaseDriver.Add(command, "now", now);
                }, token).ConfigureAwait(false);
        }

        private async Task<HolderCounts> CountHoldersAsync(DbConnection connection, DbTransaction transaction, string tenantId, string lockKey, CancellationToken token)
        {
            HolderCounts counts = new HolderCounts();
            await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, "SELECT mode, COUNT(*) AS cnt FROM " + _Driver.Catalog.LockHolders + " WHERE tenantid = @tid AND lockkey = @key GROUP BY mode;"))
            {
                AdoDatabaseDriver.Add(command, "tid", tenantId);
                AdoDatabaseDriver.Add(command, "key", lockKey);
                await using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string mode = reader.GetString(0);
                        int count = Convert.ToInt32(reader.GetValue(1));
                        if (mode == LockModeEnum.Read.ToString()) counts.Read = count;
                        else if (mode == LockModeEnum.Write.ToString()) counts.Write = count;
                        else if (mode == LockModeEnum.Delete.ToString()) counts.Delete = count;
                    }
                }
            }
            return counts;
        }

        private async Task<long> IncrementFencingAsync(DbConnection connection, DbTransaction transaction, string definitionId, DateTime now, CancellationToken token)
        {
            if (_Driver.Dialect.SupportsReturning)
            {
                return await ScalarInTxAsync(connection, transaction,
                    "UPDATE " + _Driver.Catalog.LockDefinitions + " SET fencingcounter = fencingcounter + 1, lastupdateutc = @now WHERE id = @id RETURNING fencingcounter;",
                    definitionId, now, token).ConfigureAwait(false);
            }
            if (_Driver.Dialect.SupportsOutput)
            {
                return await ScalarInTxAsync(connection, transaction,
                    "UPDATE " + _Driver.Catalog.LockDefinitions + " SET fencingcounter = fencingcounter + 1, lastupdateutc = @now OUTPUT INSERTED.fencingcounter WHERE id = @id;",
                    definitionId, now, token).ConfigureAwait(false);
            }

            await using (DbCommand update = AdoDatabaseDriver.NewCommand(connection, transaction,
                "UPDATE " + _Driver.Catalog.LockDefinitions + " SET fencingcounter = fencingcounter + 1, lastupdateutc = @now WHERE id = @id;"))
            {
                AdoDatabaseDriver.Add(update, "id", definitionId);
                AdoDatabaseDriver.Add(update, "now", now);
                await update.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await using (DbCommand read = AdoDatabaseDriver.NewCommand(connection, transaction,
                "SELECT fencingcounter FROM " + _Driver.Catalog.LockDefinitions + " WHERE id = @id;"))
            {
                AdoDatabaseDriver.Add(read, "id", definitionId);
                object? result = await read.ExecuteScalarAsync(token).ConfigureAwait(false);
                return result == null ? 0 : Convert.ToInt64(result);
            }
        }

        private static async Task<long> ScalarInTxAsync(DbConnection connection, DbTransaction transaction, string sql, string definitionId, DateTime now, CancellationToken token)
        {
            await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, sql))
            {
                AdoDatabaseDriver.Add(command, "id", definitionId);
                AdoDatabaseDriver.Add(command, "now", now);
                object? result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return result == null ? 0 : Convert.ToInt64(result);
            }
        }

        private async Task InsertHolderAsync(DbConnection connection, DbTransaction transaction, LockHolder holder, CancellationToken token)
        {
            string sql =
                "INSERT INTO " + _Driver.Catalog.LockHolders + " (id, tenantid, lockkey, lockdefinitionid, mode, credentialid, sessionid, nodeid, fencingtoken, acquiredutc, leaseexpiresutc, lastheartbeatutc, active, createdutc, lastupdateutc) " +
                "VALUES (@id, @tid, @key, @defid, @mode, @cid, @sid, @node, @fence, @acq, @lease, @hb, @active, @created, @updated);";
            await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, sql))
            {
                AdoDatabaseDriver.Add(command, "id", holder.Id);
                AdoDatabaseDriver.Add(command, "tid", holder.TenantId);
                AdoDatabaseDriver.Add(command, "key", holder.LockKey);
                AdoDatabaseDriver.Add(command, "defid", holder.LockDefinitionId);
                AdoDatabaseDriver.Add(command, "mode", holder.Mode.ToString());
                AdoDatabaseDriver.Add(command, "cid", holder.CredentialId);
                AdoDatabaseDriver.Add(command, "sid", holder.SessionId);
                AdoDatabaseDriver.Add(command, "node", holder.NodeId);
                AdoDatabaseDriver.Add(command, "fence", holder.FencingToken);
                AdoDatabaseDriver.Add(command, "acq", holder.AcquiredUtc);
                AdoDatabaseDriver.Add(command, "lease", holder.LeaseExpiresUtc);
                AdoDatabaseDriver.Add(command, "hb", holder.LastHeartbeatUtc);
                AdoDatabaseDriver.Add(command, "active", true);
                AdoDatabaseDriver.Add(command, "created", holder.CreatedUtc);
                AdoDatabaseDriver.Add(command, "updated", holder.LastUpdateUtc);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private async Task<List<LockHolder>> DeleteReturningAsync(DbConnection connection, DbTransaction transaction, string whereClause, Action<DbCommand> bind, CancellationToken token)
        {
            string table = _Driver.Catalog.LockHolders;
            List<LockHolder> holders = new List<LockHolder>();

            if (_Driver.Dialect.SupportsReturning)
            {
                await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + table + " " + whereClause + " RETURNING *;"))
                {
                    bind(command);
                    await using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) holders.Add(AdoConverters.ToLockHolder(reader));
                    }
                }
                return holders;
            }

            if (_Driver.Dialect.SupportsOutput)
            {
                await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + table + " OUTPUT DELETED.* " + whereClause + ";"))
                {
                    bind(command);
                    await using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) holders.Add(AdoConverters.ToLockHolder(reader));
                    }
                }
                return holders;
            }

            // No RETURNING/OUTPUT (MySQL): read the rows under the transaction, then delete them.
            await using (DbCommand select = AdoDatabaseDriver.NewCommand(connection, transaction, "SELECT * FROM " + table + " " + whereClause + ";"))
            {
                bind(select);
                await using (DbDataReader reader = await select.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) holders.Add(AdoConverters.ToLockHolder(reader));
                }
            }
            await using (DbCommand delete = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + table + " " + whereClause + ";"))
            {
                bind(delete);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            return holders;
        }

        private async Task InsertAuditAsync(DbConnection connection, DbTransaction transaction, LockAuditEntry entry, CancellationToken token)
        {
            string sql =
                "INSERT INTO " + _Driver.Catalog.LockAudit + " (id, tenantid, lockkey, mode, eventtype, credentialid, sessionid, nodeid, fencingtoken, reason, createdutc) " +
                "VALUES (@id, @tid, @key, @mode, @event, @cid, @sid, @node, @fence, @reason, @created);";
            await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, sql))
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
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static string BuildInList(string prefix, int count, List<string> names)
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

namespace Clutch.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Models;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of tenant data access.
    /// </summary>
    public class TenantMethods : ITenantMethods
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
        public TenantMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Tenant> CreateAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));

            tenant.CreatedUtc = DateTime.UtcNow;
            tenant.LastUpdateUtc = tenant.CreatedUtc;

            string sql = @"
INSERT INTO tenants (id, name, lockhistoryretentiondays, defaultleasems, maxleasems, active, isprotected, createdutc, lastupdateutc)
VALUES (@id, @name, @retention, @defaultlease, @maxlease, @active, @protected, @created, @updated);";

            await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", tenant.Id);
                parameters.AddWithValue("name", tenant.Name);
                parameters.AddWithValue("retention", tenant.LockHistoryRetentionDays);
                parameters.AddWithValue("defaultlease", tenant.DefaultLeaseMs);
                parameters.AddWithValue("maxlease", tenant.MaxLeaseMs);
                parameters.AddWithValue("active", tenant.Active);
                parameters.AddWithValue("protected", tenant.IsProtected);
                parameters.AddWithValue("created", tenant.CreatedUtc);
                parameters.AddWithValue("updated", tenant.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return tenant;
        }

        /// <inheritdoc />
        public async Task<Tenant?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM tenants WHERE id = @id;",
                parameters => parameters.AddWithValue("id", id),
                Converters.ToTenant,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Tenant?> ReadByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM tenants WHERE name = @name;",
                parameters => parameters.AddWithValue("name", name),
                Converters.ToTenant,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Tenant>> EnumerateAsync(CancellationToken token = default)
        {
            return await _Driver.QueryAsync(
                "SELECT * FROM tenants ORDER BY createdutc ASC;",
                null,
                Converters.ToTenant,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Tenant>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            query ??= new EnumerationQuery();

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM tenants;", null, token).ConfigureAwait(false);
            long total = countResult == null ? 0 : (long)countResult;

            string sql = "SELECT * FROM tenants" + EnumerationSql.OrderClause(query, "createdutc", "name") + " OFFSET @skip LIMIT @max;";
            List<Tenant> objects = await _Driver.QueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("skip", query.Skip);
                parameters.AddWithValue("max", query.MaxResults);
            }, Converters.ToTenant, token).ConfigureAwait(false);

            return EnumerationResult<Tenant>.Build(query, total, objects);
        }

        /// <inheritdoc />
        public async Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));

            tenant.LastUpdateUtc = DateTime.UtcNow;

            string sql = @"
UPDATE tenants SET
    name = @name,
    lockhistoryretentiondays = @retention,
    defaultleasems = @defaultlease,
    maxleasems = @maxlease,
    active = @active,
    isprotected = @protected,
    lastupdateutc = @updated
WHERE id = @id;";

            await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", tenant.Id);
                parameters.AddWithValue("name", tenant.Name);
                parameters.AddWithValue("retention", tenant.LockHistoryRetentionDays);
                parameters.AddWithValue("defaultlease", tenant.DefaultLeaseMs);
                parameters.AddWithValue("maxlease", tenant.MaxLeaseMs);
                parameters.AddWithValue("active", tenant.Active);
                parameters.AddWithValue("protected", tenant.IsProtected);
                parameters.AddWithValue("updated", tenant.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return tenant;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            await using (NpgsqlConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    string[] childTables =
                    {
                        "lock_holders", "lock_definitions", "lock_audit", "auth_sessions", "credentials", "users", "request_history"
                    };

                    foreach (string table in childTables)
                    {
                        await using (NpgsqlCommand command = new NpgsqlCommand("DELETE FROM " + table + " WHERE tenantid = @tid;", connection, transaction))
                        {
                            command.Parameters.AddWithValue("tid", id);
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    int affected;
                    await using (NpgsqlCommand command = new NpgsqlCommand("DELETE FROM tenants WHERE id = @id;", connection, transaction))
                    {
                        command.Parameters.AddWithValue("id", id);
                        affected = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return affected > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, long>> NukeAsync(string id, bool includeAuditRecords, bool includeRequestHistory, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            // Ordered leaf-first cascade. Each entry maps a physical table to the friendly count key
            // surfaced to the dashboard. Audit and request history are gated by the caller's toggles.
            List<KeyValuePair<string, string>> targets = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("lock_holders", "lockHolders"),
                new KeyValuePair<string, string>("lock_definitions", "lockDefinitions"),
                new KeyValuePair<string, string>("auth_sessions", "authSessions"),
                new KeyValuePair<string, string>("credentials", "credentials"),
                new KeyValuePair<string, string>("users", "users")
            };
            if (includeAuditRecords) targets.Insert(2, new KeyValuePair<string, string>("lock_audit", "lockAudit"));
            if (includeRequestHistory) targets.Add(new KeyValuePair<string, string>("request_history", "requestHistory"));

            Dictionary<string, long> counts = new Dictionary<string, long>();

            await using (NpgsqlConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    foreach (KeyValuePair<string, string> target in targets)
                    {
                        await using (NpgsqlCommand command = new NpgsqlCommand("DELETE FROM " + target.Key + " WHERE tenantid = @tid;", connection, transaction))
                        {
                            command.Parameters.AddWithValue("tid", id);
                            int removed = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            counts[target.Value] = removed;
                        }
                    }

                    await using (NpgsqlCommand command = new NpgsqlCommand("DELETE FROM tenants WHERE id = @id;", connection, transaction))
                    {
                        command.Parameters.AddWithValue("id", id);
                        int removed = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        counts["tenant"] = removed;
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return counts;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            object? result = await _Driver.ScalarAsync(
                "SELECT 1 FROM tenants WHERE id = @id LIMIT 1;",
                parameters => parameters.AddWithValue("id", id),
                token).ConfigureAwait(false);
            return result != null;
        }

        #endregion
    }
}

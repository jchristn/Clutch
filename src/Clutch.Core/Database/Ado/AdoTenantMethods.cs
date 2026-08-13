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

    /// <summary>
    /// Provider-neutral tenant data access.
    /// </summary>
    public class AdoTenantMethods : ITenantMethods
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
        public AdoTenantMethods(AdoDatabaseDriver driver)
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

            string sql =
                "INSERT INTO " + _Driver.Catalog.Tenants + " (id, name, lockhistoryretentiondays, defaultleasems, maxleasems, active, isprotected, createdutc, lastupdateutc) " +
                "VALUES (@id, @name, @retention, @defaultlease, @maxlease, @active, @protected, @created, @updated);";

            await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", tenant.Id);
                AdoDatabaseDriver.Add(command, "name", tenant.Name);
                AdoDatabaseDriver.Add(command, "retention", tenant.LockHistoryRetentionDays);
                AdoDatabaseDriver.Add(command, "defaultlease", tenant.DefaultLeaseMs);
                AdoDatabaseDriver.Add(command, "maxlease", tenant.MaxLeaseMs);
                AdoDatabaseDriver.Add(command, "active", tenant.Active);
                AdoDatabaseDriver.Add(command, "protected", tenant.IsProtected);
                AdoDatabaseDriver.Add(command, "created", tenant.CreatedUtc);
                AdoDatabaseDriver.Add(command, "updated", tenant.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return tenant;
        }

        /// <inheritdoc />
        public async Task<Tenant?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM " + _Driver.Catalog.Tenants + " WHERE id = @id;",
                command => AdoDatabaseDriver.Add(command, "id", id),
                AdoConverters.ToTenant,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Tenant?> ReadByNameAsync(string name, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM " + _Driver.Catalog.Tenants + " WHERE name = @name;",
                command => AdoDatabaseDriver.Add(command, "name", name),
                AdoConverters.ToTenant,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Tenant>> EnumerateAsync(CancellationToken token = default)
        {
            return await _Driver.QueryAsync(
                "SELECT * FROM " + _Driver.Catalog.Tenants + " ORDER BY createdutc ASC;",
                null,
                AdoConverters.ToTenant,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Tenant>> EnumerateAsync(EnumerationQuery query, CancellationToken token = default)
        {
            query ??= new EnumerationQuery();

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM " + _Driver.Catalog.Tenants + ";", null, token).ConfigureAwait(false);
            long total = countResult == null ? 0 : Convert.ToInt64(countResult);

            string sql = "SELECT * FROM " + _Driver.Catalog.Tenants + AdoEnumerationSql.OrderClause(query, "createdutc", "name") + _Driver.Dialect.LimitOffsetClause() + ";";
            List<Tenant> objects = await _Driver.QueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "skip", query.Skip);
                AdoDatabaseDriver.Add(command, "max", query.MaxResults);
            }, AdoConverters.ToTenant, token).ConfigureAwait(false);

            return EnumerationResult<Tenant>.Build(query, total, objects);
        }

        /// <inheritdoc />
        public async Task<Tenant> UpdateAsync(Tenant tenant, CancellationToken token = default)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));

            tenant.LastUpdateUtc = DateTime.UtcNow;

            string sql =
                "UPDATE " + _Driver.Catalog.Tenants + " SET " +
                "name = @name, lockhistoryretentiondays = @retention, defaultleasems = @defaultlease, " +
                "maxleasems = @maxlease, active = @active, isprotected = @protected, lastupdateutc = @updated " +
                "WHERE id = @id;";

            await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", tenant.Id);
                AdoDatabaseDriver.Add(command, "name", tenant.Name);
                AdoDatabaseDriver.Add(command, "retention", tenant.LockHistoryRetentionDays);
                AdoDatabaseDriver.Add(command, "defaultlease", tenant.DefaultLeaseMs);
                AdoDatabaseDriver.Add(command, "maxlease", tenant.MaxLeaseMs);
                AdoDatabaseDriver.Add(command, "active", tenant.Active);
                AdoDatabaseDriver.Add(command, "protected", tenant.IsProtected);
                AdoDatabaseDriver.Add(command, "updated", tenant.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return tenant;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            string[] childTables =
            {
                _Driver.Catalog.LockHolders, _Driver.Catalog.LockDefinitions, _Driver.Catalog.LockAudit,
                _Driver.Catalog.AuthSessions, _Driver.Catalog.Credentials, _Driver.Catalog.Users, _Driver.Catalog.RequestHistory
            };

            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbTransaction transaction = await _Driver.BeginTransactionAsync(connection, false, token).ConfigureAwait(false))
            {
                foreach (string table in childTables)
                {
                    await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + table + " WHERE tenantid = @tid;"))
                    {
                        AdoDatabaseDriver.Add(command, "tid", id);
                        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }

                int affected;
                await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + _Driver.Catalog.Tenants + " WHERE id = @id;"))
                {
                    AdoDatabaseDriver.Add(command, "id", id);
                    affected = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                return affected > 0;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, long>> NukeAsync(string id, bool includeAuditRecords, bool includeRequestHistory, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            List<KeyValuePair<string, string>> targets = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(_Driver.Catalog.LockHolders, "lockHolders"),
                new KeyValuePair<string, string>(_Driver.Catalog.LockDefinitions, "lockDefinitions"),
                new KeyValuePair<string, string>(_Driver.Catalog.AuthSessions, "authSessions"),
                new KeyValuePair<string, string>(_Driver.Catalog.Credentials, "credentials"),
                new KeyValuePair<string, string>(_Driver.Catalog.Users, "users")
            };
            if (includeAuditRecords) targets.Insert(2, new KeyValuePair<string, string>(_Driver.Catalog.LockAudit, "lockAudit"));
            if (includeRequestHistory) targets.Add(new KeyValuePair<string, string>(_Driver.Catalog.RequestHistory, "requestHistory"));

            Dictionary<string, long> counts = new Dictionary<string, long>();

            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbTransaction transaction = await _Driver.BeginTransactionAsync(connection, false, token).ConfigureAwait(false))
            {
                foreach (KeyValuePair<string, string> target in targets)
                {
                    await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + target.Key + " WHERE tenantid = @tid;"))
                    {
                        AdoDatabaseDriver.Add(command, "tid", id);
                        int removed = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        counts[target.Value] = removed;
                    }
                }

                await using (DbCommand tenantCommand = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + _Driver.Catalog.Tenants + " WHERE id = @id;"))
                {
                    AdoDatabaseDriver.Add(tenantCommand, "id", id);
                    int removed = await tenantCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    counts["tenant"] = removed;
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
            }

            return counts;
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            object? result = await _Driver.ScalarAsync(
                _Driver.Dialect.ExistsSelect(_Driver.Catalog.Tenants, "WHERE id = @id") + ";",
                command => AdoDatabaseDriver.Add(command, "id", id),
                token).ConfigureAwait(false);
            return result != null;
        }

        #endregion
    }
}

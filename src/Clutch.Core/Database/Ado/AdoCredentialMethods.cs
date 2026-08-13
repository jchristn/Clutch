namespace Clutch.Core.Database.Ado
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Models;

    /// <summary>
    /// Provider-neutral credential (application key) data access.
    /// </summary>
    public class AdoCredentialMethods : ICredentialMethods
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
        public AdoCredentialMethods(AdoDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Credential> CreateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            credential.CreatedUtc = DateTime.UtcNow;
            credential.LastUpdateUtc = credential.CreatedUtc;

            string sql =
                "INSERT INTO " + _Driver.Catalog.Credentials + " (id, tenantid, userid, name, accesskey, authmode, lastusedutc, expiresutc, active, isprotected, createdutc, lastupdateutc) " +
                "VALUES (@id, @tid, @uid, @name, @access, @mode, @lastused, @expires, @active, @protected, @created, @updated);";

            await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", credential.Id);
                AdoDatabaseDriver.Add(command, "tid", credential.TenantId);
                AdoDatabaseDriver.Add(command, "uid", credential.UserId);
                AdoDatabaseDriver.Add(command, "name", credential.Name);
                AdoDatabaseDriver.Add(command, "access", credential.AccessKey);
                AdoDatabaseDriver.Add(command, "mode", credential.AuthMode.ToString());
                AdoDatabaseDriver.Add(command, "lastused", credential.LastUsedUtc);
                AdoDatabaseDriver.Add(command, "expires", credential.ExpiresUtc);
                AdoDatabaseDriver.Add(command, "active", credential.Active);
                AdoDatabaseDriver.Add(command, "protected", credential.IsProtected);
                AdoDatabaseDriver.Add(command, "created", credential.CreatedUtc);
                AdoDatabaseDriver.Add(command, "updated", credential.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return credential;
        }

        /// <inheritdoc />
        public async Task<Credential?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM " + _Driver.Catalog.Credentials + " WHERE tenantid = @tid AND id = @id;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "id", id);
                },
                AdoConverters.ToCredential,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Credential?> ReadByAccessKeyAsync(string accessKey, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM " + _Driver.Catalog.Credentials + " WHERE accesskey = @access;",
                command => AdoDatabaseDriver.Add(command, "access", accessKey),
                AdoConverters.ToCredential,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Credential>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            return await _Driver.QueryAsync(
                "SELECT * FROM " + _Driver.Catalog.Credentials + " WHERE tenantid = @tid ORDER BY createdutc ASC;",
                command => AdoDatabaseDriver.Add(command, "tid", tenantId),
                AdoConverters.ToCredential,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            query ??= new EnumerationQuery();

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM " + _Driver.Catalog.Credentials + " WHERE tenantid = @tid;",
                command => AdoDatabaseDriver.Add(command, "tid", tenantId), token).ConfigureAwait(false);
            long total = countResult == null ? 0 : Convert.ToInt64(countResult);

            string sql = "SELECT * FROM " + _Driver.Catalog.Credentials + " WHERE tenantid = @tid" + AdoEnumerationSql.OrderClause(query, "createdutc", "name") + _Driver.Dialect.LimitOffsetClause() + ";";
            List<Credential> objects = await _Driver.QueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "tid", tenantId);
                AdoDatabaseDriver.Add(command, "skip", query.Skip);
                AdoDatabaseDriver.Add(command, "max", query.MaxResults);
            }, AdoConverters.ToCredential, token).ConfigureAwait(false);

            return EnumerationResult<Credential>.Build(query, total, objects);
        }

        /// <inheritdoc />
        public async Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            credential.LastUpdateUtc = DateTime.UtcNow;

            string sql =
                "UPDATE " + _Driver.Catalog.Credentials + " SET " +
                "name = @name, authmode = @mode, expiresutc = @expires, active = @active, isprotected = @protected, lastupdateutc = @updated " +
                "WHERE tenantid = @tid AND id = @id;";

            await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", credential.Id);
                AdoDatabaseDriver.Add(command, "tid", credential.TenantId);
                AdoDatabaseDriver.Add(command, "name", credential.Name);
                AdoDatabaseDriver.Add(command, "mode", credential.AuthMode.ToString());
                AdoDatabaseDriver.Add(command, "expires", credential.ExpiresUtc);
                AdoDatabaseDriver.Add(command, "active", credential.Active);
                AdoDatabaseDriver.Add(command, "protected", credential.IsProtected);
                AdoDatabaseDriver.Add(command, "updated", credential.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return credential;
        }

        /// <inheritdoc />
        public async Task TouchLastUsedAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            await _Driver.NonQueryAsync(
                "UPDATE " + _Driver.Catalog.Credentials + " SET lastusedutc = @now WHERE id = @id;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "now", DateTime.UtcNow);
                    AdoDatabaseDriver.Add(command, "id", id);
                },
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            int affected = await _Driver.NonQueryAsync(
                "DELETE FROM " + _Driver.Catalog.Credentials + " WHERE tenantid = @tid AND id = @id;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "id", id);
                },
                token).ConfigureAwait(false);
            return affected > 0;
        }

        #endregion
    }
}

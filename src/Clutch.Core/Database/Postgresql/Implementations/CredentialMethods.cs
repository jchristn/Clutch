namespace Clutch.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Models;

    /// <summary>
    /// PostgreSQL implementation of credential (application key) data access.
    /// </summary>
    public class CredentialMethods : ICredentialMethods
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
        public CredentialMethods(PostgresqlDatabaseDriver driver)
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

            string sql = @"
INSERT INTO credentials (id, tenantid, userid, name, accesskey, secretkeyencrypted, secretkeylast4, authmode, lastusedutc, expiresutc, active, isprotected, createdutc, lastupdateutc)
VALUES (@id, @tid, @uid, @name, @access, @secret, @last4, @mode, @lastused, @expires, @active, @protected, @created, @updated);";

            await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", credential.Id);
                parameters.AddWithValue("tid", credential.TenantId);
                parameters.AddWithValue("uid", (object?)credential.UserId ?? DBNull.Value);
                parameters.AddWithValue("name", credential.Name);
                parameters.AddWithValue("access", credential.AccessKey);
                parameters.AddWithValue("secret", credential.SecretKeyEncrypted);
                parameters.AddWithValue("last4", credential.SecretKeyLast4);
                parameters.AddWithValue("mode", credential.AuthMode.ToString());
                parameters.AddWithValue("lastused", (object?)credential.LastUsedUtc ?? DBNull.Value);
                parameters.AddWithValue("expires", (object?)credential.ExpiresUtc ?? DBNull.Value);
                parameters.AddWithValue("active", credential.Active);
                parameters.AddWithValue("protected", credential.IsProtected);
                parameters.AddWithValue("created", credential.CreatedUtc);
                parameters.AddWithValue("updated", credential.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return credential;
        }

        /// <inheritdoc />
        public async Task<Credential?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM credentials WHERE tenantid = @tid AND id = @id;",
                parameters =>
                {
                    parameters.AddWithValue("tid", tenantId);
                    parameters.AddWithValue("id", id);
                },
                Converters.ToCredential,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Credential?> ReadByAccessKeyAsync(string accessKey, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM credentials WHERE accesskey = @access;",
                parameters => parameters.AddWithValue("access", accessKey),
                Converters.ToCredential,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<Credential>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            return await _Driver.QueryAsync(
                "SELECT * FROM credentials WHERE tenantid = @tid ORDER BY createdutc ASC;",
                parameters => parameters.AddWithValue("tid", tenantId),
                Converters.ToCredential,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default)
        {
            if (credential == null) throw new ArgumentNullException(nameof(credential));

            credential.LastUpdateUtc = DateTime.UtcNow;

            string sql = @"
UPDATE credentials SET
    name = @name,
    authmode = @mode,
    expiresutc = @expires,
    active = @active,
    isprotected = @protected,
    lastupdateutc = @updated
WHERE tenantid = @tid AND id = @id;";

            await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", credential.Id);
                parameters.AddWithValue("tid", credential.TenantId);
                parameters.AddWithValue("name", credential.Name);
                parameters.AddWithValue("mode", credential.AuthMode.ToString());
                parameters.AddWithValue("expires", (object?)credential.ExpiresUtc ?? DBNull.Value);
                parameters.AddWithValue("active", credential.Active);
                parameters.AddWithValue("protected", credential.IsProtected);
                parameters.AddWithValue("updated", credential.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return credential;
        }

        /// <inheritdoc />
        public async Task TouchLastUsedAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            await _Driver.NonQueryAsync(
                "UPDATE credentials SET lastusedutc = @now WHERE id = @id;",
                parameters =>
                {
                    parameters.AddWithValue("now", DateTime.UtcNow);
                    parameters.AddWithValue("id", id);
                },
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            int affected = await _Driver.NonQueryAsync(
                "DELETE FROM credentials WHERE tenantid = @tid AND id = @id;",
                parameters =>
                {
                    parameters.AddWithValue("tid", tenantId);
                    parameters.AddWithValue("id", id);
                },
                token).ConfigureAwait(false);
            return affected > 0;
        }

        #endregion
    }
}

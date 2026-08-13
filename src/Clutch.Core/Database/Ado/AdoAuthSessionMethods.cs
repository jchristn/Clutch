namespace Clutch.Core.Database.Ado
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Models;

    /// <summary>
    /// Provider-neutral authentication session data access.
    /// </summary>
    public class AdoAuthSessionMethods : IAuthSessionMethods
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
        public AdoAuthSessionMethods(AdoDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<AuthSession> CreateAsync(AuthSession session, CancellationToken token = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            session.CreatedUtc = DateTime.UtcNow;
            session.LastUpdateUtc = session.CreatedUtc;

            string sql =
                "INSERT INTO " + _Driver.Catalog.AuthSessions + " (id, tenantid, userid, credentialid, principaltype, tokenid, sourceip, useragent, expiresutc, lastusedutc, revokedutc, revocationreason, active, isprotected, createdutc, lastupdateutc) " +
                "VALUES (@id, @tid, @uid, @cid, @ptype, @tokenid, @ip, @ua, @expires, @lastused, @revoked, @reason, @active, @protected, @created, @updated);";

            await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", session.Id);
                AdoDatabaseDriver.Add(command, "tid", session.TenantId);
                AdoDatabaseDriver.Add(command, "uid", session.UserId);
                AdoDatabaseDriver.Add(command, "cid", session.CredentialId);
                AdoDatabaseDriver.Add(command, "ptype", session.PrincipalType.ToString());
                AdoDatabaseDriver.Add(command, "tokenid", session.TokenId);
                AdoDatabaseDriver.Add(command, "ip", session.SourceIp);
                AdoDatabaseDriver.Add(command, "ua", session.UserAgent);
                AdoDatabaseDriver.Add(command, "expires", session.ExpiresUtc);
                AdoDatabaseDriver.Add(command, "lastused", session.LastUsedUtc);
                AdoDatabaseDriver.Add(command, "revoked", session.RevokedUtc);
                AdoDatabaseDriver.Add(command, "reason", session.RevocationReason);
                AdoDatabaseDriver.Add(command, "active", session.Active);
                AdoDatabaseDriver.Add(command, "protected", session.IsProtected);
                AdoDatabaseDriver.Add(command, "created", session.CreatedUtc);
                AdoDatabaseDriver.Add(command, "updated", session.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return session;
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM " + _Driver.Catalog.AuthSessions + " WHERE id = @id;",
                command => AdoDatabaseDriver.Add(command, "id", id),
                AdoConverters.ToAuthSession,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadByTokenIdAsync(string tokenId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tokenId)) throw new ArgumentNullException(nameof(tokenId));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM " + _Driver.Catalog.AuthSessions + " WHERE tokenid = @tokenid;",
                command => AdoDatabaseDriver.Add(command, "tokenid", tokenId),
                AdoConverters.ToAuthSession,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> RevokeAsync(string id, string reason, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            int affected = await _Driver.NonQueryAsync(
                "UPDATE " + _Driver.Catalog.AuthSessions + " SET active = " + _Driver.Dialect.BooleanLiteral(false) + ", revokedutc = @now, revocationreason = @reason, lastupdateutc = @now WHERE id = @id;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "now", DateTime.UtcNow);
                    AdoDatabaseDriver.Add(command, "reason", reason);
                    AdoDatabaseDriver.Add(command, "id", id);
                },
                token).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task TouchLastUsedAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            await _Driver.NonQueryAsync(
                "UPDATE " + _Driver.Catalog.AuthSessions + " SET lastusedutc = @now WHERE id = @id;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "now", DateTime.UtcNow);
                    AdoDatabaseDriver.Add(command, "id", id);
                },
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> DeleteExpiredAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            return await _Driver.NonQueryAsync(
                "DELETE FROM " + _Driver.Catalog.AuthSessions + " WHERE expiresutc < @cutoff;",
                command => AdoDatabaseDriver.Add(command, "cutoff", olderThanUtc),
                token).ConfigureAwait(false);
        }

        #endregion
    }
}

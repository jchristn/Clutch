namespace Clutch.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Models;

    /// <summary>
    /// PostgreSQL implementation of authentication session data access.
    /// </summary>
    public class AuthSessionMethods : IAuthSessionMethods
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
        public AuthSessionMethods(PostgresqlDatabaseDriver driver)
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

            string sql = @"
INSERT INTO auth_sessions (id, tenantid, userid, credentialid, principaltype, tokenid, sourceip, useragent, expiresutc, lastusedutc, revokedutc, revocationreason, active, isprotected, createdutc, lastupdateutc)
VALUES (@id, @tid, @uid, @cid, @ptype, @tokenid, @ip, @ua, @expires, @lastused, @revoked, @reason, @active, @protected, @created, @updated);";

            await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", session.Id);
                parameters.AddWithValue("tid", session.TenantId);
                parameters.AddWithValue("uid", (object?)session.UserId ?? DBNull.Value);
                parameters.AddWithValue("cid", (object?)session.CredentialId ?? DBNull.Value);
                parameters.AddWithValue("ptype", session.PrincipalType.ToString());
                parameters.AddWithValue("tokenid", session.TokenId);
                parameters.AddWithValue("ip", (object?)session.SourceIp ?? DBNull.Value);
                parameters.AddWithValue("ua", (object?)session.UserAgent ?? DBNull.Value);
                parameters.AddWithValue("expires", session.ExpiresUtc);
                parameters.AddWithValue("lastused", (object?)session.LastUsedUtc ?? DBNull.Value);
                parameters.AddWithValue("revoked", (object?)session.RevokedUtc ?? DBNull.Value);
                parameters.AddWithValue("reason", (object?)session.RevocationReason ?? DBNull.Value);
                parameters.AddWithValue("active", session.Active);
                parameters.AddWithValue("protected", session.IsProtected);
                parameters.AddWithValue("created", session.CreatedUtc);
                parameters.AddWithValue("updated", session.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return session;
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM auth_sessions WHERE id = @id;",
                parameters => parameters.AddWithValue("id", id),
                Converters.ToAuthSession,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<AuthSession?> ReadByTokenIdAsync(string tokenId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tokenId)) throw new ArgumentNullException(nameof(tokenId));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM auth_sessions WHERE tokenid = @tokenid;",
                parameters => parameters.AddWithValue("tokenid", tokenId),
                Converters.ToAuthSession,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> RevokeAsync(string id, string reason, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            int affected = await _Driver.NonQueryAsync(
                "UPDATE auth_sessions SET active = FALSE, revokedutc = @now, revocationreason = @reason, lastupdateutc = @now WHERE id = @id;",
                parameters =>
                {
                    parameters.AddWithValue("now", DateTime.UtcNow);
                    parameters.AddWithValue("reason", (object?)reason ?? DBNull.Value);
                    parameters.AddWithValue("id", id);
                },
                token).ConfigureAwait(false);
            return affected > 0;
        }

        /// <inheritdoc />
        public async Task TouchLastUsedAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            await _Driver.NonQueryAsync(
                "UPDATE auth_sessions SET lastusedutc = @now WHERE id = @id;",
                parameters =>
                {
                    parameters.AddWithValue("now", DateTime.UtcNow);
                    parameters.AddWithValue("id", id);
                },
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<int> DeleteExpiredAsync(DateTime olderThanUtc, CancellationToken token = default)
        {
            return await _Driver.NonQueryAsync(
                "DELETE FROM auth_sessions WHERE expiresutc < @cutoff;",
                parameters => parameters.AddWithValue("cutoff", olderThanUtc),
                token).ConfigureAwait(false);
        }

        #endregion
    }
}

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
    /// Provider-neutral user data access.
    /// </summary>
    public class AdoUserMethods : IUserMethods
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
        public AdoUserMethods(AdoDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<User> CreateAsync(User user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.CreatedUtc = DateTime.UtcNow;
            user.LastUpdateUtc = user.CreatedUtc;

            string sql =
                "INSERT INTO " + _Driver.Catalog.Users + " (id, tenantid, firstname, lastname, email, passwordsha256, issystemadmin, istenantadmin, active, isprotected, createdutc, lastupdateutc) " +
                "VALUES (@id, @tid, @first, @last, @email, @pw, @sysadmin, @tenantadmin, @active, @protected, @created, @updated);";

            await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", user.Id);
                AdoDatabaseDriver.Add(command, "tid", user.TenantId);
                AdoDatabaseDriver.Add(command, "first", user.FirstName);
                AdoDatabaseDriver.Add(command, "last", user.LastName);
                AdoDatabaseDriver.Add(command, "email", user.Email);
                AdoDatabaseDriver.Add(command, "pw", user.PasswordSha256);
                AdoDatabaseDriver.Add(command, "sysadmin", user.IsSystemAdmin);
                AdoDatabaseDriver.Add(command, "tenantadmin", user.IsTenantAdmin);
                AdoDatabaseDriver.Add(command, "active", user.Active);
                AdoDatabaseDriver.Add(command, "protected", user.IsProtected);
                AdoDatabaseDriver.Add(command, "created", user.CreatedUtc);
                AdoDatabaseDriver.Add(command, "updated", user.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return user;
        }

        /// <inheritdoc />
        public async Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM " + _Driver.Catalog.Users + " WHERE tenantid = @tid AND id = @id;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "id", id);
                },
                AdoConverters.ToUser,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM " + _Driver.Catalog.Users + " WHERE tenantid = @tid AND email = @email;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "email", email);
                },
                AdoConverters.ToUser,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<User>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            return await _Driver.QueryAsync(
                "SELECT * FROM " + _Driver.Catalog.Users + " WHERE tenantid = @tid ORDER BY createdutc ASC;",
                command => AdoDatabaseDriver.Add(command, "tid", tenantId),
                AdoConverters.ToUser,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<User>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            query ??= new EnumerationQuery();

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM " + _Driver.Catalog.Users + " WHERE tenantid = @tid;",
                command => AdoDatabaseDriver.Add(command, "tid", tenantId), token).ConfigureAwait(false);
            long total = countResult == null ? 0 : Convert.ToInt64(countResult);

            string sql = "SELECT * FROM " + _Driver.Catalog.Users + " WHERE tenantid = @tid" + AdoEnumerationSql.OrderClause(query, "createdutc", "email") + _Driver.Dialect.LimitOffsetClause() + ";";
            List<User> objects = await _Driver.QueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "tid", tenantId);
                AdoDatabaseDriver.Add(command, "skip", query.Skip);
                AdoDatabaseDriver.Add(command, "max", query.MaxResults);
            }, AdoConverters.ToUser, token).ConfigureAwait(false);

            return EnumerationResult<User>.Build(query, total, objects);
        }

        /// <inheritdoc />
        public async Task<User> UpdateAsync(User user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.LastUpdateUtc = DateTime.UtcNow;

            string sql =
                "UPDATE " + _Driver.Catalog.Users + " SET " +
                "firstname = @first, lastname = @last, email = @email, passwordsha256 = @pw, " +
                "issystemadmin = @sysadmin, istenantadmin = @tenantadmin, active = @active, isprotected = @protected, lastupdateutc = @updated " +
                "WHERE tenantid = @tid AND id = @id;";

            await _Driver.NonQueryAsync(sql, command =>
            {
                AdoDatabaseDriver.Add(command, "id", user.Id);
                AdoDatabaseDriver.Add(command, "tid", user.TenantId);
                AdoDatabaseDriver.Add(command, "first", user.FirstName);
                AdoDatabaseDriver.Add(command, "last", user.LastName);
                AdoDatabaseDriver.Add(command, "email", user.Email);
                AdoDatabaseDriver.Add(command, "pw", user.PasswordSha256);
                AdoDatabaseDriver.Add(command, "sysadmin", user.IsSystemAdmin);
                AdoDatabaseDriver.Add(command, "tenantadmin", user.IsTenantAdmin);
                AdoDatabaseDriver.Add(command, "active", user.Active);
                AdoDatabaseDriver.Add(command, "protected", user.IsProtected);
                AdoDatabaseDriver.Add(command, "updated", user.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return user;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbTransaction transaction = await _Driver.BeginTransactionAsync(connection, false, token).ConfigureAwait(false))
            {
                await using (DbCommand credentials = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + _Driver.Catalog.Credentials + " WHERE tenantid = @tid AND userid = @uid;"))
                {
                    AdoDatabaseDriver.Add(credentials, "tid", tenantId);
                    AdoDatabaseDriver.Add(credentials, "uid", id);
                    await credentials.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                int affected;
                await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + _Driver.Catalog.Users + " WHERE tenantid = @tid AND id = @id;"))
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "id", id);
                    affected = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                return affected > 0;
            }
        }

        #endregion
    }
}

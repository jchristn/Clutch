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
    /// PostgreSQL implementation of user data access.
    /// </summary>
    public class UserMethods : IUserMethods
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
        public UserMethods(PostgresqlDatabaseDriver driver)
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

            string sql = @"
INSERT INTO users (id, tenantid, firstname, lastname, email, passwordsha256, issystemadmin, istenantadmin, active, isprotected, createdutc, lastupdateutc)
VALUES (@id, @tid, @first, @last, @email, @pw, @sysadmin, @tenantadmin, @active, @protected, @created, @updated);";

            await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", user.Id);
                parameters.AddWithValue("tid", user.TenantId);
                parameters.AddWithValue("first", user.FirstName);
                parameters.AddWithValue("last", user.LastName);
                parameters.AddWithValue("email", user.Email);
                parameters.AddWithValue("pw", user.PasswordSha256);
                parameters.AddWithValue("sysadmin", user.IsSystemAdmin);
                parameters.AddWithValue("tenantadmin", user.IsTenantAdmin);
                parameters.AddWithValue("active", user.Active);
                parameters.AddWithValue("protected", user.IsProtected);
                parameters.AddWithValue("created", user.CreatedUtc);
                parameters.AddWithValue("updated", user.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return user;
        }

        /// <inheritdoc />
        public async Task<User?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM users WHERE tenantid = @tid AND id = @id;",
                parameters =>
                {
                    parameters.AddWithValue("tid", tenantId);
                    parameters.AddWithValue("id", id);
                },
                Converters.ToUser,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<User?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM users WHERE tenantid = @tid AND email = @email;",
                parameters =>
                {
                    parameters.AddWithValue("tid", tenantId);
                    parameters.AddWithValue("email", email);
                },
                Converters.ToUser,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<User>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            return await _Driver.QueryAsync(
                "SELECT * FROM users WHERE tenantid = @tid ORDER BY createdutc ASC;",
                parameters => parameters.AddWithValue("tid", tenantId),
                Converters.ToUser,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<User>> EnumerateAsync(string tenantId, EnumerationQuery query, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            query ??= new EnumerationQuery();

            object? countResult = await _Driver.ScalarAsync("SELECT COUNT(*) FROM users WHERE tenantid = @tid;",
                parameters => parameters.AddWithValue("tid", tenantId), token).ConfigureAwait(false);
            long total = countResult == null ? 0 : (long)countResult;

            string sql = "SELECT * FROM users WHERE tenantid = @tid" + EnumerationSql.OrderClause(query, "createdutc", "email") + " OFFSET @skip LIMIT @max;";
            List<User> objects = await _Driver.QueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("tid", tenantId);
                parameters.AddWithValue("skip", query.Skip);
                parameters.AddWithValue("max", query.MaxResults);
            }, Converters.ToUser, token).ConfigureAwait(false);

            return EnumerationResult<User>.Build(query, total, objects);
        }

        /// <inheritdoc />
        public async Task<User> UpdateAsync(User user, CancellationToken token = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            user.LastUpdateUtc = DateTime.UtcNow;

            string sql = @"
UPDATE users SET
    firstname = @first,
    lastname = @last,
    email = @email,
    passwordsha256 = @pw,
    issystemadmin = @sysadmin,
    istenantadmin = @tenantadmin,
    active = @active,
    isprotected = @protected,
    lastupdateutc = @updated
WHERE tenantid = @tid AND id = @id;";

            await _Driver.NonQueryAsync(sql, parameters =>
            {
                parameters.AddWithValue("id", user.Id);
                parameters.AddWithValue("tid", user.TenantId);
                parameters.AddWithValue("first", user.FirstName);
                parameters.AddWithValue("last", user.LastName);
                parameters.AddWithValue("email", user.Email);
                parameters.AddWithValue("pw", user.PasswordSha256);
                parameters.AddWithValue("sysadmin", user.IsSystemAdmin);
                parameters.AddWithValue("tenantadmin", user.IsTenantAdmin);
                parameters.AddWithValue("active", user.Active);
                parameters.AddWithValue("protected", user.IsProtected);
                parameters.AddWithValue("updated", user.LastUpdateUtc);
            }, token).ConfigureAwait(false);

            return user;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            await using (NpgsqlConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await using (NpgsqlCommand credentials = new NpgsqlCommand("DELETE FROM credentials WHERE tenantid = @tid AND userid = @uid;", connection, transaction))
                    {
                        credentials.Parameters.AddWithValue("tid", tenantId);
                        credentials.Parameters.AddWithValue("uid", id);
                        await credentials.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int affected;
                    await using (NpgsqlCommand command = new NpgsqlCommand("DELETE FROM users WHERE tenantid = @tid AND id = @id;", connection, transaction))
                    {
                        command.Parameters.AddWithValue("tid", tenantId);
                        command.Parameters.AddWithValue("id", id);
                        affected = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return affected > 0;
                }
            }
        }

        #endregion
    }
}

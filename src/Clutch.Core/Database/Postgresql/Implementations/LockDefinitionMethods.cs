namespace Clutch.Core.Database.Postgresql.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Models;
    using Npgsql;

    /// <summary>
    /// PostgreSQL implementation of lock definition read and administration access.
    /// </summary>
    public class LockDefinitionMethods : ILockDefinitionMethods
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
        public LockDefinitionMethods(PostgresqlDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<LockDefinition?> ReadAsync(string tenantId, string lockKey, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(lockKey)) throw new ArgumentNullException(nameof(lockKey));

            return await _Driver.QuerySingleAsync(
                "SELECT * FROM lock_definitions WHERE tenantid = @tid AND lockkey = @key;",
                parameters =>
                {
                    parameters.AddWithValue("tid", tenantId);
                    parameters.AddWithValue("key", lockKey);
                },
                Converters.ToLockDefinition,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<LockDefinition>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            return await _Driver.QueryAsync(
                "SELECT * FROM lock_definitions WHERE tenantid = @tid ORDER BY lockkey ASC;",
                parameters => parameters.AddWithValue("tid", tenantId),
                Converters.ToLockDefinition,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string lockKey, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(lockKey)) throw new ArgumentNullException(nameof(lockKey));

            await using (NpgsqlConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await using (NpgsqlCommand holders = new NpgsqlCommand("DELETE FROM lock_holders WHERE tenantid = @tid AND lockkey = @key;", connection, transaction))
                    {
                        holders.Parameters.AddWithValue("tid", tenantId);
                        holders.Parameters.AddWithValue("key", lockKey);
                        await holders.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int affected;
                    await using (NpgsqlCommand command = new NpgsqlCommand("DELETE FROM lock_definitions WHERE tenantid = @tid AND lockkey = @key;", connection, transaction))
                    {
                        command.Parameters.AddWithValue("tid", tenantId);
                        command.Parameters.AddWithValue("key", lockKey);
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

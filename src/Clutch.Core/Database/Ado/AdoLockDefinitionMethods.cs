namespace Clutch.Core.Database.Ado
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Models;

    /// <summary>
    /// Provider-neutral lock definition read and administration access.
    /// </summary>
    public class AdoLockDefinitionMethods : ILockDefinitionMethods
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
        public AdoLockDefinitionMethods(AdoDatabaseDriver driver)
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
                "SELECT * FROM " + _Driver.Catalog.LockDefinitions + " WHERE tenantid = @tid AND lockkey = @key;",
                command =>
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "key", lockKey);
                },
                AdoConverters.ToLockDefinition,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<List<LockDefinition>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));

            return await _Driver.QueryAsync(
                "SELECT * FROM " + _Driver.Catalog.LockDefinitions + " WHERE tenantid = @tid ORDER BY lockkey ASC;",
                command => AdoDatabaseDriver.Add(command, "tid", tenantId),
                AdoConverters.ToLockDefinition,
                token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string lockKey, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(lockKey)) throw new ArgumentNullException(nameof(lockKey));

            await using (DbConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbTransaction transaction = await _Driver.BeginTransactionAsync(connection, false, token).ConfigureAwait(false))
            {
                await using (DbCommand holders = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + _Driver.Catalog.LockHolders + " WHERE tenantid = @tid AND lockkey = @key;"))
                {
                    AdoDatabaseDriver.Add(holders, "tid", tenantId);
                    AdoDatabaseDriver.Add(holders, "key", lockKey);
                    await holders.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                int affected;
                await using (DbCommand command = AdoDatabaseDriver.NewCommand(connection, transaction, "DELETE FROM " + _Driver.Catalog.LockDefinitions + " WHERE tenantid = @tid AND lockkey = @key;"))
                {
                    AdoDatabaseDriver.Add(command, "tid", tenantId);
                    AdoDatabaseDriver.Add(command, "key", lockKey);
                    affected = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await transaction.CommitAsync(token).ConfigureAwait(false);
                return affected > 0;
            }
        }

        #endregion
    }
}

namespace Clutch.Core.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Postgresql;
    using Clutch.Core.Enums;

    /// <summary>
    /// Composition root for the data layer. Creates the driver for the configured provider. Clutch
    /// implements Postgresql only; other providers throw until implemented.
    /// </summary>
    public static class DatabaseDriverFactory
    {
        #region Public-Methods

        /// <summary>
        /// Create a driver for the configured provider.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <returns>A database driver.</returns>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="NotSupportedException">Thrown when the provider is not implemented.</exception>
        public static DatabaseDriverBase Create(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            switch (settings.Type)
            {
                case DatabaseTypeEnum.Postgresql:
                    return new PostgresqlDatabaseDriver(settings);
                case DatabaseTypeEnum.Sqlite:
                case DatabaseTypeEnum.Mysql:
                case DatabaseTypeEnum.SqlServer:
                    throw new NotSupportedException(
                        "Database provider '" + settings.Type + "' is not implemented. Clutch supports Postgresql only.");
                default:
                    throw new NotSupportedException("Unknown database type: " + settings.Type);
            }
        }

        /// <summary>
        /// Create and initialize a driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An initialized database driver.</returns>
        public static async Task<DatabaseDriverBase> CreateAndInitializeAsync(DatabaseSettings settings, CancellationToken token = default)
        {
            DatabaseDriverBase driver = Create(settings);
            await driver.InitializeAsync(token).ConfigureAwait(false);
            return driver;
        }

        #endregion
    }
}

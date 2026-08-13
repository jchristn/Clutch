namespace Clutch.Core.Database.Sqlite
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Ado;
    using Clutch.Core.Database.Sql;
    using Clutch.Core.Enums;
    using Microsoft.Data.Sqlite;

    /// <summary>
    /// SQLite database driver. Single-node only. Enables WAL journaling and a busy timeout on each
    /// connection, and opens acquire transactions in IMMEDIATE mode so concurrent acquirers serialize.
    /// </summary>
    public class SqliteDatabaseDriver : AdoDatabaseDriver
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get
            {
                return DatabaseTypeEnum.Sqlite;
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public SqliteDatabaseDriver(DatabaseSettings settings)
            : base(settings, new SqliteDialect())
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override DbConnection CreateConnection()
        {
            return new SqliteConnection(Settings.ToSqliteConnectionString());
        }

        /// <inheritdoc />
        public override Task<DbTransaction> BeginTransactionAsync(DbConnection connection, bool immediate, CancellationToken token = default)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            SqliteConnection sqlite = (SqliteConnection)connection;
            DbTransaction transaction = sqlite.BeginTransaction(deferred: !immediate);
            return Task.FromResult(transaction);
        }

        #endregion

        #region Protected-Methods

        /// <inheritdoc />
        protected override async Task OnConnectionOpenedAsync(DbConnection connection, CancellationToken token)
        {
            await using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL;";
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        #endregion
    }
}

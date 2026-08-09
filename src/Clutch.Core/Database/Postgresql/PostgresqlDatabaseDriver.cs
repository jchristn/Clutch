namespace Clutch.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Postgresql.Implementations;
    using Clutch.Core.Database.Postgresql.Queries;
    using Clutch.Core.Enums;
    using Npgsql;

    /// <summary>
    /// PostgreSQL database driver. Owns a pooled data source, applies tracked migrations, seeds default
    /// records, and exposes parameterized execution helpers to the method implementations.
    /// </summary>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get
            {
                return DatabaseTypeEnum.Postgresql;
            }
        }

        /// <summary>
        /// The pooled data source. Available after construction.
        /// </summary>
        public NpgsqlDataSource DataSource
        {
            get
            {
                return _DataSource;
            }
        }

        #endregion

        #region Private-Members

        private readonly DatabaseSettings _Settings;
        private readonly NpgsqlDataSource _DataSource;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the driver and wire the method implementations.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public PostgresqlDatabaseDriver(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            _Settings = settings;
            _DataSource = NpgsqlDataSource.Create(settings.ToPostgresConnectionString());

            Tenants = new TenantMethods(this);
            Users = new UserMethods(this);
            Credentials = new CredentialMethods(this);
            Sessions = new AuthSessionMethods(this);
            LockDefinitions = new LockDefinitionMethods(this);
            LockHolders = new LockHolderMethods(this);
            LockAudit = new LockAuditMethods(this);
            RequestHistory = new RequestHistoryMethods(this);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            await ApplyMigrationsAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> PingAsync(CancellationToken token = default)
        {
            object? result = await ScalarAsync("SELECT 1;", null, token).ConfigureAwait(false);
            return result != null;
        }

        /// <inheritdoc />
        public override async Task CloseAsync(CancellationToken token = default)
        {
            await _DataSource.DisposeAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Open a pooled connection.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An open connection.</returns>
        public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken token = default)
        {
            return await _DataSource.OpenConnectionAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute a non-query statement.
        /// </summary>
        /// <param name="sql">SQL text.</param>
        /// <param name="bind">Optional parameter binder.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of affected rows.</returns>
        public async Task<int> NonQueryAsync(string sql, Action<NpgsqlParameterCollection>? bind, CancellationToken token = default)
        {
            await using (NpgsqlConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                {
                    if (bind != null) bind(command.Parameters);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Execute a scalar query.
        /// </summary>
        /// <param name="sql">SQL text.</param>
        /// <param name="bind">Optional parameter binder.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The scalar result, or null.</returns>
        public async Task<object?> ScalarAsync(string sql, Action<NpgsqlParameterCollection>? bind, CancellationToken token = default)
        {
            await using (NpgsqlConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                {
                    if (bind != null) bind(command.Parameters);
                    return await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Execute a query and map each row.
        /// </summary>
        /// <typeparam name="T">Result element type.</typeparam>
        /// <param name="sql">SQL text.</param>
        /// <param name="bind">Optional parameter binder.</param>
        /// <param name="map">Row mapping function.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The mapped rows.</returns>
        public async Task<List<T>> QueryAsync<T>(string sql, Action<NpgsqlParameterCollection>? bind, Func<NpgsqlDataReader, T> map, CancellationToken token = default)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            List<T> results = new List<T>();
            await using (NpgsqlConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
            {
                await using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                {
                    if (bind != null) bind(command.Parameters);
                    await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            results.Add(map(reader));
                        }
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Execute a query and map the first row, or return null.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="sql">SQL text.</param>
        /// <param name="bind">Optional parameter binder.</param>
        /// <param name="map">Row mapping function.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The first mapped row, or null.</returns>
        public async Task<T?> QuerySingleAsync<T>(string sql, Action<NpgsqlParameterCollection>? bind, Func<NpgsqlDataReader, T> map, CancellationToken token = default) where T : class
        {
            List<T> results = await QueryAsync(sql, bind, map, token).ConfigureAwait(false);
            return results.Count > 0 ? results[0] : null;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (!_Disposed)
            {
                _DataSource.Dispose();
                _Disposed = true;
            }
            base.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task ApplyMigrationsAsync(CancellationToken token)
        {
            await NonQueryAsync(SetupQueries.CreateSchemaMigrationsTable, null, token).ConfigureAwait(false);

            HashSet<int> applied = new HashSet<int>();
            List<int> appliedList = await QueryAsync(
                "SELECT version FROM schema_migrations ORDER BY version ASC;",
                null,
                reader => reader.GetInt32(0),
                token).ConfigureAwait(false);
            foreach (int version in appliedList) applied.Add(version);

            foreach (SchemaMigration migration in SetupQueries.All)
            {
                if (applied.Contains(migration.Version)) continue;

                await using (NpgsqlConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
                {
                    await using (NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                    {
                        foreach (string statement in migration.Statements)
                        {
                            await using (NpgsqlCommand command = new NpgsqlCommand(statement, connection, transaction))
                            {
                                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            }
                        }

                        await using (NpgsqlCommand record = new NpgsqlCommand(
                            "INSERT INTO schema_migrations (version, description, appliedutc) VALUES (@v, @d, @t);",
                            connection, transaction))
                        {
                            record.Parameters.AddWithValue("v", migration.Version);
                            record.Parameters.AddWithValue("d", migration.Description);
                            record.Parameters.AddWithValue("t", DateTime.UtcNow);
                            await record.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }

                        await transaction.CommitAsync(token).ConfigureAwait(false);
                    }
                }
            }
        }

        #endregion
    }
}

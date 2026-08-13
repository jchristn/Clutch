namespace Clutch.Core.Database.Ado
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Sql;

    /// <summary>
    /// Shared ADO.NET database driver. Implements the full data-access surface once against
    /// <see cref="System.Data.Common"/>, parameterized by a <see cref="SqlDialect"/> and a
    /// <see cref="TableCatalog"/>. Provider subclasses supply only a connection, a dialect, and any
    /// connection-open or transaction-begin specifics.
    /// </summary>
    public abstract class AdoDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <summary>
        /// The SQL dialect for this provider.
        /// </summary>
        public SqlDialect Dialect
        {
            get
            {
                return _Dialect;
            }
        }

        /// <summary>
        /// The resolved table catalog for this deployment.
        /// </summary>
        public TableCatalog Catalog
        {
            get
            {
                return _Catalog;
            }
        }

        /// <summary>
        /// The database settings this driver was constructed with.
        /// </summary>
        public DatabaseSettings Settings
        {
            get
            {
                return _Settings;
            }
        }

        #endregion

        #region Private-Members

        private readonly DatabaseSettings _Settings;
        private readonly SqlDialect _Dialect;
        private readonly TableCatalog _Catalog;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate and wire the shared method implementations.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="dialect">Provider dialect.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        protected AdoDatabaseDriver(DatabaseSettings settings, SqlDialect dialect)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
            _Catalog = new TableCatalog(settings.Tables, settings.Schema, dialect);

            Tenants = new AdoTenantMethods(this);
            Users = new AdoUserMethods(this);
            Credentials = new AdoCredentialMethods(this);
            Sessions = new AdoAuthSessionMethods(this);
            LockDefinitions = new AdoLockDefinitionMethods(this);
            LockHolders = new AdoLockHolderMethods(this);
            LockAudit = new AdoLockAuditMethods(this);
            RequestHistory = new AdoRequestHistoryMethods(this);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            _Settings.Validate();

            if (!String.IsNullOrEmpty(_Settings.Schema))
            {
                string? schemaSql = _Dialect.CreateSchemaStatement(_Settings.Schema!);
                if (!String.IsNullOrEmpty(schemaSql)) await NonQueryAsync(schemaSql!, null, token).ConfigureAwait(false);
            }

            if (_Settings.ManageSchema) await ApplyMigrationsAsync(token).ConfigureAwait(false);
            else await VerifyTablesAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> PingAsync(CancellationToken token = default)
        {
            object? result = await ScalarAsync("SELECT 1;", null, token).ConfigureAwait(false);
            return result != null;
        }

        /// <inheritdoc />
        public override Task CloseAsync(CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Create a new, unopened provider connection.
        /// </summary>
        /// <returns>A new connection.</returns>
        public abstract DbConnection CreateConnection();

        /// <summary>
        /// Open a new connection.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An open connection.</returns>
        public virtual async Task<DbConnection> OpenConnectionAsync(CancellationToken token = default)
        {
            DbConnection connection = CreateConnection();
            await connection.OpenAsync(token).ConfigureAwait(false);
            await OnConnectionOpenedAsync(connection, token).ConfigureAwait(false);
            return connection;
        }

        /// <summary>
        /// Begin a transaction. When <paramref name="immediate"/> is true, providers that need an explicit
        /// up-front write lock to serialize the acquire path (SQLite) take it here.
        /// </summary>
        /// <param name="connection">Open connection.</param>
        /// <param name="immediate">Whether to acquire a write lock immediately.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A transaction.</returns>
        public virtual async Task<DbTransaction> BeginTransactionAsync(DbConnection connection, bool immediate, CancellationToken token = default)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            return await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Execute a non-query statement.
        /// </summary>
        /// <param name="sql">SQL text.</param>
        /// <param name="bind">Optional parameter binder.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of affected rows.</returns>
        public async Task<int> NonQueryAsync(string sql, Action<DbCommand>? bind, CancellationToken token = default)
        {
            await using (DbConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                bind?.Invoke(command);
                return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Execute a scalar query.
        /// </summary>
        /// <param name="sql">SQL text.</param>
        /// <param name="bind">Optional parameter binder.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The scalar result, or null.</returns>
        public async Task<object?> ScalarAsync(string sql, Action<DbCommand>? bind, CancellationToken token = default)
        {
            await using (DbConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                bind?.Invoke(command);
                object? result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return result == null || result == DBNull.Value ? null : result;
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
        /// <exception cref="ArgumentNullException">Thrown when map is null.</exception>
        public async Task<List<T>> QueryAsync<T>(string sql, Action<DbCommand>? bind, Func<DbDataReader, T> map, CancellationToken token = default)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            List<T> results = new List<T>();
            await using (DbConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
            await using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                bind?.Invoke(command);
                await using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) results.Add(map(reader));
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
        public async Task<T?> QuerySingleAsync<T>(string sql, Action<DbCommand>? bind, Func<DbDataReader, T> map, CancellationToken token = default) where T : class
        {
            List<T> results = await QueryAsync(sql, bind, map, token).ConfigureAwait(false);
            return results.Count > 0 ? results[0] : null;
        }

        /// <summary>
        /// Add a parameter to a command, mapping null to DBNull.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <param name="name">Parameter name without prefix.</param>
        /// <param name="value">Value, or null.</param>
        /// <exception cref="ArgumentNullException">Thrown when command or name is null.</exception>
        public static void Add(DbCommand command, string name, object? value)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = "@" + name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        /// <summary>
        /// Run an action, retrying it when the provider raises a transient concurrency conflict (deadlock,
        /// serialization failure, or lock-wait timeout). Used to wrap the transactional lock mutations that
        /// serialize acquirers with row/range locks.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="action">The action to run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The action result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
        public async Task<T> RetryOnConflictAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            int attempt = 0;
            while (true)
            {
                try
                {
                    return await action(token).ConfigureAwait(false);
                }
                catch (Exception ex) when (_Dialect.IsTransientConflict(ex) && attempt < 16)
                {
                    attempt++;
                    await Task.Delay(Math.Min(5 + attempt * 5, 100), token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Create a command bound to a connection and transaction.
        /// </summary>
        /// <param name="connection">Connection.</param>
        /// <param name="transaction">Transaction.</param>
        /// <param name="sql">SQL text.</param>
        /// <returns>The command.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public static DbCommand NewCommand(DbConnection connection, DbTransaction transaction, string sql)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (String.IsNullOrEmpty(sql)) throw new ArgumentNullException(nameof(sql));

            DbCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            return command;
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Hook invoked immediately after a connection is opened. Override to set provider pragmas.
        /// </summary>
        /// <param name="connection">The freshly opened connection.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        protected virtual Task OnConnectionOpenedAsync(DbConnection connection, CancellationToken token)
        {
            return Task.CompletedTask;
        }

        #endregion

        #region Private-Methods

        private async Task ApplyMigrationsAsync(CancellationToken token)
        {
            await NonQueryAsync(SchemaBuilder.MigrationsTableDdl(_Catalog, _Dialect), null, token).ConfigureAwait(false);

            HashSet<int> applied = new HashSet<int>();
            List<int> appliedList = await QueryAsync(
                "SELECT version FROM " + _Catalog.SchemaMigrations + " ORDER BY version ASC;",
                null,
                reader => Convert.ToInt32(reader.GetValue(0)),
                token).ConfigureAwait(false);
            foreach (int version in appliedList) applied.Add(version);

            foreach (SchemaMigration migration in SchemaBuilder.Build(_Catalog, _Dialect))
            {
                if (applied.Contains(migration.Version)) continue;

                await using (DbConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false))
                await using (DbTransaction transaction = await BeginTransactionAsync(connection, false, token).ConfigureAwait(false))
                {
                    foreach (string statement in migration.Statements)
                    {
                        await using (DbCommand command = NewCommand(connection, transaction, statement))
                        {
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    await using (DbCommand record = NewCommand(connection, transaction,
                        "INSERT INTO " + _Catalog.SchemaMigrations + " (version, description, appliedutc) VALUES (@v, @d, @t);"))
                    {
                        Add(record, "v", migration.Version);
                        Add(record, "d", migration.Description);
                        Add(record, "t", DateTime.UtcNow);
                        await record.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }
        }

        private async Task VerifyTablesAsync(CancellationToken token)
        {
            foreach (TableCatalogEntry entry in _Catalog.DataEntries)
            {
                string probe = _Dialect.TableExistsProbe(entry.Schema, entry.RawName);
                object? result = await ScalarAsync(probe, command =>
                {
                    Add(command, "name", entry.RawName);
                    if (!String.IsNullOrEmpty(entry.Schema)) Add(command, "schema", entry.Schema);
                }, token).ConfigureAwait(false);

                if (result == null)
                {
                    throw new InvalidOperationException(
                        "Required table '" + entry.RawName + "' was not found and ManageSchema is disabled. " +
                        "Create the Clutch tables (see sql/" + _Dialect.DatabaseType.ToString().ToLowerInvariant() + "/schema.sql) or enable ManageSchema.");
                }
            }
        }

        #endregion
    }
}

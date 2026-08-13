namespace Clutch.Core.Database.Sql
{
    using System;
    using Clutch.Core.Enums;

    /// <summary>
    /// Provider-specific SQL rendering. Centralizes every dialect difference — identifier quoting,
    /// pagination, boolean literals, case-insensitive matching, JSON storage, row-lock strategy, and DDL
    /// type mapping — so the shared data-access implementations are written once against
    /// <see cref="System.Data.Common"/> and parameterized by a dialect.
    /// </summary>
    public abstract class SqlDialect
    {
        #region Public-Members

        /// <summary>
        /// The provider this dialect targets.
        /// </summary>
        public abstract DatabaseTypeEnum DatabaseType { get; }

        /// <summary>
        /// Whether DELETE/UPDATE statements can use a RETURNING clause (PostgreSQL, SQLite).
        /// </summary>
        public virtual bool SupportsReturning
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Whether DELETE/UPDATE statements can use an OUTPUT clause (SQL Server).
        /// </summary>
        public virtual bool SupportsOutput
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// A token appended to a JSON-column insert parameter to cast a bound string into the native JSON
        /// type. Empty for providers that accept a plain string. For PostgreSQL this is "::jsonb".
        /// </summary>
        public virtual string JsonInsertCast
        {
            get
            {
                return string.Empty;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Quote a single identifier (table or column) for this provider.
        /// </summary>
        /// <param name="identifier">Identifier to quote.</param>
        /// <returns>The quoted identifier.</returns>
        public abstract string Quote(string identifier);

        /// <summary>
        /// Build a schema-qualified, quoted table reference.
        /// </summary>
        /// <param name="schema">Optional schema/namespace. Null or empty means the provider default.</param>
        /// <param name="rawName">Unquoted table name.</param>
        /// <returns>The quoted, optionally schema-qualified reference.</returns>
        public virtual string QualifiedReference(string? schema, string rawName)
        {
            if (String.IsNullOrEmpty(rawName)) throw new ArgumentNullException(nameof(rawName));
            if (String.IsNullOrEmpty(schema)) return Quote(rawName);
            return Quote(schema) + "." + Quote(rawName);
        }

        /// <summary>
        /// The pagination clause appended after ORDER BY. Uses parameters named skip and max.
        /// </summary>
        /// <returns>The clause with a leading space.</returns>
        public abstract string LimitOffsetClause();

        /// <summary>
        /// Render a boolean literal for this provider.
        /// </summary>
        /// <param name="value">Boolean value.</param>
        /// <returns>The literal text.</returns>
        public abstract string BooleanLiteral(bool value);

        /// <summary>
        /// Build a case-insensitive substring match clause for a column.
        /// </summary>
        /// <param name="columnReference">The column reference.</param>
        /// <param name="parameterName">The parameter name (without prefix) holding the "%value%" pattern.</param>
        /// <returns>The match clause.</returns>
        public abstract string CaseInsensitiveLike(string columnReference, string parameterName);

        /// <summary>
        /// Build the SELECT that reads and row-locks a lock definition for the atomic acquire path.
        /// The WHERE clause matches tenantid and lockkey parameters.
        /// </summary>
        /// <param name="tableReference">The lock definitions table reference.</param>
        /// <returns>The full SELECT statement, ending without a semicolon.</returns>
        public abstract string DefinitionLockSelect(string tableReference);

        /// <summary>
        /// Build an existence probe returning a single row when a match exists.
        /// </summary>
        /// <param name="tableReference">The table reference.</param>
        /// <param name="whereClause">The WHERE clause including the WHERE keyword.</param>
        /// <returns>The full SELECT statement.</returns>
        public abstract string ExistsSelect(string tableReference, string whereClause);

        /// <summary>
        /// Build a CREATE TABLE statement that is a no-op when the table already exists.
        /// </summary>
        /// <param name="schema">Optional schema.</param>
        /// <param name="rawName">Unquoted table name.</param>
        /// <param name="columnsCsv">Comma-separated column definitions.</param>
        /// <returns>The DDL statement.</returns>
        public abstract string CreateTable(string? schema, string rawName, string columnsCsv);

        /// <summary>
        /// Build a CREATE INDEX statement appropriate for this provider.
        /// </summary>
        /// <param name="indexName">Unquoted index name.</param>
        /// <param name="schema">Optional schema.</param>
        /// <param name="rawTableName">Unquoted table name.</param>
        /// <param name="columnsCsv">Comma-separated quoted column list.</param>
        /// <param name="unique">Whether the index is unique.</param>
        /// <returns>The DDL statement.</returns>
        public abstract string CreateIndex(string indexName, string? schema, string rawTableName, string columnsCsv, bool unique);

        /// <summary>
        /// Build a query that returns one row when the given table exists. Used to verify schema presence
        /// when Clutch is not permitted to manage the schema.
        /// </summary>
        /// <param name="schema">Optional schema.</param>
        /// <param name="rawName">Unquoted table name.</param>
        /// <returns>The full SELECT statement using parameters named schema and name where applicable.</returns>
        public abstract string TableExistsProbe(string? schema, string rawName);

        /// <summary>
        /// Build a statement that creates the given schema/namespace if the provider supports one and it is
        /// missing. Returns null for providers without a schema concept (SQLite, MySQL).
        /// </summary>
        /// <param name="schema">Schema name.</param>
        /// <returns>The DDL statement, or null when not applicable.</returns>
        public virtual string? CreateSchemaStatement(string schema)
        {
            return null;
        }

        /// <summary>
        /// Whether the given exception represents a transient concurrency conflict (deadlock, serialization
        /// failure, or lock-wait timeout) that should be retried by re-running the transaction.
        /// </summary>
        /// <param name="exception">The exception thrown during a transaction.</param>
        /// <returns>True if the caller should retry.</returns>
        public virtual bool IsTransientConflict(Exception exception)
        {
            return false;
        }

        #endregion

        #region DDL-Type-Tokens

        /// <summary>
        /// Column type for a 64-character identifier.
        /// </summary>
        public virtual string TypeId
        {
            get
            {
                return "VARCHAR(64)";
            }
        }

        /// <summary>
        /// Column type for a 128-character identifier.
        /// </summary>
        public virtual string TypeId128
        {
            get
            {
                return "VARCHAR(128)";
            }
        }

        /// <summary>
        /// Column type for unbounded text.
        /// </summary>
        public abstract string TypeText { get; }

        /// <summary>
        /// Column type for a 32-bit integer.
        /// </summary>
        public virtual string TypeInt
        {
            get
            {
                return "INTEGER";
            }
        }

        /// <summary>
        /// Column type for a 64-bit integer.
        /// </summary>
        public virtual string TypeBigInt
        {
            get
            {
                return "BIGINT";
            }
        }

        /// <summary>
        /// Column type for a boolean.
        /// </summary>
        public abstract string TypeBool { get; }

        /// <summary>
        /// Column type for a double-precision floating point number.
        /// </summary>
        public abstract string TypeDouble { get; }

        /// <summary>
        /// Column type for a UTC timestamp.
        /// </summary>
        public abstract string TypeTimestamp { get; }

        /// <summary>
        /// Column type for JSON storage.
        /// </summary>
        public abstract string TypeJson { get; }

        #endregion
    }
}

namespace Clutch.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Clutch.Core.Database.Sql;

    /// <summary>
    /// The resolved set of Clutch tables for a deployment. Applies the clutch_-prefixed default name for
    /// each purpose, layers any per-purpose override and global prefix, validates every identifier against
    /// a strict allowlist, and precomputes the provider-quoted reference for each table. Because table
    /// names are concatenated into SQL text, the allowlist is the sole defense against injection through a
    /// configured name.
    /// </summary>
    public class TableCatalog
    {
        #region Public-Members

        /// <summary>
        /// Reference for the schema-migrations tracking table.
        /// </summary>
        public string SchemaMigrations { get; }

        /// <summary>
        /// Reference for the tenants table.
        /// </summary>
        public string Tenants { get; }

        /// <summary>
        /// Reference for the users table.
        /// </summary>
        public string Users { get; }

        /// <summary>
        /// Reference for the credentials table.
        /// </summary>
        public string Credentials { get; }

        /// <summary>
        /// Reference for the authentication sessions table.
        /// </summary>
        public string AuthSessions { get; }

        /// <summary>
        /// Reference for the lock definitions table.
        /// </summary>
        public string LockDefinitions { get; }

        /// <summary>
        /// Reference for the lock holders table.
        /// </summary>
        public string LockHolders { get; }

        /// <summary>
        /// Reference for the lock audit table.
        /// </summary>
        public string LockAudit { get; }

        /// <summary>
        /// Reference for the request history table.
        /// </summary>
        public string RequestHistory { get; }

        /// <summary>
        /// All resolved entries, including the migrations table.
        /// </summary>
        public IReadOnlyList<TableCatalogEntry> Entries
        {
            get
            {
                return _Entries;
            }
        }

        /// <summary>
        /// The resolved entries excluding the migrations table. These are the tables that must exist when
        /// schema management is disabled.
        /// </summary>
        public IReadOnlyList<TableCatalogEntry> DataEntries
        {
            get
            {
                return _DataEntries;
            }
        }

        #endregion

        #region Private-Members

        private static readonly Regex _Identifier = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private readonly List<TableCatalogEntry> _Entries = new List<TableCatalogEntry>();
        private readonly List<TableCatalogEntry> _DataEntries = new List<TableCatalogEntry>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Resolve the catalog from naming settings, an optional schema, and a dialect.
        /// </summary>
        /// <param name="tables">Per-purpose naming overrides.</param>
        /// <param name="schema">Optional schema/namespace.</param>
        /// <param name="dialect">Provider dialect used to quote references.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a resolved name, prefix, or schema is not a valid identifier.</exception>
        public TableCatalog(TableNamingSettings tables, string? schema, SqlDialect dialect)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (dialect == null) throw new ArgumentNullException(nameof(dialect));

            string prefix = tables.Prefix ?? string.Empty;
            if (!String.IsNullOrEmpty(prefix)) ValidateIdentifier(prefix, "table prefix");
            if (!String.IsNullOrEmpty(schema)) ValidateIdentifier(schema, "schema");

            SchemaMigrations = Resolve("schemaMigrations", tables.SchemaMigrations, "clutch_schema_migrations", prefix, schema, dialect, false);
            Tenants = Resolve("tenants", tables.Tenants, "clutch_tenants", prefix, schema, dialect, true);
            Users = Resolve("users", tables.Users, "clutch_users", prefix, schema, dialect, true);
            Credentials = Resolve("credentials", tables.Credentials, "clutch_credentials", prefix, schema, dialect, true);
            AuthSessions = Resolve("authSessions", tables.AuthSessions, "clutch_auth_sessions", prefix, schema, dialect, true);
            LockDefinitions = Resolve("lockDefinitions", tables.LockDefinitions, "clutch_lock_definitions", prefix, schema, dialect, true);
            LockHolders = Resolve("lockHolders", tables.LockHolders, "clutch_lock_holders", prefix, schema, dialect, true);
            LockAudit = Resolve("lockAudit", tables.LockAudit, "clutch_lock_audit", prefix, schema, dialect, true);
            RequestHistory = Resolve("requestHistory", tables.RequestHistory, "clutch_request_history", prefix, schema, dialect, true);
        }

        #endregion

        #region Private-Methods

        private string Resolve(string purpose, string? overrideName, string defaultName, string prefix, string? schema, SqlDialect dialect, bool isDataTable)
        {
            string chosen = String.IsNullOrEmpty(overrideName) ? defaultName : overrideName.Trim();
            ValidateIdentifier(chosen, "table name for '" + purpose + "'");

            string rawName = prefix + chosen;
            ValidateIdentifier(rawName, "resolved table name for '" + purpose + "'");

            string reference = dialect.QualifiedReference(schema, rawName);
            TableCatalogEntry entry = new TableCatalogEntry(purpose, rawName, schema, reference);
            _Entries.Add(entry);
            if (isDataTable) _DataEntries.Add(entry);
            return reference;
        }

        private static void ValidateIdentifier(string value, string description)
        {
            if (!_Identifier.IsMatch(value))
            {
                throw new ArgumentException(
                    "Invalid " + description + ": '" + value + "'. Identifiers must match ^[A-Za-z_][A-Za-z0-9_]*$.");
            }
        }

        #endregion
    }
}

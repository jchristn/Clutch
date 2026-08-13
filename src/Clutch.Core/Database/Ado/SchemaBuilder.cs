namespace Clutch.Core.Database.Ado
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Clutch.Core.Database.Sql;

    /// <summary>
    /// Builds the Clutch schema for any provider from the resolved <see cref="TableCatalog"/> and a
    /// <see cref="SqlDialect"/>. String columns use explicit VARCHAR lengths (portable across all four
    /// providers and safe for indexing on MySQL); only the genuinely provider-divergent column types
    /// (text, boolean, double, timestamp, json) come from dialect tokens.
    /// </summary>
    public class SchemaBuilder
    {
        #region Private-Members

        private readonly TableCatalog _Catalog;
        private readonly SqlDialect _Dialect;
        private readonly List<string> _Statements = new List<string>();

        #endregion

        #region Constructors-and-Factories

        private SchemaBuilder(TableCatalog catalog, SqlDialect dialect)
        {
            _Catalog = catalog;
            _Dialect = dialect;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the ordered, tracked migration list for the given catalog and dialect.
        /// </summary>
        /// <param name="catalog">Resolved table catalog.</param>
        /// <param name="dialect">Provider dialect.</param>
        /// <returns>The migration list.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public static List<SchemaMigration> Build(TableCatalog catalog, SqlDialect dialect)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (dialect == null) throw new ArgumentNullException(nameof(dialect));

            SchemaBuilder builder = new SchemaBuilder(catalog, dialect);
            builder.BuildVersion1();

            List<SchemaMigration> migrations = new List<SchemaMigration>();
            migrations.Add(new SchemaMigration(1, "Initial schema", builder._Statements));
            return migrations;
        }

        /// <summary>
        /// Build the DDL that creates the migration-tracking table.
        /// </summary>
        /// <param name="catalog">Resolved table catalog.</param>
        /// <param name="dialect">Provider dialect.</param>
        /// <returns>The DDL statement.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public static string MigrationsTableDdl(TableCatalog catalog, SqlDialect dialect)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (dialect == null) throw new ArgumentNullException(nameof(dialect));

            TableCatalogEntry entry = catalog.Entries.First(e => e.Purpose == "schemaMigrations");
            string columns =
                "    version INTEGER NOT NULL PRIMARY KEY,\n" +
                "    description VARCHAR(1024) NOT NULL,\n" +
                "    appliedutc " + dialect.TypeTimestamp + " NOT NULL";
            return dialect.CreateTable(entry.Schema, entry.RawName, columns);
        }

        #endregion

        #region Private-Methods

        private void BuildVersion1()
        {
            BuildTenants();
            BuildUsers();
            BuildCredentials();
            BuildAuthSessions();
            BuildLockDefinitions();
            BuildLockHolders();
            BuildLockAudit();
            BuildRequestHistory();
        }

        private void BuildTenants()
        {
            TableCatalogEntry entry = Entry("tenants");
            string columns =
                "    id VARCHAR(64) NOT NULL PRIMARY KEY,\n" +
                "    name VARCHAR(256) NOT NULL,\n" +
                "    lockhistoryretentiondays INTEGER NOT NULL DEFAULT 7,\n" +
                "    defaultleasems INTEGER NOT NULL DEFAULT 30000,\n" +
                "    maxleasems INTEGER NOT NULL DEFAULT 300000,\n" +
                Bool("active", true) +
                Bool("isprotected", false) +
                Ts("createdutc") +
                TsLast("lastupdateutc");
            AddTable(entry, columns);
            AddIndex(entry, "ux_" + entry.RawName + "_name", true, "name");
        }

        private void BuildUsers()
        {
            TableCatalogEntry entry = Entry("users");
            string columns =
                "    id VARCHAR(64) NOT NULL PRIMARY KEY,\n" +
                "    tenantid VARCHAR(64) NOT NULL,\n" +
                "    firstname VARCHAR(256) NOT NULL DEFAULT '',\n" +
                "    lastname VARCHAR(256) NOT NULL DEFAULT '',\n" +
                "    email VARCHAR(256) NOT NULL,\n" +
                "    passwordsha256 VARCHAR(64) NOT NULL,\n" +
                Bool("issystemadmin", false) +
                Bool("istenantadmin", false) +
                Bool("active", true) +
                Bool("isprotected", false) +
                Ts("createdutc") +
                TsLast("lastupdateutc");
            AddTable(entry, columns);
            AddIndex(entry, "ux_" + entry.RawName + "_tenant_email", true, "tenantid", "email");
            AddIndex(entry, "ix_" + entry.RawName + "_tenant", false, "tenantid");
        }

        private void BuildCredentials()
        {
            TableCatalogEntry entry = Entry("credentials");
            string columns =
                "    id VARCHAR(64) NOT NULL PRIMARY KEY,\n" +
                "    tenantid VARCHAR(64) NOT NULL,\n" +
                "    userid VARCHAR(64) NULL,\n" +
                "    name VARCHAR(256) NOT NULL,\n" +
                "    accesskey VARCHAR(256) NOT NULL,\n" +
                "    authmode VARCHAR(64) NOT NULL DEFAULT 'DirectHeader',\n" +
                "    lastusedutc " + _Dialect.TypeTimestamp + " NULL,\n" +
                "    expiresutc " + _Dialect.TypeTimestamp + " NULL,\n" +
                Bool("active", true) +
                Bool("isprotected", false) +
                Ts("createdutc") +
                TsLast("lastupdateutc");
            AddTable(entry, columns);
            AddIndex(entry, "ux_" + entry.RawName + "_accesskey", true, "accesskey");
            AddIndex(entry, "ix_" + entry.RawName + "_tenant", false, "tenantid");
            AddIndex(entry, "ix_" + entry.RawName + "_user", false, "userid");
        }

        private void BuildAuthSessions()
        {
            TableCatalogEntry entry = Entry("authSessions");
            string columns =
                "    id VARCHAR(64) NOT NULL PRIMARY KEY,\n" +
                "    tenantid VARCHAR(64) NOT NULL,\n" +
                "    userid VARCHAR(64) NULL,\n" +
                "    credentialid VARCHAR(64) NULL,\n" +
                "    principaltype VARCHAR(64) NOT NULL,\n" +
                "    tokenid VARCHAR(128) NOT NULL,\n" +
                "    sourceip VARCHAR(128) NULL,\n" +
                "    useragent VARCHAR(1024) NULL,\n" +
                "    expiresutc " + _Dialect.TypeTimestamp + " NOT NULL,\n" +
                "    lastusedutc " + _Dialect.TypeTimestamp + " NULL,\n" +
                "    revokedutc " + _Dialect.TypeTimestamp + " NULL,\n" +
                "    revocationreason VARCHAR(1024) NULL,\n" +
                Bool("active", true) +
                Bool("isprotected", false) +
                Ts("createdutc") +
                TsLast("lastupdateutc");
            AddTable(entry, columns);
            AddIndex(entry, "ux_" + entry.RawName + "_tokenid", true, "tokenid");
            AddIndex(entry, "ix_" + entry.RawName + "_tenant", false, "tenantid");
        }

        private void BuildLockDefinitions()
        {
            TableCatalogEntry entry = Entry("lockDefinitions");
            string columns =
                "    id VARCHAR(64) NOT NULL PRIMARY KEY,\n" +
                "    tenantid VARCHAR(64) NOT NULL,\n" +
                "    lockkey VARCHAR(512) NOT NULL,\n" +
                "    readmaxholders INTEGER NOT NULL DEFAULT -1,\n" +
                "    writeexclusivity VARCHAR(32) NOT NULL DEFAULT 'Exclusive',\n" +
                "    writemaxholders INTEGER NOT NULL DEFAULT 1,\n" +
                Bool("writeblocksreads", true) +
                "    defaultleasems INTEGER NOT NULL DEFAULT 30000,\n" +
                "    maxleasems INTEGER NOT NULL DEFAULT 300000,\n" +
                "    maxholdms INTEGER NOT NULL DEFAULT 3600000,\n" +
                "    fencingcounter BIGINT NOT NULL DEFAULT 0,\n" +
                "    firstacquiredbycredentialid VARCHAR(64) NULL,\n" +
                Bool("active", true) +
                Ts("createdutc") +
                TsLast("lastupdateutc");
            AddTable(entry, columns);
            AddIndex(entry, "ux_" + entry.RawName + "_tenant_key", true, "tenantid", "lockkey");
        }

        private void BuildLockHolders()
        {
            TableCatalogEntry entry = Entry("lockHolders");
            string columns =
                "    id VARCHAR(64) NOT NULL PRIMARY KEY,\n" +
                "    tenantid VARCHAR(64) NOT NULL,\n" +
                "    lockkey VARCHAR(512) NOT NULL,\n" +
                "    lockdefinitionid VARCHAR(64) NOT NULL,\n" +
                "    mode VARCHAR(32) NOT NULL,\n" +
                "    credentialid VARCHAR(64) NOT NULL DEFAULT '',\n" +
                "    sessionid VARCHAR(128) NOT NULL DEFAULT '',\n" +
                "    nodeid VARCHAR(128) NOT NULL DEFAULT '',\n" +
                "    fencingtoken BIGINT NOT NULL DEFAULT 0,\n" +
                "    acquiredutc " + _Dialect.TypeTimestamp + " NOT NULL,\n" +
                "    leaseexpiresutc " + _Dialect.TypeTimestamp + " NOT NULL,\n" +
                "    lastheartbeatutc " + _Dialect.TypeTimestamp + " NOT NULL,\n" +
                Bool("active", true) +
                Ts("createdutc") +
                TsLast("lastupdateutc");
            AddTable(entry, columns);
            AddIndex(entry, "ix_" + entry.RawName + "_tenant_key", false, "tenantid", "lockkey");
            AddIndex(entry, "ix_" + entry.RawName + "_session", false, "sessionid");
            AddIndex(entry, "ix_" + entry.RawName + "_lease", false, "leaseexpiresutc");
        }

        private void BuildLockAudit()
        {
            TableCatalogEntry entry = Entry("lockAudit");
            string columns =
                "    id VARCHAR(64) NOT NULL PRIMARY KEY,\n" +
                "    tenantid VARCHAR(64) NOT NULL,\n" +
                "    lockkey VARCHAR(512) NOT NULL,\n" +
                "    mode VARCHAR(32) NULL,\n" +
                "    eventtype VARCHAR(32) NOT NULL,\n" +
                "    credentialid VARCHAR(64) NULL,\n" +
                "    sessionid VARCHAR(128) NULL,\n" +
                "    nodeid VARCHAR(128) NOT NULL DEFAULT '',\n" +
                "    fencingtoken BIGINT NULL,\n" +
                "    reason VARCHAR(1024) NULL,\n" +
                "    createdutc " + _Dialect.TypeTimestamp + " NOT NULL";
            AddTable(entry, columns);
            AddIndex(entry, "ix_" + entry.RawName + "_tenant_key_time", false, "tenantid", "lockkey", "createdutc");
            AddIndex(entry, "ix_" + entry.RawName + "_tenant_time", false, "tenantid", "createdutc");
            AddIndex(entry, "ix_" + entry.RawName + "_time", false, "createdutc");
        }

        private void BuildRequestHistory()
        {
            TableCatalogEntry entry = Entry("requestHistory");
            string columns =
                "    id VARCHAR(64) NOT NULL PRIMARY KEY,\n" +
                "    tenantid VARCHAR(64) NULL,\n" +
                "    userid VARCHAR(64) NULL,\n" +
                "    principalname VARCHAR(256) NULL,\n" +
                "    method VARCHAR(16) NOT NULL DEFAULT '',\n" +
                "    path VARCHAR(2048) NOT NULL DEFAULT '',\n" +
                "    url VARCHAR(2048) NOT NULL DEFAULT '',\n" +
                "    statuscode INTEGER NOT NULL DEFAULT 0,\n" +
                "    durationms " + _Dialect.TypeDouble + " NOT NULL DEFAULT 0,\n" +
                "    sourceip VARCHAR(128) NULL,\n" +
                "    requestheaders " + _Dialect.TypeJson + " NULL,\n" +
                "    requestbody " + _Dialect.TypeText + " NULL,\n" +
                "    requestbodybytes BIGINT NOT NULL DEFAULT 0,\n" +
                Bool("requestbodytruncated", false) +
                "    responseheaders " + _Dialect.TypeJson + " NULL,\n" +
                "    responsebody " + _Dialect.TypeText + " NULL,\n" +
                "    responsebodybytes BIGINT NOT NULL DEFAULT 0,\n" +
                Bool("responsebodytruncated", false) +
                "    createdutc " + _Dialect.TypeTimestamp + " NOT NULL,\n" +
                "    completedutc " + _Dialect.TypeTimestamp + " NULL";
            AddTable(entry, columns);
            AddIndex(entry, "ix_" + entry.RawName + "_tenant_time", false, "tenantid", "createdutc");
            AddIndex(entry, "ix_" + entry.RawName + "_time", false, "createdutc");
        }

        private TableCatalogEntry Entry(string purpose)
        {
            return _Catalog.Entries.First(e => e.Purpose == purpose);
        }

        private void AddTable(TableCatalogEntry entry, string columns)
        {
            _Statements.Add(_Dialect.CreateTable(entry.Schema, entry.RawName, columns));
        }

        private void AddIndex(TableCatalogEntry entry, string indexName, bool unique, params string[] columns)
        {
            string columnsCsv = string.Join(", ", columns.Select(c => _Dialect.Quote(c)));
            _Statements.Add(_Dialect.CreateIndex(indexName, entry.Schema, entry.RawName, columnsCsv, unique));
        }

        private string Bool(string column, bool defaultValue)
        {
            return "    " + column + " " + _Dialect.TypeBool + " NOT NULL DEFAULT " + _Dialect.BooleanLiteral(defaultValue) + ",\n";
        }

        private string Ts(string column)
        {
            return "    " + column + " " + _Dialect.TypeTimestamp + " NOT NULL,\n";
        }

        private string TsLast(string column)
        {
            return "    " + column + " " + _Dialect.TypeTimestamp + " NOT NULL";
        }

        #endregion
    }
}

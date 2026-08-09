namespace Clutch.Core.Database.Postgresql.Queries
{
    using System.Collections.Generic;

    /// <summary>
    /// PostgreSQL schema setup statements and the tracked migration list. All statements are idempotent.
    /// </summary>
    public static class SetupQueries
    {
        #region Public-Members

        /// <summary>
        /// Statement that creates the migration tracking table.
        /// </summary>
        public const string CreateSchemaMigrationsTable = @"
CREATE TABLE IF NOT EXISTS schema_migrations (
    version INTEGER PRIMARY KEY,
    description TEXT NOT NULL,
    appliedutc TIMESTAMPTZ NOT NULL
);";

        /// <summary>
        /// The ordered list of migrations to apply.
        /// </summary>
        public static List<SchemaMigration> All
        {
            get
            {
                List<SchemaMigration> migrations = new List<SchemaMigration>();
                migrations.Add(new SchemaMigration(1, "Initial schema", Version1Statements()));
                return migrations;
            }
        }

        #endregion

        #region Private-Methods

        private static List<string> Version1Statements()
        {
            List<string> statements = new List<string>();

            statements.Add(@"
CREATE TABLE IF NOT EXISTS tenants (
    id VARCHAR(64) PRIMARY KEY,
    name TEXT NOT NULL,
    lockhistoryretentiondays INTEGER NOT NULL DEFAULT 7,
    defaultleasems INTEGER NOT NULL DEFAULT 30000,
    maxleasems INTEGER NOT NULL DEFAULT 300000,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);");
            statements.Add("CREATE UNIQUE INDEX IF NOT EXISTS ux_tenants_name ON tenants (name);");

            statements.Add(@"
CREATE TABLE IF NOT EXISTS users (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    firstname TEXT NOT NULL DEFAULT '',
    lastname TEXT NOT NULL DEFAULT '',
    email TEXT NOT NULL,
    passwordsha256 VARCHAR(64) NOT NULL,
    issystemadmin BOOLEAN NOT NULL DEFAULT FALSE,
    istenantadmin BOOLEAN NOT NULL DEFAULT FALSE,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);");
            statements.Add("CREATE UNIQUE INDEX IF NOT EXISTS ux_users_tenant_email ON users (tenantid, email);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_users_tenant ON users (tenantid);");

            statements.Add(@"
CREATE TABLE IF NOT EXISTS credentials (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    userid VARCHAR(64),
    name TEXT NOT NULL,
    accesskey TEXT NOT NULL,
    secretkeyencrypted TEXT NOT NULL DEFAULT '',
    secretkeylast4 VARCHAR(8) NOT NULL DEFAULT '',
    authmode TEXT NOT NULL DEFAULT 'DirectHeader',
    lastusedutc TIMESTAMPTZ,
    expiresutc TIMESTAMPTZ,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);");
            statements.Add("CREATE UNIQUE INDEX IF NOT EXISTS ux_credentials_accesskey ON credentials (accesskey);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_credentials_tenant ON credentials (tenantid);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_credentials_user ON credentials (userid);");

            statements.Add(@"
CREATE TABLE IF NOT EXISTS auth_sessions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    userid VARCHAR(64),
    credentialid VARCHAR(64),
    principaltype TEXT NOT NULL,
    tokenid VARCHAR(128) NOT NULL,
    sourceip TEXT,
    useragent TEXT,
    expiresutc TIMESTAMPTZ NOT NULL,
    lastusedutc TIMESTAMPTZ,
    revokedutc TIMESTAMPTZ,
    revocationreason TEXT,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);");
            statements.Add("CREATE UNIQUE INDEX IF NOT EXISTS ux_authsessions_tokenid ON auth_sessions (tokenid);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_authsessions_tenant ON auth_sessions (tenantid);");

            statements.Add(@"
CREATE TABLE IF NOT EXISTS lock_definitions (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    lockkey TEXT NOT NULL,
    readmaxholders INTEGER NOT NULL DEFAULT -1,
    writeexclusivity TEXT NOT NULL DEFAULT 'Exclusive',
    writemaxholders INTEGER NOT NULL DEFAULT 1,
    writeblocksreads BOOLEAN NOT NULL DEFAULT TRUE,
    defaultleasems INTEGER NOT NULL DEFAULT 30000,
    maxleasems INTEGER NOT NULL DEFAULT 300000,
    maxholdms INTEGER NOT NULL DEFAULT 3600000,
    fencingcounter BIGINT NOT NULL DEFAULT 0,
    firstacquiredbycredentialid VARCHAR(64),
    active BOOLEAN NOT NULL DEFAULT TRUE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);");
            statements.Add("CREATE UNIQUE INDEX IF NOT EXISTS ux_lockdefs_tenant_key ON lock_definitions (tenantid, lockkey);");

            statements.Add(@"
CREATE TABLE IF NOT EXISTS lock_holders (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    lockkey TEXT NOT NULL,
    lockdefinitionid VARCHAR(64) NOT NULL,
    mode TEXT NOT NULL,
    credentialid VARCHAR(64) NOT NULL DEFAULT '',
    sessionid VARCHAR(128) NOT NULL DEFAULT '',
    nodeid TEXT NOT NULL DEFAULT '',
    fencingtoken BIGINT NOT NULL DEFAULT 0,
    acquiredutc TIMESTAMPTZ NOT NULL,
    leaseexpiresutc TIMESTAMPTZ NOT NULL,
    lastheartbeatutc TIMESTAMPTZ NOT NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_lockholders_tenant_key ON lock_holders (tenantid, lockkey);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_lockholders_session ON lock_holders (sessionid);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_lockholders_lease ON lock_holders (leaseexpiresutc);");

            statements.Add(@"
CREATE TABLE IF NOT EXISTS lock_audit (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    lockkey TEXT NOT NULL,
    mode TEXT,
    eventtype TEXT NOT NULL,
    credentialid VARCHAR(64),
    sessionid VARCHAR(128),
    nodeid TEXT NOT NULL DEFAULT '',
    fencingtoken BIGINT,
    reason TEXT,
    createdutc TIMESTAMPTZ NOT NULL
);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_lockaudit_tenant_key_time ON lock_audit (tenantid, lockkey, createdutc);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_lockaudit_tenant_time ON lock_audit (tenantid, createdutc);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_lockaudit_time ON lock_audit (createdutc);");

            statements.Add(@"
CREATE TABLE IF NOT EXISTS request_history (
    id VARCHAR(64) PRIMARY KEY,
    tenantid VARCHAR(64),
    userid VARCHAR(64),
    principalname TEXT,
    method TEXT NOT NULL DEFAULT '',
    path TEXT NOT NULL DEFAULT '',
    url TEXT NOT NULL DEFAULT '',
    statuscode INTEGER NOT NULL DEFAULT 0,
    durationms DOUBLE PRECISION NOT NULL DEFAULT 0,
    sourceip TEXT,
    requestheaders JSONB,
    requestbody TEXT,
    requestbodybytes BIGINT NOT NULL DEFAULT 0,
    requestbodytruncated BOOLEAN NOT NULL DEFAULT FALSE,
    responseheaders JSONB,
    responsebody TEXT,
    responsebodybytes BIGINT NOT NULL DEFAULT 0,
    responsebodytruncated BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    completedutc TIMESTAMPTZ
);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_reqhist_tenant_time ON request_history (tenantid, createdutc);");
            statements.Add("CREATE INDEX IF NOT EXISTS ix_reqhist_time ON request_history (createdutc);");

            return statements;
        }

        #endregion
    }
}

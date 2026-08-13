-- Clutch schema for PostgreSQL.
-- Generated from Clutch.Core SchemaBuilder; keep in sync by regenerating.
-- Table names use the clutch_ default naming. Override in server settings if needed.

CREATE TABLE IF NOT EXISTS "clutch_schema_migrations" (
    version INTEGER NOT NULL PRIMARY KEY,
    description VARCHAR(1024) NOT NULL,
    appliedutc TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS "clutch_tenants" (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    name VARCHAR(256) NOT NULL,
    lockhistoryretentiondays INTEGER NOT NULL DEFAULT 7,
    defaultleasems INTEGER NOT NULL DEFAULT 30000,
    maxleasems INTEGER NOT NULL DEFAULT 300000,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "ux_clutch_tenants_name" ON "clutch_tenants" ("name");

CREATE TABLE IF NOT EXISTS "clutch_users" (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    firstname VARCHAR(256) NOT NULL DEFAULT '',
    lastname VARCHAR(256) NOT NULL DEFAULT '',
    email VARCHAR(256) NOT NULL,
    passwordsha256 VARCHAR(64) NOT NULL,
    issystemadmin BOOLEAN NOT NULL DEFAULT FALSE,
    istenantadmin BOOLEAN NOT NULL DEFAULT FALSE,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "ux_clutch_users_tenant_email" ON "clutch_users" ("tenantid", "email");

CREATE INDEX IF NOT EXISTS "ix_clutch_users_tenant" ON "clutch_users" ("tenantid");

CREATE TABLE IF NOT EXISTS "clutch_credentials" (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    userid VARCHAR(64) NULL,
    name VARCHAR(256) NOT NULL,
    accesskey VARCHAR(256) NOT NULL,
    authmode VARCHAR(64) NOT NULL DEFAULT 'DirectHeader',
    lastusedutc TIMESTAMPTZ NULL,
    expiresutc TIMESTAMPTZ NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "ux_clutch_credentials_accesskey" ON "clutch_credentials" ("accesskey");

CREATE INDEX IF NOT EXISTS "ix_clutch_credentials_tenant" ON "clutch_credentials" ("tenantid");

CREATE INDEX IF NOT EXISTS "ix_clutch_credentials_user" ON "clutch_credentials" ("userid");

CREATE TABLE IF NOT EXISTS "clutch_auth_sessions" (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    userid VARCHAR(64) NULL,
    credentialid VARCHAR(64) NULL,
    principaltype VARCHAR(64) NOT NULL,
    tokenid VARCHAR(128) NOT NULL,
    sourceip VARCHAR(128) NULL,
    useragent VARCHAR(1024) NULL,
    expiresutc TIMESTAMPTZ NOT NULL,
    lastusedutc TIMESTAMPTZ NULL,
    revokedutc TIMESTAMPTZ NULL,
    revocationreason VARCHAR(1024) NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    isprotected BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "ux_clutch_auth_sessions_tokenid" ON "clutch_auth_sessions" ("tokenid");

CREATE INDEX IF NOT EXISTS "ix_clutch_auth_sessions_tenant" ON "clutch_auth_sessions" ("tenantid");

CREATE TABLE IF NOT EXISTS "clutch_lock_definitions" (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    lockkey VARCHAR(512) NOT NULL,
    readmaxholders INTEGER NOT NULL DEFAULT -1,
    writeexclusivity VARCHAR(32) NOT NULL DEFAULT 'Exclusive',
    writemaxholders INTEGER NOT NULL DEFAULT 1,
    writeblocksreads BOOLEAN NOT NULL DEFAULT TRUE,
    defaultleasems INTEGER NOT NULL DEFAULT 30000,
    maxleasems INTEGER NOT NULL DEFAULT 300000,
    maxholdms INTEGER NOT NULL DEFAULT 3600000,
    fencingcounter BIGINT NOT NULL DEFAULT 0,
    firstacquiredbycredentialid VARCHAR(64) NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "ux_clutch_lock_definitions_tenant_key" ON "clutch_lock_definitions" ("tenantid", "lockkey");

CREATE TABLE IF NOT EXISTS "clutch_lock_holders" (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    lockkey VARCHAR(512) NOT NULL,
    lockdefinitionid VARCHAR(64) NOT NULL,
    mode VARCHAR(32) NOT NULL,
    credentialid VARCHAR(64) NOT NULL DEFAULT '',
    sessionid VARCHAR(128) NOT NULL DEFAULT '',
    nodeid VARCHAR(128) NOT NULL DEFAULT '',
    fencingtoken BIGINT NOT NULL DEFAULT 0,
    acquiredutc TIMESTAMPTZ NOT NULL,
    leaseexpiresutc TIMESTAMPTZ NOT NULL,
    lastheartbeatutc TIMESTAMPTZ NOT NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    createdutc TIMESTAMPTZ NOT NULL,
    lastupdateutc TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS "ix_clutch_lock_holders_tenant_key" ON "clutch_lock_holders" ("tenantid", "lockkey");

CREATE INDEX IF NOT EXISTS "ix_clutch_lock_holders_session" ON "clutch_lock_holders" ("sessionid");

CREATE INDEX IF NOT EXISTS "ix_clutch_lock_holders_lease" ON "clutch_lock_holders" ("leaseexpiresutc");

CREATE TABLE IF NOT EXISTS "clutch_lock_audit" (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    lockkey VARCHAR(512) NOT NULL,
    mode VARCHAR(32) NULL,
    eventtype VARCHAR(32) NOT NULL,
    credentialid VARCHAR(64) NULL,
    sessionid VARCHAR(128) NULL,
    nodeid VARCHAR(128) NOT NULL DEFAULT '',
    fencingtoken BIGINT NULL,
    reason VARCHAR(1024) NULL,
    createdutc TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS "ix_clutch_lock_audit_tenant_key_time" ON "clutch_lock_audit" ("tenantid", "lockkey", "createdutc");

CREATE INDEX IF NOT EXISTS "ix_clutch_lock_audit_tenant_time" ON "clutch_lock_audit" ("tenantid", "createdutc");

CREATE INDEX IF NOT EXISTS "ix_clutch_lock_audit_time" ON "clutch_lock_audit" ("createdutc");

CREATE TABLE IF NOT EXISTS "clutch_request_history" (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NULL,
    userid VARCHAR(64) NULL,
    principalname VARCHAR(256) NULL,
    method VARCHAR(16) NOT NULL DEFAULT '',
    path VARCHAR(2048) NOT NULL DEFAULT '',
    url VARCHAR(2048) NOT NULL DEFAULT '',
    statuscode INTEGER NOT NULL DEFAULT 0,
    durationms DOUBLE PRECISION NOT NULL DEFAULT 0,
    sourceip VARCHAR(128) NULL,
    requestheaders JSONB NULL,
    requestbody TEXT NULL,
    requestbodybytes BIGINT NOT NULL DEFAULT 0,
    requestbodytruncated BOOLEAN NOT NULL DEFAULT FALSE,
    responseheaders JSONB NULL,
    responsebody TEXT NULL,
    responsebodybytes BIGINT NOT NULL DEFAULT 0,
    responsebodytruncated BOOLEAN NOT NULL DEFAULT FALSE,
    createdutc TIMESTAMPTZ NOT NULL,
    completedutc TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS "ix_clutch_request_history_tenant_time" ON "clutch_request_history" ("tenantid", "createdutc");

CREATE INDEX IF NOT EXISTS "ix_clutch_request_history_time" ON "clutch_request_history" ("createdutc");


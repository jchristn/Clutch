-- Clutch schema for SQL Server.
-- Generated from Clutch.Core SchemaBuilder; keep in sync by regenerating.
-- Table names use the clutch_ default naming. Override in server settings if needed.

IF OBJECT_ID(N'clutch_schema_migrations', N'U') IS NULL
BEGIN
CREATE TABLE [clutch_schema_migrations] (
    version INTEGER NOT NULL PRIMARY KEY,
    description VARCHAR(1024) NOT NULL,
    appliedutc DATETIME2(7) NOT NULL
);
END;

IF OBJECT_ID(N'clutch_tenants', N'U') IS NULL
BEGIN
CREATE TABLE [clutch_tenants] (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    name VARCHAR(256) NOT NULL,
    lockhistoryretentiondays INTEGER NOT NULL DEFAULT 7,
    defaultleasems INTEGER NOT NULL DEFAULT 30000,
    maxleasems INTEGER NOT NULL DEFAULT 300000,
    active BIT NOT NULL DEFAULT 1,
    isprotected BIT NOT NULL DEFAULT 0,
    createdutc DATETIME2(7) NOT NULL,
    lastupdateutc DATETIME2(7) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ux_clutch_tenants_name' AND object_id = OBJECT_ID(N'clutch_tenants'))
BEGIN
CREATE UNIQUE INDEX [ux_clutch_tenants_name] ON [clutch_tenants] ([name]);
END;

IF OBJECT_ID(N'clutch_users', N'U') IS NULL
BEGIN
CREATE TABLE [clutch_users] (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    firstname VARCHAR(256) NOT NULL DEFAULT '',
    lastname VARCHAR(256) NOT NULL DEFAULT '',
    email VARCHAR(256) NOT NULL,
    passwordsha256 VARCHAR(64) NOT NULL,
    issystemadmin BIT NOT NULL DEFAULT 0,
    istenantadmin BIT NOT NULL DEFAULT 0,
    active BIT NOT NULL DEFAULT 1,
    isprotected BIT NOT NULL DEFAULT 0,
    createdutc DATETIME2(7) NOT NULL,
    lastupdateutc DATETIME2(7) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ux_clutch_users_tenant_email' AND object_id = OBJECT_ID(N'clutch_users'))
BEGIN
CREATE UNIQUE INDEX [ux_clutch_users_tenant_email] ON [clutch_users] ([tenantid], [email]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_users_tenant' AND object_id = OBJECT_ID(N'clutch_users'))
BEGIN
CREATE INDEX [ix_clutch_users_tenant] ON [clutch_users] ([tenantid]);
END;

IF OBJECT_ID(N'clutch_credentials', N'U') IS NULL
BEGIN
CREATE TABLE [clutch_credentials] (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    userid VARCHAR(64) NULL,
    name VARCHAR(256) NOT NULL,
    accesskey VARCHAR(256) NOT NULL,
    authmode VARCHAR(64) NOT NULL DEFAULT 'DirectHeader',
    lastusedutc DATETIME2(7) NULL,
    expiresutc DATETIME2(7) NULL,
    active BIT NOT NULL DEFAULT 1,
    isprotected BIT NOT NULL DEFAULT 0,
    createdutc DATETIME2(7) NOT NULL,
    lastupdateutc DATETIME2(7) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ux_clutch_credentials_accesskey' AND object_id = OBJECT_ID(N'clutch_credentials'))
BEGIN
CREATE UNIQUE INDEX [ux_clutch_credentials_accesskey] ON [clutch_credentials] ([accesskey]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_credentials_tenant' AND object_id = OBJECT_ID(N'clutch_credentials'))
BEGIN
CREATE INDEX [ix_clutch_credentials_tenant] ON [clutch_credentials] ([tenantid]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_credentials_user' AND object_id = OBJECT_ID(N'clutch_credentials'))
BEGIN
CREATE INDEX [ix_clutch_credentials_user] ON [clutch_credentials] ([userid]);
END;

IF OBJECT_ID(N'clutch_auth_sessions', N'U') IS NULL
BEGIN
CREATE TABLE [clutch_auth_sessions] (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    userid VARCHAR(64) NULL,
    credentialid VARCHAR(64) NULL,
    principaltype VARCHAR(64) NOT NULL,
    tokenid VARCHAR(128) NOT NULL,
    sourceip VARCHAR(128) NULL,
    useragent VARCHAR(1024) NULL,
    expiresutc DATETIME2(7) NOT NULL,
    lastusedutc DATETIME2(7) NULL,
    revokedutc DATETIME2(7) NULL,
    revocationreason VARCHAR(1024) NULL,
    active BIT NOT NULL DEFAULT 1,
    isprotected BIT NOT NULL DEFAULT 0,
    createdutc DATETIME2(7) NOT NULL,
    lastupdateutc DATETIME2(7) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ux_clutch_auth_sessions_tokenid' AND object_id = OBJECT_ID(N'clutch_auth_sessions'))
BEGIN
CREATE UNIQUE INDEX [ux_clutch_auth_sessions_tokenid] ON [clutch_auth_sessions] ([tokenid]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_auth_sessions_tenant' AND object_id = OBJECT_ID(N'clutch_auth_sessions'))
BEGIN
CREATE INDEX [ix_clutch_auth_sessions_tenant] ON [clutch_auth_sessions] ([tenantid]);
END;

IF OBJECT_ID(N'clutch_lock_definitions', N'U') IS NULL
BEGIN
CREATE TABLE [clutch_lock_definitions] (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    lockkey VARCHAR(512) NOT NULL,
    readmaxholders INTEGER NOT NULL DEFAULT -1,
    writeexclusivity VARCHAR(32) NOT NULL DEFAULT 'Exclusive',
    writemaxholders INTEGER NOT NULL DEFAULT 1,
    writeblocksreads BIT NOT NULL DEFAULT 1,
    defaultleasems INTEGER NOT NULL DEFAULT 30000,
    maxleasems INTEGER NOT NULL DEFAULT 300000,
    maxholdms INTEGER NOT NULL DEFAULT 3600000,
    fencingcounter BIGINT NOT NULL DEFAULT 0,
    firstacquiredbycredentialid VARCHAR(64) NULL,
    active BIT NOT NULL DEFAULT 1,
    createdutc DATETIME2(7) NOT NULL,
    lastupdateutc DATETIME2(7) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ux_clutch_lock_definitions_tenant_key' AND object_id = OBJECT_ID(N'clutch_lock_definitions'))
BEGIN
CREATE UNIQUE INDEX [ux_clutch_lock_definitions_tenant_key] ON [clutch_lock_definitions] ([tenantid], [lockkey]);
END;

IF OBJECT_ID(N'clutch_lock_holders', N'U') IS NULL
BEGIN
CREATE TABLE [clutch_lock_holders] (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NOT NULL,
    lockkey VARCHAR(512) NOT NULL,
    lockdefinitionid VARCHAR(64) NOT NULL,
    mode VARCHAR(32) NOT NULL,
    credentialid VARCHAR(64) NOT NULL DEFAULT '',
    sessionid VARCHAR(128) NOT NULL DEFAULT '',
    nodeid VARCHAR(128) NOT NULL DEFAULT '',
    fencingtoken BIGINT NOT NULL DEFAULT 0,
    acquiredutc DATETIME2(7) NOT NULL,
    leaseexpiresutc DATETIME2(7) NOT NULL,
    lastheartbeatutc DATETIME2(7) NOT NULL,
    active BIT NOT NULL DEFAULT 1,
    createdutc DATETIME2(7) NOT NULL,
    lastupdateutc DATETIME2(7) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_lock_holders_tenant_key' AND object_id = OBJECT_ID(N'clutch_lock_holders'))
BEGIN
CREATE INDEX [ix_clutch_lock_holders_tenant_key] ON [clutch_lock_holders] ([tenantid], [lockkey]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_lock_holders_session' AND object_id = OBJECT_ID(N'clutch_lock_holders'))
BEGIN
CREATE INDEX [ix_clutch_lock_holders_session] ON [clutch_lock_holders] ([sessionid]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_lock_holders_lease' AND object_id = OBJECT_ID(N'clutch_lock_holders'))
BEGIN
CREATE INDEX [ix_clutch_lock_holders_lease] ON [clutch_lock_holders] ([leaseexpiresutc]);
END;

IF OBJECT_ID(N'clutch_lock_audit', N'U') IS NULL
BEGIN
CREATE TABLE [clutch_lock_audit] (
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
    createdutc DATETIME2(7) NOT NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_lock_audit_tenant_key_time' AND object_id = OBJECT_ID(N'clutch_lock_audit'))
BEGIN
CREATE INDEX [ix_clutch_lock_audit_tenant_key_time] ON [clutch_lock_audit] ([tenantid], [lockkey], [createdutc]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_lock_audit_tenant_time' AND object_id = OBJECT_ID(N'clutch_lock_audit'))
BEGIN
CREATE INDEX [ix_clutch_lock_audit_tenant_time] ON [clutch_lock_audit] ([tenantid], [createdutc]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_lock_audit_time' AND object_id = OBJECT_ID(N'clutch_lock_audit'))
BEGIN
CREATE INDEX [ix_clutch_lock_audit_time] ON [clutch_lock_audit] ([createdutc]);
END;

IF OBJECT_ID(N'clutch_request_history', N'U') IS NULL
BEGIN
CREATE TABLE [clutch_request_history] (
    id VARCHAR(64) NOT NULL PRIMARY KEY,
    tenantid VARCHAR(64) NULL,
    userid VARCHAR(64) NULL,
    principalname VARCHAR(256) NULL,
    method VARCHAR(16) NOT NULL DEFAULT '',
    path VARCHAR(2048) NOT NULL DEFAULT '',
    url VARCHAR(2048) NOT NULL DEFAULT '',
    statuscode INTEGER NOT NULL DEFAULT 0,
    durationms FLOAT NOT NULL DEFAULT 0,
    sourceip VARCHAR(128) NULL,
    requestheaders NVARCHAR(MAX) NULL,
    requestbody NVARCHAR(MAX) NULL,
    requestbodybytes BIGINT NOT NULL DEFAULT 0,
    requestbodytruncated BIT NOT NULL DEFAULT 0,
    responseheaders NVARCHAR(MAX) NULL,
    responsebody NVARCHAR(MAX) NULL,
    responsebodybytes BIGINT NOT NULL DEFAULT 0,
    responsebodytruncated BIT NOT NULL DEFAULT 0,
    createdutc DATETIME2(7) NOT NULL,
    completedutc DATETIME2(7) NULL
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_request_history_tenant_time' AND object_id = OBJECT_ID(N'clutch_request_history'))
BEGIN
CREATE INDEX [ix_clutch_request_history_tenant_time] ON [clutch_request_history] ([tenantid], [createdutc]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'ix_clutch_request_history_time' AND object_id = OBJECT_ID(N'clutch_request_history'))
BEGIN
CREATE INDEX [ix_clutch_request_history_time] ON [clutch_request_history] ([createdutc]);
END;


namespace Clutch.Core.Database
{
    /// <summary>
    /// Per-purpose table name overrides. Each property is null or empty by default, which resolves to the
    /// clutch_-prefixed default name for that purpose (see <see cref="TableCatalog"/>). A non-empty value
    /// replaces the default for that purpose only. An optional <see cref="Prefix"/> is applied to every
    /// resolved name.
    /// </summary>
    public class TableNamingSettings
    {
        #region Public-Members

        /// <summary>
        /// Optional prefix applied to every resolved table name. Null or empty means no prefix.
        /// </summary>
        public string? Prefix { get; set; } = null;

        /// <summary>
        /// Override for the schema-migrations tracking table. Default clutch_schema_migrations.
        /// </summary>
        public string? SchemaMigrations { get; set; } = null;

        /// <summary>
        /// Override for the tenants table. Default clutch_tenants.
        /// </summary>
        public string? Tenants { get; set; } = null;

        /// <summary>
        /// Override for the users table. Default clutch_users.
        /// </summary>
        public string? Users { get; set; } = null;

        /// <summary>
        /// Override for the credentials table. Default clutch_credentials.
        /// </summary>
        public string? Credentials { get; set; } = null;

        /// <summary>
        /// Override for the authentication sessions table. Default clutch_auth_sessions.
        /// </summary>
        public string? AuthSessions { get; set; } = null;

        /// <summary>
        /// Override for the lock definitions table. Default clutch_lock_definitions.
        /// </summary>
        public string? LockDefinitions { get; set; } = null;

        /// <summary>
        /// Override for the lock holders table. Default clutch_lock_holders.
        /// </summary>
        public string? LockHolders { get; set; } = null;

        /// <summary>
        /// Override for the lock audit table. Default clutch_lock_audit.
        /// </summary>
        public string? LockAudit { get; set; } = null;

        /// <summary>
        /// Override for the request history table. Default clutch_request_history.
        /// </summary>
        public string? RequestHistory { get; set; } = null;

        #endregion
    }
}

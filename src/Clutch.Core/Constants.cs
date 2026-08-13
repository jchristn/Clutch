namespace Clutch.Core
{
    /// <summary>
    /// Platform-wide constants including entity identifier prefixes and internal channel names.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Identifier prefix for tenant records.
        /// </summary>
        public const string TenantPrefix = "ten_";

        /// <summary>
        /// Identifier prefix for user records.
        /// </summary>
        public const string UserPrefix = "usr_";

        /// <summary>
        /// Identifier prefix for credential (application key) records.
        /// </summary>
        public const string CredentialPrefix = "crd_";

        /// <summary>
        /// Identifier prefix for authentication session records.
        /// </summary>
        public const string AuthSessionPrefix = "ses_";

        /// <summary>
        /// Identifier prefix for lock definition records.
        /// </summary>
        public const string LockDefinitionPrefix = "lkd_";

        /// <summary>
        /// Identifier prefix for lock holder records.
        /// </summary>
        public const string LockHolderPrefix = "lkh_";

        /// <summary>
        /// Identifier prefix for lock audit records.
        /// </summary>
        public const string LockAuditPrefix = "lka_";

        /// <summary>
        /// Identifier prefix for request history records.
        /// </summary>
        public const string RequestHistoryPrefix = "req_";
    }
}

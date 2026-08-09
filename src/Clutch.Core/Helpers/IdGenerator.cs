namespace Clutch.Core.Helpers
{
    using System;
    using Clutch.Core;

    /// <summary>
    /// Generates k-sortable, prefixed application identifiers for Clutch entities.
    /// Identifiers are chronologically sortable and stay well under the 64-character storage limit.
    /// </summary>
    public static class IdGenerator
    {
        #region Private-Members

        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();
        private static readonly int _RandomLength = 32;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate a tenant identifier.
        /// </summary>
        /// <returns>Prefixed k-sortable identifier.</returns>
        public static string GenerateTenantId()
        {
            return _Generator.GenerateKSortable(Constants.TenantPrefix, _RandomLength);
        }

        /// <summary>
        /// Generate a user identifier.
        /// </summary>
        /// <returns>Prefixed k-sortable identifier.</returns>
        public static string GenerateUserId()
        {
            return _Generator.GenerateKSortable(Constants.UserPrefix, _RandomLength);
        }

        /// <summary>
        /// Generate a credential (application key) identifier.
        /// </summary>
        /// <returns>Prefixed k-sortable identifier.</returns>
        public static string GenerateCredentialId()
        {
            return _Generator.GenerateKSortable(Constants.CredentialPrefix, _RandomLength);
        }

        /// <summary>
        /// Generate an authentication session identifier.
        /// </summary>
        /// <returns>Prefixed k-sortable identifier.</returns>
        public static string GenerateAuthSessionId()
        {
            return _Generator.GenerateKSortable(Constants.AuthSessionPrefix, _RandomLength);
        }

        /// <summary>
        /// Generate a lock definition identifier.
        /// </summary>
        /// <returns>Prefixed k-sortable identifier.</returns>
        public static string GenerateLockDefinitionId()
        {
            return _Generator.GenerateKSortable(Constants.LockDefinitionPrefix, _RandomLength);
        }

        /// <summary>
        /// Generate a lock holder identifier.
        /// </summary>
        /// <returns>Prefixed k-sortable identifier.</returns>
        public static string GenerateLockHolderId()
        {
            return _Generator.GenerateKSortable(Constants.LockHolderPrefix, _RandomLength);
        }

        /// <summary>
        /// Generate a lock audit entry identifier.
        /// </summary>
        /// <returns>Prefixed k-sortable identifier.</returns>
        public static string GenerateLockAuditId()
        {
            return _Generator.GenerateKSortable(Constants.LockAuditPrefix, _RandomLength);
        }

        /// <summary>
        /// Generate a request history entry identifier.
        /// </summary>
        /// <returns>Prefixed k-sortable identifier.</returns>
        public static string GenerateRequestHistoryId()
        {
            return _Generator.GenerateKSortable(Constants.RequestHistoryPrefix, _RandomLength);
        }

        #endregion
    }
}

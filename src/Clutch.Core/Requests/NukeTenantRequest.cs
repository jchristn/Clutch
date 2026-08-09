namespace Clutch.Core.Requests
{
    /// <summary>
    /// Request to permanently destroy a tenant and every record scoped to it. System administrators only.
    /// </summary>
    public class NukeTenantRequest
    {
        #region Public-Members

        /// <summary>
        /// Identifier of the tenant to destroy.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Confirmation of the tenant identifier. Must match <see cref="TenantId"/> exactly for the operation to proceed.
        /// </summary>
        public string ConfirmTenantId { get; set; } = string.Empty;

        /// <summary>
        /// Administrative reason for the operation. Required, minimum ten characters.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Whether to also delete lock audit records for the tenant. Defaults to true.
        /// </summary>
        public bool IncludeAuditRecords { get; set; } = true;

        /// <summary>
        /// Whether to also delete request history for the tenant. Defaults to true.
        /// </summary>
        public bool IncludeRequestHistory { get; set; } = true;

        #endregion
    }
}

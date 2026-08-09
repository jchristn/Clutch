namespace Clutch.Core.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Outcome of a tenant nuke: the identifiers affected and per-entity deletion counts.
    /// </summary>
    public class NukeTenantResult
    {
        #region Public-Members

        /// <summary>
        /// Unique identifier for this operation.
        /// </summary>
        public string OperationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Identifier of the tenant that was destroyed.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the tenant that was destroyed.
        /// </summary>
        public string TenantName { get; set; } = string.Empty;

        /// <summary>
        /// Administrative reason supplied with the request.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Map of friendly entity name to the number of rows deleted.
        /// </summary>
        public Dictionary<string, long> Deleted { get; set; } = new Dictionary<string, long>();

        /// <summary>
        /// UTC time the operation started.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time the operation completed.
        /// </summary>
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;

        #endregion
    }
}

namespace Clutch.Server.Responses
{
    using System;

    /// <summary>
    /// A lease renewed by a heartbeat.
    /// </summary>
    public class RenewedLease
    {
        #region Public-Members

        /// <summary>
        /// Holder identifier.
        /// </summary>
        public string HolderId { get; set; } = string.Empty;

        /// <summary>
        /// New lease expiry (UTC).
        /// </summary>
        public DateTime LeaseExpiresUtc { get; set; } = default;

        #endregion
    }
}

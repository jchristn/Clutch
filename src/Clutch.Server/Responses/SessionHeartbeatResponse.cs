namespace Clutch.Server.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Response for a lock session heartbeat.
    /// </summary>
    public class SessionHeartbeatResponse
    {
        #region Public-Members

        /// <summary>
        /// Lock session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Renewed leases.
        /// </summary>
        public List<RenewedLease> Renewed { get; set; } = new List<RenewedLease>();

        #endregion
    }
}

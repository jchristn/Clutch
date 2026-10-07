namespace Clutch.Server.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// WebSocket frame listing the leases a heartbeat renewed.
    /// </summary>
    public class WsHeartbeatMessage
    {
        #region Public-Members

        /// <summary>
        /// Frame type.
        /// </summary>
        public string Type { get; set; } = "heartbeat";

        /// <summary>
        /// Correlating request identifier.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Renewed leases.
        /// </summary>
        public List<RenewedLease> Renewed { get; set; } = new List<RenewedLease>();

        #endregion
    }
}

namespace Clutch.Core.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// The body of a REST lock-heartbeat request. Mirrors the WebSocket "heartbeat" frame: renews the leases
    /// of one or more holders owned by the session named in the route.
    /// </summary>
    public class LockHeartbeatHttpRequest
    {
        #region Public-Members

        /// <summary>
        /// Identifiers of the holders to renew. Only holders owned by the route's session are renewed; a
        /// holder that has reached its key's maximum hold is omitted from the response.
        /// </summary>
        public List<string>? HolderIds { get; set; } = null;

        #endregion
    }
}

namespace Clutch.Server.WebSocket
{
    using System.Collections.Generic;
    using Clutch.Core.Enums;
    using Clutch.Core.Requests;

    /// <summary>
    /// A message received from a lock client over the WebSocket. The Type field discriminates the message.
    /// </summary>
    public class WsInboundMessage
    {
        #region Public-Members

        /// <summary>
        /// Message type: "acquire", "release", "heartbeat", or "ping".
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Client-supplied correlation identifier echoed on the response.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Lock key, for acquire and release.
        /// </summary>
        public string? Key { get; set; } = null;

        /// <summary>
        /// Lock mode, for acquire.
        /// </summary>
        public LockModeEnum? Mode { get; set; } = null;

        /// <summary>
        /// Acquire behavior. Defaults to fail-fast when omitted.
        /// </summary>
        public LockBehaviorEnum? Behavior { get; set; } = null;

        /// <summary>
        /// Wait timeout in milliseconds, for acquire with wait behavior.
        /// </summary>
        public int? TimeoutMs { get; set; } = null;

        /// <summary>
        /// Requested lease in milliseconds, for acquire.
        /// </summary>
        public int? LeaseMs { get; set; } = null;

        /// <summary>
        /// Policy for the key, applied only when this acquirer creates the key.
        /// </summary>
        public LockPolicySpec? Policy { get; set; } = null;

        /// <summary>
        /// Holder identifier, for release.
        /// </summary>
        public string? HolderId { get; set; } = null;

        /// <summary>
        /// Holder identifiers to renew, for heartbeat.
        /// </summary>
        public List<string>? HolderIds { get; set; } = null;

        #endregion
    }
}

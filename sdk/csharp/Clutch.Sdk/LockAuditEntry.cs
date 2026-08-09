namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// A single lock audit entry describing a lifecycle event on a key.
    /// </summary>
    public class LockAuditEntry
    {
        /// <summary>
        /// The audit entry identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// The lock key the event pertains to.
        /// </summary>
        public string? LockKey { get; set; }

        /// <summary>
        /// The lock mode associated with the event.
        /// </summary>
        public LockMode Mode { get; set; }

        /// <summary>
        /// The event type, for example "Acquired", "Released", "Waited", "Denied", "Expired", "Revoked", or "HeartbeatRenewed".
        /// </summary>
        public string? EventType { get; set; }

        /// <summary>
        /// The identifier of the credential associated with the event.
        /// </summary>
        public string? CredentialId { get; set; }

        /// <summary>
        /// The identifier of the WebSocket session associated with the event.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// The identifier of the node that recorded the event.
        /// </summary>
        public string? NodeId { get; set; }

        /// <summary>
        /// The fencing token associated with the event.
        /// </summary>
        public long FencingToken { get; set; }

        /// <summary>
        /// A human-readable reason describing the event.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// The UTC timestamp at which the event occurred.
        /// </summary>
        public DateTime CreatedUtc { get; set; }
    }
}

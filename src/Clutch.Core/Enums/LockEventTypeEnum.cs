namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The category of a lock audit event.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LockEventTypeEnum
    {
        /// <summary>
        /// A lock definition (policy) was created by the first acquirer of a key.
        /// </summary>
        PolicyCreated,

        /// <summary>
        /// A lock was granted to a holder.
        /// </summary>
        Acquired,

        /// <summary>
        /// A held lock was released.
        /// </summary>
        Released,

        /// <summary>
        /// An acquire request began waiting for availability.
        /// </summary>
        Waited,

        /// <summary>
        /// An acquire request was denied (incompatible and fail-fast, or timed out).
        /// </summary>
        Denied,

        /// <summary>
        /// A held lock expired because its lease was not renewed.
        /// </summary>
        Expired,

        /// <summary>
        /// A held lock was force-released by an administrator or because its principal was disabled.
        /// </summary>
        Revoked,

        /// <summary>
        /// A holder's lease was renewed by heartbeat.
        /// </summary>
        HeartbeatRenewed
    }
}

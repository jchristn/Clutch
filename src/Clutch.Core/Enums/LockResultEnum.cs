namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The outcome of a full acquire operation, including waiting.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LockResultEnum
    {
        /// <summary>
        /// The lock was granted.
        /// </summary>
        Granted,

        /// <summary>
        /// The lock was not available and the caller chose to fail fast.
        /// </summary>
        Denied,

        /// <summary>
        /// The caller waited but the lock did not become available before the deadline.
        /// </summary>
        Timeout,

        /// <summary>
        /// The caller requested strict policy enforcement and supplied a conflicting policy.
        /// </summary>
        PolicyConflict
    }
}

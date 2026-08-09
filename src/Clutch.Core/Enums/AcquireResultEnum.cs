namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The result of a single (non-waiting) acquire attempt against the database.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AcquireResultEnum
    {
        /// <summary>
        /// The lock was granted.
        /// </summary>
        Granted,

        /// <summary>
        /// The requested mode is incompatible with the current holders under the key's policy.
        /// </summary>
        Incompatible,

        /// <summary>
        /// The caller requested strict policy enforcement and supplied a policy conflicting with the
        /// existing definition.
        /// </summary>
        PolicyConflict
    }
}

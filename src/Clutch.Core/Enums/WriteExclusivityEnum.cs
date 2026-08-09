namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The exclusivity policy applied to write (mutating) locks on a key.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum WriteExclusivityEnum
    {
        /// <summary>
        /// At most one writer at a time.
        /// </summary>
        Exclusive,

        /// <summary>
        /// Multiple concurrent writers allowed, up to a configured maximum.
        /// </summary>
        Shared
    }
}

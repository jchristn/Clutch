namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The kind of access a lock request represents.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LockModeEnum
    {
        /// <summary>
        /// Non-mutating (read) access. Shared with other readers up to the key's configured maximum.
        /// </summary>
        Read,

        /// <summary>
        /// Mutating (write/update) access. Exclusive among writers, and blocks readers unless the key's
        /// policy allows concurrent reads during a write.
        /// </summary>
        Write,

        /// <summary>
        /// Delete access. Fully exclusive; blocks and is blocked by every other mode.
        /// </summary>
        Delete
    }
}

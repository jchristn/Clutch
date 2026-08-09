namespace Clutch.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Detail for a single lock key, including its policy definition and current holders.
    /// </summary>
    public class LockKeyDetail
    {
        /// <summary>
        /// The raw policy definition for the key, when present. Exposed as a JSON element because its shape is server-defined.
        /// </summary>
        public JsonElement? Definition { get; set; }

        /// <summary>
        /// The current holders of the key.
        /// </summary>
        public List<LockHolder> Holders { get; set; } = new List<LockHolder>();
    }
}

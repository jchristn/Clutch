namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// How an acquire request behaves when the lock is not immediately available.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LockBehaviorEnum
    {
        /// <summary>
        /// Return a denial immediately if the lock cannot be granted.
        /// </summary>
        FailFast,

        /// <summary>
        /// Wait for the lock to become available, up to the caller-supplied timeout.
        /// </summary>
        Wait
    }
}

namespace Clutch.Sdk
{
    /// <summary>
    /// The result of a force-release of every holder on a key via the REST API.
    /// </summary>
    public class ReleaseLockResult
    {
        /// <summary>
        /// The key that was released.
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// The number of holders that were released.
        /// </summary>
        public int Released { get; set; }
    }
}

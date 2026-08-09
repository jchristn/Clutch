namespace Clutch.Sdk
{
    /// <summary>
    /// A lock policy applied when a caller is the first to acquire a key and thereby creates it.
    /// On an existing key the policy is ignored; the first acquirer's policy stands.
    /// </summary>
    public class LockPolicy
    {
        /// <summary>
        /// The maximum number of concurrent read holders. A value of -1 means unlimited readers. Default is -1.
        /// </summary>
        public int ReadMaxHolders { get; set; } = -1;

        /// <summary>
        /// The write exclusivity mode, for example "Exclusive". Default is "Exclusive".
        /// </summary>
        public string WriteExclusivity { get; set; } = "Exclusive";

        /// <summary>
        /// The maximum number of concurrent write holders. Default is 1.
        /// </summary>
        public int WriteMaxHolders { get; set; } = 1;

        /// <summary>
        /// Whether a write lock blocks readers on the key. Default is true.
        /// </summary>
        public bool WriteBlocksReads { get; set; } = true;

        /// <summary>
        /// The default lease duration for the key, in milliseconds. Default is 30000.
        /// </summary>
        public int DefaultLeaseMs { get; set; } = 30000;

        /// <summary>
        /// The maximum lease duration for the key, in milliseconds. Default is 300000.
        /// </summary>
        public int MaxLeaseMs { get; set; } = 300000;

        /// <summary>
        /// The maximum total hold duration for the key, in milliseconds. Default is 3600000.
        /// </summary>
        public int MaxHoldMs { get; set; } = 3600000;
    }
}

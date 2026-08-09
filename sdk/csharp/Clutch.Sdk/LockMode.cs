namespace Clutch.Sdk
{
    /// <summary>
    /// The mode in which a lock is acquired. Determines compatibility with other holders on the same key.
    /// </summary>
    public enum LockMode
    {
        /// <summary>
        /// A shared read lock. Multiple readers may hold the key simultaneously subject to policy.
        /// </summary>
        Read,

        /// <summary>
        /// An exclusive write lock. Subject to policy, blocks other writers and optionally readers.
        /// </summary>
        Write,

        /// <summary>
        /// An exclusive delete lock, the strongest mode.
        /// </summary>
        Delete
    }
}

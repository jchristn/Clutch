namespace Clutch.Sdk
{
    /// <summary>
    /// The outcome of a lock acquisition request.
    /// </summary>
    public enum AcquireResult
    {
        /// <summary>
        /// The lock was granted.
        /// </summary>
        Acquired,

        /// <summary>
        /// A fail-fast acquire was not immediately grantable because of an incompatible holder.
        /// </summary>
        Denied,

        /// <summary>
        /// A waiting acquire reached its timeout before the lock became grantable.
        /// </summary>
        Timeout,

        /// <summary>
        /// A strict policy request conflicted with the existing key definition.
        /// </summary>
        PolicyConflict
    }
}

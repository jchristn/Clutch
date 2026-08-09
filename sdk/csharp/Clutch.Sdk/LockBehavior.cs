namespace Clutch.Sdk
{
    /// <summary>
    /// The behavior applied when a lock cannot be granted immediately.
    /// </summary>
    public enum LockBehavior
    {
        /// <summary>
        /// Return immediately with a denial if the lock cannot be granted. This is the default.
        /// </summary>
        FailFast,

        /// <summary>
        /// Wait up to the supplied timeout for the lock to become grantable.
        /// </summary>
        Wait
    }
}

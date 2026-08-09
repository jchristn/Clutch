namespace Clutch.Sdk
{
    /// <summary>
    /// Options controlling a lock acquisition request.
    /// </summary>
    public class AcquireOptions
    {
        /// <summary>
        /// The behavior applied when the lock cannot be granted immediately. Default is <see cref="LockBehavior.FailFast"/>.
        /// </summary>
        public LockBehavior Behavior { get; set; } = LockBehavior.FailFast;

        /// <summary>
        /// The maximum time to wait for the lock, in milliseconds. Applies only when <see cref="Behavior"/> is
        /// <see cref="LockBehavior.Wait"/>. When null, the server default applies.
        /// </summary>
        public int? TimeoutMs { get; set; }

        /// <summary>
        /// The requested lease duration, in milliseconds. Clamped to the key's maximum lease. When null, the key's default applies.
        /// </summary>
        public int? LeaseMs { get; set; }

        /// <summary>
        /// The policy to apply if this caller creates the key. Ignored on an existing key. When null, server defaults apply.
        /// </summary>
        public LockPolicy? Policy { get; set; }
    }
}

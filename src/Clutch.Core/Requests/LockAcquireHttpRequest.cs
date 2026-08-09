namespace Clutch.Core.Requests
{
    using Clutch.Core.Enums;

    /// <summary>
    /// The body of a REST lock-acquire request. Mirrors the fields of the WebSocket "acquire" frame so a
    /// REST client can acquire a lock exactly as a WebSocket client does. The returned session identifier
    /// must be presented on subsequent release and heartbeat calls that own the resulting holder.
    /// </summary>
    public class LockAcquireHttpRequest
    {
        #region Public-Members

        /// <summary>
        /// Requested lock mode: Read, Write, or Delete. Defaults to Read.
        /// </summary>
        public LockModeEnum Mode { get; set; } = LockModeEnum.Read;

        /// <summary>
        /// Acquire behavior: FailFast (default) returns immediately if not grantable; Wait blocks up to
        /// TimeoutMs for the lock to become available.
        /// </summary>
        public LockBehaviorEnum Behavior { get; set; } = LockBehaviorEnum.FailFast;

        /// <summary>
        /// Wait timeout in milliseconds, applied only when Behavior is Wait. Clamped to the server maximum.
        /// </summary>
        public int? TimeoutMs { get; set; } = null;

        /// <summary>
        /// Requested lease in milliseconds. Null uses the key's default lease. Clamped to the key's maximum.
        /// </summary>
        public int? LeaseMs { get; set; } = null;

        /// <summary>
        /// Owning session identifier. Omit on the first acquire to have the server assign one (returned in
        /// the response); reuse the same value to acquire additional holders under one logical session.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// Policy applied only when this acquirer creates the key. Ignored on an existing key.
        /// </summary>
        public LockPolicySpec? Policy { get; set; } = null;

        /// <summary>
        /// When true, an acquire whose policy conflicts with the existing key definition returns a
        /// policy-conflict outcome instead of using the existing policy. Defaults to false.
        /// </summary>
        public bool StrictPolicy { get; set; } = false;

        #endregion
    }
}

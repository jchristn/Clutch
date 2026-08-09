namespace Clutch.Core.Responses
{
    using System;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;

    /// <summary>
    /// The result of a full acquire operation from the lock engine, including any waiting.
    /// </summary>
    public class LockResult
    {
        #region Public-Members

        /// <summary>
        /// The outcome classification.
        /// </summary>
        public LockResultEnum Result { get; set; } = LockResultEnum.Denied;

        /// <summary>
        /// The granted holder, when Result is Granted.
        /// </summary>
        public LockHolder? Holder { get; set; } = null;

        /// <summary>
        /// The fencing token, when Result is Granted.
        /// </summary>
        public long FencingToken { get; set; } = 0;

        /// <summary>
        /// The lease expiry, when Result is Granted.
        /// </summary>
        public DateTime? LeaseExpiresUtc { get; set; } = null;

        /// <summary>
        /// Human-readable reason, when not granted.
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Number of times the acquire was attempted before returning.
        /// </summary>
        public int Attempts { get; set; } = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Whether the operation granted the lock.
        /// </summary>
        /// <returns>True when granted.</returns>
        public bool IsGranted()
        {
            return Result == LockResultEnum.Granted;
        }

        #endregion
    }
}

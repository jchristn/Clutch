namespace Clutch.Core.Responses
{
    using System;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;

    /// <summary>
    /// The result of a single transactional acquire attempt.
    /// </summary>
    public class AcquireOutcome
    {
        #region Public-Members

        /// <summary>
        /// The outcome classification.
        /// </summary>
        public AcquireResultEnum Result { get; set; } = AcquireResultEnum.Incompatible;

        /// <summary>
        /// The granted holder, when Result is Granted.
        /// </summary>
        public LockHolder? Holder { get; set; } = null;

        /// <summary>
        /// The fencing token assigned, when Result is Granted.
        /// </summary>
        public long FencingToken { get; set; } = 0;

        /// <summary>
        /// The lease expiry, when Result is Granted.
        /// </summary>
        public DateTime? LeaseExpiresUtc { get; set; } = null;

        /// <summary>
        /// The effective lock definition (policy) for the key.
        /// </summary>
        public LockDefinition? Definition { get; set; } = null;

        /// <summary>
        /// Human-readable reason, when not granted.
        /// </summary>
        public string? Reason { get; set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Whether the attempt granted the lock.
        /// </summary>
        /// <returns>True when granted.</returns>
        public bool IsGranted()
        {
            return Result == AcquireResultEnum.Granted;
        }

        #endregion
    }
}

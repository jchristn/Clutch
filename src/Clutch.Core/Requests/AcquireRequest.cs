namespace Clutch.Core.Requests
{
    using System;
    using Clutch.Core.Enums;

    /// <summary>
    /// A single attempt to acquire a lock. Carries everything the transactional acquire needs, including
    /// the optional policy applied only when the key is created by this (first) acquirer.
    /// </summary>
    public class AcquireRequest
    {
        #region Public-Members

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// Lock key.
        /// </summary>
        public string LockKey
        {
            get
            {
                return _LockKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(LockKey));
                _LockKey = value;
            }
        }

        /// <summary>
        /// Requested lock mode.
        /// </summary>
        public LockModeEnum Mode { get; set; } = LockModeEnum.Read;

        /// <summary>
        /// Acquiring credential identifier.
        /// </summary>
        public string CredentialId { get; set; } = String.Empty;

        /// <summary>
        /// Owning WebSocket session identifier.
        /// </summary>
        public string SessionId { get; set; } = String.Empty;

        /// <summary>
        /// Node performing the acquire.
        /// </summary>
        public string NodeId { get; set; } = String.Empty;

        /// <summary>
        /// Requested lease in milliseconds. Null uses the key's default lease. Clamped to the key's maximum.
        /// </summary>
        public int? RequestedLeaseMs { get; set; } = null;

        /// <summary>
        /// Policy applied when this acquirer creates the key. Null uses defaults.
        /// </summary>
        public LockPolicySpec? Policy { get; set; } = null;

        /// <summary>
        /// When true, an acquire that supplies a policy conflicting with the existing definition returns a
        /// policy-conflict outcome instead of silently using the existing policy. Defaults to false.
        /// </summary>
        public bool StrictPolicy { get; set; } = false;

        #endregion

        #region Private-Members

        private string _TenantId = String.Empty;
        private string _LockKey = String.Empty;

        #endregion
    }
}

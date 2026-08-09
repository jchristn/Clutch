namespace Clutch.Core.Models
{
    using System;
    using Clutch.Core.Enums;
    using Clutch.Core.Helpers;

    /// <summary>
    /// An active hold on a lock key by a specific WebSocket session, with a lease and fencing token.
    /// </summary>
    public class LockHolder
    {
        #region Public-Members

        /// <summary>
        /// Lock holder identifier (prefix "lkh_").
        /// </summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Owning tenant identifier.
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
        /// The lock key held.
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
        /// The lock definition identifier this holder belongs to.
        /// </summary>
        public string LockDefinitionId { get; set; } = String.Empty;

        /// <summary>
        /// The mode of the hold.
        /// </summary>
        public LockModeEnum Mode { get; set; } = LockModeEnum.Read;

        /// <summary>
        /// The credential that acquired the hold.
        /// </summary>
        public string CredentialId { get; set; } = String.Empty;

        /// <summary>
        /// The WebSocket session identifier that owns the hold. Releasing the socket releases the hold.
        /// </summary>
        public string SessionId { get; set; } = String.Empty;

        /// <summary>
        /// The node that granted the hold.
        /// </summary>
        public string NodeId { get; set; } = String.Empty;

        /// <summary>
        /// The fencing token assigned at grant time.
        /// </summary>
        public long FencingToken { get; set; } = 0;

        /// <summary>
        /// UTC timestamp the hold was acquired.
        /// </summary>
        public DateTime AcquiredUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp the lease expires unless renewed by heartbeat.
        /// </summary>
        public DateTime LeaseExpiresUtc { get; set; } = DateTime.UtcNow.AddSeconds(30);

        /// <summary>
        /// UTC timestamp of the last heartbeat.
        /// </summary>
        public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the hold is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC last update timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateLockHolderId();
        private string _TenantId = String.Empty;
        private string _LockKey = String.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LockHolder()
        {
        }

        #endregion
    }
}

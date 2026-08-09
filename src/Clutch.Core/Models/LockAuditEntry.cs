namespace Clutch.Core.Models
{
    using System;
    using Clutch.Core.Enums;
    using Clutch.Core.Helpers;

    /// <summary>
    /// An append-only record of a lock lifecycle event. Powers the audit view and the lock-activity chart.
    /// </summary>
    public class LockAuditEntry
    {
        #region Public-Members

        /// <summary>
        /// Lock audit entry identifier (prefix "lka_").
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
        /// The lock key involved.
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
        /// The lock mode involved, if applicable.
        /// </summary>
        public LockModeEnum? Mode { get; set; } = null;

        /// <summary>
        /// The event type.
        /// </summary>
        public LockEventTypeEnum EventType { get; set; } = LockEventTypeEnum.Acquired;

        /// <summary>
        /// The credential involved, if any.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// The WebSocket session involved, if any.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// The node that recorded the event.
        /// </summary>
        public string NodeId { get; set; } = String.Empty;

        /// <summary>
        /// The fencing token involved, if any.
        /// </summary>
        public long? FencingToken { get; set; } = null;

        /// <summary>
        /// Free-text reason or detail (for example a denial reason).
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// UTC timestamp the event occurred.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private string _Id = IdGenerator.GenerateLockAuditId();
        private string _TenantId = String.Empty;
        private string _LockKey = String.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LockAuditEntry()
        {
        }

        #endregion
    }
}

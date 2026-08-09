namespace Clutch.Core.Models
{
    using System;
    using Clutch.Core.Enums;
    using Clutch.Core.Helpers;

    /// <summary>
    /// The policy for a named lock key within a tenant, fixed by the first acquirer and enforced for all
    /// later acquirers. Also carries the monotonic fencing counter for the key.
    /// </summary>
    public class LockDefinition
    {
        #region Public-Members

        /// <summary>
        /// Lock definition identifier (prefix "lkd_").
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
        /// The lock key. Unique within a tenant.
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
        /// Maximum concurrent read holders. A value of -1 means unlimited. Minimum -1. Defaults to -1.
        /// </summary>
        public int ReadMaxHolders
        {
            get
            {
                return _ReadMaxHolders;
            }
            set
            {
                _ReadMaxHolders = value < -1 ? -1 : value;
            }
        }

        /// <summary>
        /// The exclusivity policy for write holders. Defaults to Exclusive.
        /// </summary>
        public WriteExclusivityEnum WriteExclusivity { get; set; } = WriteExclusivityEnum.Exclusive;

        /// <summary>
        /// Maximum concurrent write holders when WriteExclusivity is Shared. Minimum 1. Defaults to 1.
        /// </summary>
        public int WriteMaxHolders
        {
            get
            {
                return _WriteMaxHolders;
            }
            set
            {
                _WriteMaxHolders = value < 1 ? 1 : value;
            }
        }

        /// <summary>
        /// Whether a held write lock blocks new read acquisitions. Defaults to true.
        /// </summary>
        public bool WriteBlocksReads { get; set; } = true;

        /// <summary>
        /// Default lease duration in milliseconds for this key. Minimum 1000, maximum 3600000. Defaults to 30000.
        /// </summary>
        public int DefaultLeaseMs
        {
            get
            {
                return _DefaultLeaseMs;
            }
            set
            {
                _DefaultLeaseMs = Math.Clamp(value, 1000, 3600000);
            }
        }

        /// <summary>
        /// Maximum lease duration in milliseconds for this key. Minimum 1000, maximum 3600000. Defaults to 300000.
        /// </summary>
        public int MaxLeaseMs
        {
            get
            {
                return _MaxLeaseMs;
            }
            set
            {
                _MaxLeaseMs = Math.Clamp(value, 1000, 3600000);
            }
        }

        /// <summary>
        /// Maximum total hold time in milliseconds across heartbeats. Minimum 1000, maximum 86400000.
        /// Defaults to 3600000.
        /// </summary>
        public int MaxHoldMs
        {
            get
            {
                return _MaxHoldMs;
            }
            set
            {
                _MaxHoldMs = Math.Clamp(value, 1000, 86400000);
            }
        }

        /// <summary>
        /// Monotonic fencing counter. Incremented on each successful acquire; the resulting value becomes
        /// the holder's fencing token.
        /// </summary>
        public long FencingCounter { get; set; } = 0;

        /// <summary>
        /// Credential identifier of the first acquirer that created this definition, if recorded.
        /// </summary>
        public string? FirstAcquiredByCredentialId { get; set; } = null;

        /// <summary>
        /// Whether the definition is active.
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

        private string _Id = IdGenerator.GenerateLockDefinitionId();
        private string _TenantId = String.Empty;
        private string _LockKey = String.Empty;
        private int _ReadMaxHolders = -1;
        private int _WriteMaxHolders = 1;
        private int _DefaultLeaseMs = 30000;
        private int _MaxLeaseMs = 300000;
        private int _MaxHoldMs = 3600000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LockDefinition()
        {
        }

        #endregion
    }
}

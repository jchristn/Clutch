namespace Clutch.Core.Models
{
    using System;
    using Clutch.Core.Helpers;

    /// <summary>
    /// A tenant. Isolates lock namespaces, users, and application keys.
    /// </summary>
    public class Tenant
    {
        #region Public-Members

        /// <summary>
        /// Tenant identifier (prefix "ten_").
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
        /// Human-readable tenant name.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Number of days lock audit history is retained. Minimum 1, maximum 3650. Defaults to 7.
        /// </summary>
        public int LockHistoryRetentionDays
        {
            get
            {
                return _LockHistoryRetentionDays;
            }
            set
            {
                _LockHistoryRetentionDays = Math.Clamp(value, 1, 3650);
            }
        }

        /// <summary>
        /// Default lease duration in milliseconds for locks in this tenant. Minimum 1000, maximum 3600000.
        /// Defaults to 30000.
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
        /// Maximum lease duration in milliseconds for locks in this tenant. Minimum 1000, maximum 3600000.
        /// Defaults to 300000.
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
        /// Whether the tenant is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether the tenant is protected from deletion.
        /// </summary>
        public bool IsProtected { get; set; } = false;

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

        private string _Id = IdGenerator.GenerateTenantId();
        private string _Name = String.Empty;
        private int _LockHistoryRetentionDays = 7;
        private int _DefaultLeaseMs = 30000;
        private int _MaxLeaseMs = 300000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Tenant()
        {
        }

        #endregion
    }
}

namespace Clutch.Server.Settings
{
    using System;

    /// <summary>
    /// Default lock engine timing and lease settings. These are platform-wide defaults; a lock
    /// definition's first acquirer may set per-key values within these bounds.
    /// </summary>
    public class LockSettings
    {
        #region Public-Members

        /// <summary>
        /// Default lease duration in milliseconds granted to a holder when the caller does not specify one.
        /// Minimum 1000, maximum 3600000. Defaults to 30000 (30 seconds).
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
        /// Maximum lease duration in milliseconds a caller may request. Minimum 1000, maximum 3600000.
        /// Defaults to 300000 (5 minutes).
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
        /// Maximum total time in milliseconds a single holder may keep a lock across heartbeats before it
        /// must be re-acquired. Minimum 1000, maximum 86400000. Defaults to 3600000 (1 hour).
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
        /// Maximum time in milliseconds an acquire request may wait for availability. Minimum 0,
        /// maximum 600000. Defaults to 60000 (60 seconds).
        /// </summary>
        public int MaxWaitMs
        {
            get
            {
                return _MaxWaitMs;
            }
            set
            {
                _MaxWaitMs = Math.Clamp(value, 0, 600000);
            }
        }

        /// <summary>
        /// Polling interval in milliseconds at which a blocked waiter retries its acquire. This is the
        /// wakeup path for cross-node waiters; same-node waiters are signaled directly by the in-process
        /// coordinator. Minimum 50, maximum 60000. Defaults to 1000.
        /// </summary>
        public int WaiterPollMs
        {
            get
            {
                return _WaiterPollMs;
            }
            set
            {
                _WaiterPollMs = Math.Clamp(value, 50, 60000);
            }
        }

        /// <summary>
        /// Interval in milliseconds at which the lease sweeper reclaims expired holders. Minimum 100,
        /// maximum 60000. Defaults to 1000.
        /// </summary>
        public int SweepIntervalMs
        {
            get
            {
                return _SweepIntervalMs;
            }
            set
            {
                _SweepIntervalMs = Math.Clamp(value, 100, 60000);
            }
        }

        #endregion

        #region Private-Members

        private int _DefaultLeaseMs = 30000;
        private int _MaxLeaseMs = 300000;
        private int _MaxHoldMs = 3600000;
        private int _MaxWaitMs = 60000;
        private int _WaiterPollMs = 1000;
        private int _SweepIntervalMs = 1000;

        #endregion
    }
}

namespace Clutch.Core.Services
{
    using System;

    /// <summary>
    /// Timing and identity options for the lock engine.
    /// </summary>
    public class LockEngineOptions
    {
        #region Public-Members

        /// <summary>
        /// Identifier of the node running the engine, recorded on holders and audit entries.
        /// </summary>
        public string NodeId
        {
            get
            {
                return _NodeId;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(NodeId));
                _NodeId = value;
            }
        }

        /// <summary>
        /// Fallback default lease in milliseconds when neither the caller nor the key specify one.
        /// Minimum 1000, maximum 3600000. Defaults to 30000.
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
        /// Maximum time in milliseconds an acquire may wait for availability. Minimum 0, maximum 600000.
        /// Defaults to 60000.
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
        /// Polling fallback interval in milliseconds for blocked waiters. Minimum 50, maximum 60000.
        /// Defaults to 1000.
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

        #endregion

        #region Private-Members

        private string _NodeId = "node1";
        private int _DefaultLeaseMs = 30000;
        private int _MaxWaitMs = 60000;
        private int _WaiterPollMs = 1000;

        #endregion
    }
}

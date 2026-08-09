namespace Clutch.Core.Requests
{
    using Clutch.Core.Enums;
    using Clutch.Core.Models;

    /// <summary>
    /// The policy a first acquirer requests for a new lock key. Ignored when the key already exists.
    /// </summary>
    public class LockPolicySpec
    {
        #region Public-Members

        /// <summary>
        /// Maximum concurrent read holders; -1 means unlimited. Defaults to -1.
        /// </summary>
        public int ReadMaxHolders { get; set; } = -1;

        /// <summary>
        /// Write exclusivity policy. Defaults to Exclusive.
        /// </summary>
        public WriteExclusivityEnum WriteExclusivity { get; set; } = WriteExclusivityEnum.Exclusive;

        /// <summary>
        /// Maximum concurrent write holders when WriteExclusivity is Shared. Defaults to 1.
        /// </summary>
        public int WriteMaxHolders { get; set; } = 1;

        /// <summary>
        /// Whether a held write blocks new reads. Defaults to true.
        /// </summary>
        public bool WriteBlocksReads { get; set; } = true;

        /// <summary>
        /// Default lease in milliseconds for the key. Defaults to 30000.
        /// </summary>
        public int DefaultLeaseMs { get; set; } = 30000;

        /// <summary>
        /// Maximum lease in milliseconds for the key. Defaults to 300000.
        /// </summary>
        public int MaxLeaseMs { get; set; } = 300000;

        /// <summary>
        /// Maximum total hold in milliseconds for the key. Defaults to 3600000.
        /// </summary>
        public int MaxHoldMs { get; set; } = 3600000;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Project this spec onto a new lock definition for the given tenant and key.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lockKey">Lock key.</param>
        /// <param name="credentialId">First acquirer credential identifier.</param>
        /// <returns>A populated lock definition.</returns>
        public LockDefinition ToDefinition(string tenantId, string lockKey, string? credentialId)
        {
            LockDefinition definition = new LockDefinition();
            definition.TenantId = tenantId;
            definition.LockKey = lockKey;
            definition.ReadMaxHolders = ReadMaxHolders;
            definition.WriteExclusivity = WriteExclusivity;
            definition.WriteMaxHolders = WriteMaxHolders;
            definition.WriteBlocksReads = WriteBlocksReads;
            definition.DefaultLeaseMs = DefaultLeaseMs;
            definition.MaxLeaseMs = MaxLeaseMs;
            definition.MaxHoldMs = MaxHoldMs;
            definition.FirstAcquiredByCredentialId = credentialId;
            return definition;
        }

        #endregion
    }
}

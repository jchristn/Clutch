namespace Clutch.Server.Responses
{
    using System.Collections.Generic;
    using Clutch.Core.Models;

    /// <summary>
    /// A lock key's definition and current holders.
    /// </summary>
    public class LockKeyResponse
    {
        #region Public-Members

        /// <summary>
        /// Lock definition, when one exists.
        /// </summary>
        public LockDefinition? Definition { get; set; } = null;

        /// <summary>
        /// Current holders.
        /// </summary>
        public List<LockHolder> Holders { get; set; } = new List<LockHolder>();

        #endregion
    }
}

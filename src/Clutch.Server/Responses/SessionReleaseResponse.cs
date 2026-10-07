namespace Clutch.Server.Responses
{
    using System.Collections.Generic;

    /// <summary>
    /// Response for releasing every lock held by a session.
    /// </summary>
    public class SessionReleaseResponse
    {
        #region Public-Members

        /// <summary>
        /// Lock session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Released holder identifiers.
        /// </summary>
        public List<string> Released { get; set; } = new List<string>();

        /// <summary>
        /// Number of holders released.
        /// </summary>
        public int Count { get; set; } = 0;

        #endregion
    }
}

namespace Clutch.Server.Responses
{
    /// <summary>
    /// Response for a database connectivity test.
    /// </summary>
    public class DatabaseTestResponse
    {
        #region Public-Members

        /// <summary>
        /// True when the connection succeeded.
        /// </summary>
        public bool Ok { get; set; } = false;

        /// <summary>
        /// Result message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Database provider.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        #endregion
    }
}

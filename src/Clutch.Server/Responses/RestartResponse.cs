namespace Clutch.Server.Responses
{
    /// <summary>
    /// Response for a node restart request.
    /// </summary>
    public class RestartResponse
    {
        #region Public-Members

        /// <summary>
        /// True when the node is restarting.
        /// </summary>
        public bool Restarting { get; set; } = true;

        /// <summary>
        /// Node identifier.
        /// </summary>
        public string Node { get; set; } = string.Empty;

        #endregion
    }
}

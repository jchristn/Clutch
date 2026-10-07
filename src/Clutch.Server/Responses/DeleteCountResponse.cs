namespace Clutch.Server.Responses
{
    /// <summary>
    /// Response reporting how many records were deleted.
    /// </summary>
    public class DeleteCountResponse
    {
        #region Public-Members

        /// <summary>
        /// Number of records deleted.
        /// </summary>
        public int DeletedCount { get; set; } = 0;

        #endregion
    }
}

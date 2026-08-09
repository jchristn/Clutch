namespace Clutch.Core.Responses
{
    /// <summary>
    /// A standard error response body.
    /// </summary>
    public class ErrorResponse
    {
        #region Public-Members

        /// <summary>
        /// A short machine-readable error code.
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// A human-readable message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ErrorResponse()
        {
        }

        /// <summary>
        /// Instantiate with a code and message.
        /// </summary>
        /// <param name="error">Error code.</param>
        /// <param name="message">Message.</param>
        public ErrorResponse(string error, string message)
        {
            Error = error;
            Message = message;
        }

        #endregion
    }
}

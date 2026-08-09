namespace Clutch.Sdk
{
    using System;

    /// <summary>
    /// Exception thrown when a Clutch REST or WebSocket operation fails.
    /// </summary>
    public class ClutchException : Exception
    {
        private int _StatusCode;
        private string? _ResponseBody;

        /// <summary>
        /// The HTTP status code associated with the error, or 0 when not applicable (for example a transport or protocol failure).
        /// </summary>
        public int StatusCode
        {
            get
            {
                return _StatusCode;
            }
        }

        /// <summary>
        /// The raw response body associated with the error, when available; otherwise null.
        /// </summary>
        public string? ResponseBody
        {
            get
            {
                return _ResponseBody;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClutchException"/> class with a message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public ClutchException(string message) : base(message)
        {
            _StatusCode = 0;
            _ResponseBody = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClutchException"/> class with a message, status code, and response body.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="responseBody">The raw response body, when available.</param>
        public ClutchException(string message, int statusCode, string? responseBody) : base(message)
        {
            _StatusCode = statusCode;
            _ResponseBody = responseBody;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClutchException"/> class with a message and inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception that caused this error.</param>
        public ClutchException(string message, Exception innerException) : base(message, innerException)
        {
            _StatusCode = 0;
            _ResponseBody = null;
        }
    }
}

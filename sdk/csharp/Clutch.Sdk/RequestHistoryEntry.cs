namespace Clutch.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single recorded HTTP request. List responses omit header and body detail.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>
        /// The request history entry identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The tenant identifier associated with the request.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// The name of the principal that made the request.
        /// </summary>
        public string? PrincipalName { get; set; }

        /// <summary>
        /// The HTTP method.
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// The request path, without the query string.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// The full request URL, including the query string.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// The HTTP status code of the response.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// The duration of the request, in milliseconds.
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// The redacted request headers, populated on the detail endpoint.
        /// </summary>
        public Dictionary<string, string>? RequestHeaders { get; set; }

        /// <summary>
        /// The size of the request body, in bytes.
        /// </summary>
        public int RequestBodyBytes { get; set; }

        /// <summary>
        /// Whether the request body was truncated when stored.
        /// </summary>
        public bool RequestBodyTruncated { get; set; }

        /// <summary>
        /// The redacted response headers, populated on the detail endpoint.
        /// </summary>
        public Dictionary<string, string>? ResponseHeaders { get; set; }

        /// <summary>
        /// The size of the response body, in bytes.
        /// </summary>
        public int ResponseBodyBytes { get; set; }

        /// <summary>
        /// Whether the response body was truncated when stored.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; }

        /// <summary>
        /// The UTC timestamp at which the request was received.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// The UTC timestamp at which the request completed.
        /// </summary>
        public DateTime? CompletedUtc { get; set; }
    }
}

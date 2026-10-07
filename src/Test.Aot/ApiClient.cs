namespace Test.Aot
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal raw REST client. Request bodies are written with <see cref="Utf8JsonWriter"/> and responses are read
    /// with <see cref="JsonDocument"/>, so the test exercises the server's JSON without relying on reflection itself.
    /// </summary>
    internal sealed class ApiClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Bearer token sent on every request when set.
        /// </summary>
        public string? Token { get; set; } = null;

        /// <summary>
        /// Administrator API key sent as x-api-key when set.
        /// </summary>
        public string? AdminApiKey { get; set; } = null;

        #endregion

        #region Private-Members

        private readonly HttpClient _Http = new HttpClient();
        private readonly string _Endpoint;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="endpoint">Server base URL.</param>
        public ApiClient(string endpoint)
        {
            _Endpoint = endpoint.TrimEnd('/');
            _Http.Timeout = TimeSpan.FromSeconds(30);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Send a request and capture the status code and parsed JSON body.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Path and query, starting with a slash.</param>
        /// <param name="json">JSON request body, or null.</param>
        /// <returns>The response.</returns>
        public async Task<ApiResponse> SendAsync(HttpMethod method, string path, string? json = null)
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, _Endpoint + path);
            if (!string.IsNullOrEmpty(Token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            if (!string.IsNullOrEmpty(AdminApiKey)) request.Headers.TryAddWithoutValidation("x-api-key", AdminApiKey);
            if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _Http.SendAsync(request).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new ApiResponse((int)response.StatusCode, body);
        }

        /// <summary>
        /// Build a JSON object body.
        /// </summary>
        /// <param name="write">Writes the object's properties.</param>
        /// <returns>The JSON text.</returns>
        public static string Body(Action<Utf8JsonWriter> write)
        {
            using MemoryStream stream = new MemoryStream();
            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                write(writer);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Release the HTTP client.
        /// </summary>
        public void Dispose()
        {
            _Http.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// A captured REST response.
    /// </summary>
    internal sealed class ApiResponse
    {
        #region Public-Members

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int Status { get; }

        /// <summary>
        /// Raw response body.
        /// </summary>
        public string Body { get; }

        /// <summary>
        /// Parsed JSON body (undefined when the body is empty or not JSON).
        /// </summary>
        public JsonElement Json { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="status">HTTP status code.</param>
        /// <param name="body">Raw response body.</param>
        public ApiResponse(int status, string body)
        {
            Status = status;
            Body = body;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(body);
                    Json = doc.RootElement.Clone();
                }
                catch (JsonException)
                {
                    Json = default;
                }
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Read a string property of the root object.
        /// </summary>
        /// <param name="name">Property name.</param>
        /// <returns>The value, or null.</returns>
        public string? String(string name)
        {
            return Json.ValueKind == JsonValueKind.Object && Json.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        /// <summary>
        /// Describe the response for failure messages.
        /// </summary>
        /// <returns>Status and body.</returns>
        public override string ToString()
        {
            return Status + " " + Body;
        }

        #endregion
    }
}

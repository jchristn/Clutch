namespace Test.Shared
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal MCP streamable-HTTP client used by the MCP server test cases. It speaks raw JSON-RPC so the
    /// assertions see exactly what an MCP client receives on the wire (results, error codes, empty objects).
    /// </summary>
    internal sealed class McpTestClient : IDisposable
    {
        #region Private-Members

        private const string _ProtocolVersion = "2025-11-25";

        private readonly HttpClient _Http = new HttpClient();
        private readonly string _Url;
        private string? _SessionId;
        private int _NextId = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="url">Full URL of the MCP endpoint, for example http://127.0.0.1:8100/mcp.</param>
        public McpTestClient(string url)
        {
            _Url = url ?? throw new ArgumentNullException(nameof(url));
            _Http.Timeout = TimeSpan.FromSeconds(30);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Perform the MCP handshake (initialize, then notifications/initialized), retrying while the server
        /// is still starting up.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task InitializeAsync(CancellationToken ct)
        {
            object initParams = new
            {
                protocolVersion = _ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "clutch-tests", version = "1.0" }
            };

            Exception? last = null;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    JsonElement response = await SendAsync("initialize", initParams, ct).ConfigureAwait(false);
                    if (response.TryGetProperty("error", out _)) throw new Exception("initialize failed: " + response.GetRawText());
                    await PostAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "notifications/initialized" }), ct).ConfigureAwait(false);
                    return;
                }
                catch (HttpRequestException e)
                {
                    last = e;
                    await Task.Delay(100, ct).ConfigureAwait(false);
                }
            }

            throw new Exception("MCP server did not accept connections: " + last?.Message);
        }

        /// <summary>
        /// Send a JSON-RPC request and return the full response envelope.
        /// </summary>
        /// <param name="method">Method name.</param>
        /// <param name="parameters">Parameters, or null to omit them.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The JSON-RPC response object.</returns>
        public async Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken ct)
        {
            int id = Interlocked.Increment(ref _NextId);
            string body = parameters == null
                ? JsonSerializer.Serialize(new { jsonrpc = "2.0", id = id, method = method })
                : JsonSerializer.Serialize(new { jsonrpc = "2.0", id = id, method = method, @params = parameters });

            string payload = await PostAsync(body, ct).ConfigureAwait(false);
            using JsonDocument doc = JsonDocument.Parse(payload);
            return doc.RootElement.Clone();
        }

        /// <summary>
        /// Call a tool through tools/call and return the concatenated text of its content blocks. Throws when
        /// the call fails with a JSON-RPC error or a tool error result.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Text content returned by the tool.</returns>
        public async Task<string> CallToolTextAsync(string name, object arguments, CancellationToken ct)
        {
            JsonElement response = await SendAsync("tools/call", new { name = name, arguments = arguments }, ct).ConfigureAwait(false);
            if (response.TryGetProperty("error", out _)) throw new Exception("tools/call " + name + " failed: " + response.GetRawText());

            JsonElement result = response.GetProperty("result");
            if (result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True)
                throw new Exception("tools/call " + name + " returned a tool error: " + result.GetRawText());

            return string.Concat(result.GetProperty("content").EnumerateArray()
                .Where(c => c.TryGetProperty("text", out _))
                .Select(c => c.GetProperty("text").GetString()));
        }

        /// <summary>
        /// Release the HTTP client.
        /// </summary>
        public void Dispose()
        {
            _Http.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task<string> PostAsync(string body, CancellationToken ct)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _Url);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
            request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", _ProtocolVersion);
            if (_SessionId != null) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _SessionId);

            using HttpResponseMessage response = await _Http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.Headers.TryGetValues("Mcp-Session-Id", out System.Collections.Generic.IEnumerable<string>? values))
                _SessionId = values.FirstOrDefault() ?? _SessionId;

            string text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                // A single-response SSE stream: the JSON-RPC message is carried on the data lines.
                text = string.Join("\n", text.Split('\n')
                    .Select(l => l.TrimEnd('\r'))
                    .Where(l => l.StartsWith("data:", StringComparison.Ordinal))
                    .Select(l => l.Substring(5).TrimStart()));
            }

            return text;
        }

        #endregion
    }
}

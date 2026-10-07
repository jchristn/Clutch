namespace Test.Aot
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using static Clutch.Sdk.Test.SdkChecks;

    /// <summary>
    /// MCP checks over streamable HTTP: the handshake, tools/list (each tool's input schema), and tools/call for
    /// every Clutch tool, including a tool error. Requests are raw JSON-RPC text and responses are read with
    /// <see cref="JsonDocument"/>.
    /// </summary>
    internal static class McpChecks
    {
        #region Private-Members

        private const string _ProtocolVersion = "2025-11-25";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Run the MCP checks.
        /// </summary>
        /// <param name="host">The running node.</param>
        /// <param name="tenantId">The default tenant's identifier.</param>
        /// <returns>A task that completes when every check has run.</returns>
        public static async Task RunAsync(AotServerHost host, string tenantId)
        {
            using McpSession mcp = new McpSession(host.McpUrl);

            await CheckAsync("MCP: initialize handshake", async () =>
            {
                JsonElement response = await mcp.InitializeAsync().ConfigureAwait(false);
                Assert(response.GetProperty("result").TryGetProperty("serverInfo", out _), response.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("MCP: tools/list publishes the Clutch tools with their schemas", async () =>
            {
                JsonElement response = await mcp.SendAsync("tools/list", "{}").ConfigureAwait(false);
                JsonElement tools = response.GetProperty("result").GetProperty("tools");
                List<string> names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString() ?? string.Empty).OrderBy(n => n, StringComparer.Ordinal).ToList();
                Assert(string.Join(",", names) == "clutch_list_locks,clutch_list_tenants,clutch_lock_audit,clutch_server_info", "tools: " + string.Join(",", names));
                foreach (JsonElement tool in tools.EnumerateArray())
                {
                    Assert(tool.GetProperty("inputSchema").GetProperty("type").GetString() == "object", "input schema should be an object: " + tool.GetRawText());
                }
                JsonElement locks = tools.EnumerateArray().First(t => t.GetProperty("name").GetString() == "clutch_list_locks");
                Assert(locks.GetProperty("inputSchema").GetProperty("required")[0].GetString() == "tenantId", locks.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("MCP: clutch_server_info returns the node", async () =>
            {
                JsonElement result = await mcp.CallToolAsync("clutch_server_info", "{}").ConfigureAwait(false);
                Assert(result.GetProperty("nodeId").GetString() == "aot-node" && result.GetProperty("product").GetString() == "Clutch", result.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("MCP: clutch_list_tenants returns tenants", async () =>
            {
                JsonElement result = await mcp.CallToolAsync("clutch_list_tenants", "{\"maxResults\":10}").ConfigureAwait(false);
                Assert(result.GetProperty("objects").GetArrayLength() >= 1, result.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("MCP: clutch_list_locks returns a page", async () =>
            {
                JsonElement result = await mcp.CallToolAsync("clutch_list_locks", "{\"tenantId\":\"" + tenantId + "\"}").ConfigureAwait(false);
                Assert(result.TryGetProperty("objects", out _), result.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("MCP: clutch_lock_audit returns audit entries", async () =>
            {
                JsonElement result = await mcp.CallToolAsync("clutch_lock_audit", "{\"tenantId\":\"" + tenantId + "\",\"maxResults\":5}").ConfigureAwait(false);
                Assert(result.GetProperty("objects").GetArrayLength() > 0, result.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("MCP: a missing required argument is rejected", async () =>
            {
                JsonElement response = await mcp.SendAsync("tools/call", "{\"name\":\"clutch_list_locks\",\"arguments\":{}}").ConfigureAwait(false);
                bool rpcError = response.TryGetProperty("error", out _);
                bool toolError = response.TryGetProperty("result", out JsonElement result)
                    && result.TryGetProperty("isError", out JsonElement isError)
                    && isError.ValueKind == JsonValueKind.True;
                Assert(rpcError || toolError, "expected an error: " + response.GetRawText());
            }).ConfigureAwait(false);
        }

        #endregion

        #region Private-Classes

        private sealed class McpSession : IDisposable
        {
            private readonly HttpClient _Http = new HttpClient();
            private readonly string _Url;
            private string? _SessionId;
            private int _NextId = 0;

            public McpSession(string url)
            {
                _Url = url;
                _Http.Timeout = TimeSpan.FromSeconds(30);
            }

            public async Task<JsonElement> InitializeAsync()
            {
                string parameters = "{\"protocolVersion\":\"" + _ProtocolVersion + "\",\"capabilities\":{},\"clientInfo\":{\"name\":\"clutch-aot\",\"version\":\"1.0\"}}";
                Exception? last = null;
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    try
                    {
                        JsonElement response = await SendAsync("initialize", parameters).ConfigureAwait(false);
                        if (response.TryGetProperty("error", out _)) throw new InvalidOperationException("initialize failed: " + response.GetRawText());
                        await PostAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}").ConfigureAwait(false);
                        return response;
                    }
                    catch (HttpRequestException e)
                    {
                        last = e;
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                }
                throw new InvalidOperationException("the MCP server did not accept connections: " + last?.Message);
            }

            public async Task<JsonElement> SendAsync(string method, string parametersJson)
            {
                int id = Interlocked.Increment(ref _NextId);
                string body = "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"" + method + "\",\"params\":" + parametersJson + "}";
                string payload = await PostAsync(body).ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(payload);
                return doc.RootElement.Clone();
            }

            public async Task<JsonElement> CallToolAsync(string name, string argumentsJson)
            {
                JsonElement response = await SendAsync("tools/call", "{\"name\":\"" + name + "\",\"arguments\":" + argumentsJson + "}").ConfigureAwait(false);
                if (response.TryGetProperty("error", out _)) throw new InvalidOperationException("tools/call " + name + " failed: " + response.GetRawText());
                JsonElement result = response.GetProperty("result");
                if (result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True)
                    throw new InvalidOperationException("tools/call " + name + " returned a tool error: " + result.GetRawText());

                string text = string.Concat(result.GetProperty("content").EnumerateArray()
                    .Where(c => c.TryGetProperty("text", out _))
                    .Select(c => c.GetProperty("text").GetString()));
                using JsonDocument doc = JsonDocument.Parse(text);
                return doc.RootElement.Clone();
            }

            public void Dispose()
            {
                _Http.Dispose();
            }

            private async Task<string> PostAsync(string body)
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _Url);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
                request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", _ProtocolVersion);
                if (_SessionId != null) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _SessionId);

                using HttpResponseMessage response = await _Http.SendAsync(request).ConfigureAwait(false);
                if (response.Headers.TryGetValues("Mcp-Session-Id", out IEnumerable<string>? values))
                    _SessionId = values.FirstOrDefault() ?? _SessionId;

                string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                string? mediaType = response.Content.Headers.ContentType?.MediaType;
                if (string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
                {
                    text = string.Join("\n", text.Split('\n')
                        .Select(l => l.TrimEnd('\r'))
                        .Where(l => l.StartsWith("data:", StringComparison.Ordinal))
                        .Select(l => l.Substring(5).TrimStart()));
                }
                return text;
            }
        }

        #endregion
    }
}

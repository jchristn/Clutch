namespace Test.Aot
{
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Services;
    using static Clutch.Sdk.Test.SdkChecks;

    /// <summary>
    /// Raw WebSocket checks for the frames the SDK checks do not reach: ping/pong and the error frames.
    /// </summary>
    internal static class WebSocketChecks
    {
        #region Public-Methods

        /// <summary>
        /// Run the WebSocket checks.
        /// </summary>
        /// <param name="host">The running node.</param>
        /// <returns>A task that completes when every check has run.</returns>
        public static async Task RunAsync(AotServerHost host)
        {
            using ClientWebSocket socket = new ClientWebSocket();
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await CheckAsync("WebSocket: connect and receive the welcome frame", async () =>
            {
                socket.Options.SetRequestHeader("x-clutch-access-key", DefaultSeeder.DefaultAccessKey);
                string url = "ws://" + host.Endpoint.Substring("http://".Length) + "/v1.0/lock/connect";
                await socket.ConnectAsync(new Uri(url), cts.Token).ConfigureAwait(false);
                JsonElement welcome = await ReceiveAsync(socket, cts.Token).ConfigureAwait(false);
                Assert(Type(welcome) == "welcome", "expected welcome: " + welcome.GetRawText());
                Assert(welcome.GetProperty("heartbeatIntervalMs").GetInt32() > 0 && welcome.GetProperty("defaultLeaseMs").GetInt32() > 0, welcome.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("WebSocket: ping is answered with pong", async () =>
            {
                await SendAsync(socket, "{\"type\":\"ping\"}", cts.Token).ConfigureAwait(false);
                JsonElement pong = await ReceiveAsync(socket, cts.Token).ConfigureAwait(false);
                Assert(Type(pong) == "pong", pong.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("WebSocket: an unknown message type returns an error frame", async () =>
            {
                await SendAsync(socket, "{\"type\":\"bogus\",\"requestId\":\"r-1\"}", cts.Token).ConfigureAwait(false);
                JsonElement error = await ReceiveAsync(socket, cts.Token).ConfigureAwait(false);
                Assert(Type(error) == "error" && error.GetProperty("requestId").GetString() == "r-1", error.GetRawText());
                Assert(error.GetProperty("message").GetString()!.Contains("bogus", StringComparison.Ordinal), error.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("WebSocket: an acquire without a key returns an error frame", async () =>
            {
                await SendAsync(socket, "{\"type\":\"acquire\",\"requestId\":\"r-2\",\"mode\":\"Write\"}", cts.Token).ConfigureAwait(false);
                JsonElement error = await ReceiveAsync(socket, cts.Token).ConfigureAwait(false);
                Assert(Type(error) == "error" && error.GetProperty("requestId").GetString() == "r-2", error.GetRawText());
            }).ConfigureAwait(false);

            await CheckAsync("WebSocket: release without a holder returns an error frame", async () =>
            {
                await SendAsync(socket, "{\"type\":\"release\",\"requestId\":\"r-3\"}", cts.Token).ConfigureAwait(false);
                JsonElement error = await ReceiveAsync(socket, cts.Token).ConfigureAwait(false);
                Assert(Type(error) == "error" && error.GetProperty("requestId").GetString() == "r-3", error.GetRawText());
            }).ConfigureAwait(false);

            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token).ConfigureAwait(false);
            }
            catch
            {
                // best effort
            }
        }

        #endregion

        #region Private-Methods

        private static string? Type(JsonElement frame)
        {
            return frame.TryGetProperty("type", out JsonElement type) ? type.GetString() : null;
        }

        private static async Task SendAsync(ClientWebSocket socket, string json, CancellationToken token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
        }

        private static async Task<JsonElement> ReceiveAsync(ClientWebSocket socket, CancellationToken token)
        {
            byte[] buffer = new byte[16384];
            StringBuilder text = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("the server closed the connection");
                text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);

            using JsonDocument doc = JsonDocument.Parse(text.ToString());
            return doc.RootElement.Clone();
        }

        #endregion
    }
}

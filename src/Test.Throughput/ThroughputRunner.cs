namespace Test.Throughput
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Drives concurrent WebSocket lock clients against a running Clutch endpoint and reports throughput.
    /// </summary>
    public class ThroughputRunner
    {
        #region Private-Members

        private static readonly string[] _ModeNames = { "Read", "Write", "Delete" };

        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Run the load and print a report.
        /// </summary>
        /// <param name="endpoint">HTTP(S) base endpoint, for example http://localhost:8080.</param>
        /// <param name="accessKey">Application access key. This is the sole connect credential.</param>
        /// <param name="threads">Number of emulated clients.</param>
        /// <param name="durationSeconds">Load duration in seconds.</param>
        /// <param name="keyCount">Size of the shared key namespace.</param>
        /// <param name="seed">RNG seed for reproducibility.</param>
        /// <returns>Awaitable task.</returns>
        public async Task RunAsync(string endpoint, string accessKey, int threads, int durationSeconds, int keyCount, int seed)
        {
            string wsUrl = BuildWebSocketUrl(endpoint, accessKey);

            string[] keys = new string[keyCount];
            for (int i = 0; i < keyCount; i++) keys[i] = "throughput-key-" + i;

            ClientResult[] results = new ClientResult[threads];
            Console.WriteLine("Connecting " + threads + " clients to " + Redact(wsUrl) + " ...");
            Console.WriteLine("Running for " + durationSeconds + "s over " + keyCount + " keys (seed " + seed + ") ...");

            Stopwatch wall = Stopwatch.StartNew();
            DateTime deadline = DateTime.UtcNow.AddSeconds(durationSeconds);

            List<Task> clients = new List<Task>();
            for (int t = 0; t < threads; t++)
            {
                int index = t;
                ClientResult result = new ClientResult();
                results[index] = result;
                clients.Add(Task.Run(() => RunClientAsync(wsUrl, keys, result, deadline, seed + index)));
            }

            await Task.WhenAll(clients).ConfigureAwait(false);
            wall.Stop();

            PrintReport(results, threads, keyCount, wall.Elapsed.TotalSeconds);
        }

        #endregion

        #region Private-Methods

        private static async Task RunClientAsync(string wsUrl, string[] keys, ClientResult result, DateTime deadline, int seed)
        {
            Random rng = new Random(seed);
            byte[] buffer = new byte[8192];

            try
            {
                using (ClientWebSocket socket = new ClientWebSocket())
                {
                    using (CancellationTokenSource connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        await socket.ConnectAsync(new Uri(wsUrl), connectCts.Token).ConfigureAwait(false);
                    }

                    // Consume the welcome frame.
                    await ReceiveMessageAsync(socket, buffer, CancellationToken.None).ConfigureAwait(false);

                    long requestCounter = 0;
                    while (DateTime.UtcNow < deadline)
                    {
                        int keyIndex = rng.Next(keys.Length);
                        int modeIndex = rng.Next(3);
                        string key = keys[keyIndex];
                        string mode = _ModeNames[modeIndex];
                        requestCounter++;

                        result.AcquireAttempts[modeIndex] = result.AcquireAttempts[modeIndex] + 1;
                        long startTicks = Stopwatch.GetTimestamp();
                        await SendAsync(socket, "{\"type\":\"acquire\",\"requestId\":\"" + requestCounter + "\",\"key\":\"" + key + "\",\"mode\":\"" + mode + "\",\"behavior\":\"FailFast\"}").ConfigureAwait(false);
                        string response = await ReceiveMessageAsync(socket, buffer, CancellationToken.None).ConfigureAwait(false);
                        result.AcquireLatencyMs.Add(Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds);

                        string type = ReadType(response, out string? holderId);
                        if (type == "acquired")
                        {
                            result.AcquireGranted[modeIndex] = result.AcquireGranted[modeIndex] + 1;
                            if (!string.IsNullOrEmpty(holderId))
                            {
                                await SendAsync(socket, "{\"type\":\"release\",\"requestId\":\"" + requestCounter + "r\",\"key\":\"" + key + "\",\"holderId\":\"" + holderId + "\"}").ConfigureAwait(false);
                                await ReceiveMessageAsync(socket, buffer, CancellationToken.None).ConfigureAwait(false);
                                result.Releases = result.Releases + 1;
                            }
                        }
                        else if (type == "denied")
                        {
                            result.AcquireDenied[modeIndex] = result.AcquireDenied[modeIndex] + 1;
                        }
                        else
                        {
                            result.Errors = result.Errors + 1;
                        }
                    }

                    using (CancellationTokenSource closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", closeCts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception)
            {
                result.Errors = result.Errors + 1;
            }
        }

        private static async Task SendAsync(ClientWebSocket socket, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task<string> ReceiveMessageAsync(ClientWebSocket socket, byte[] buffer, CancellationToken token)
        {
            StringBuilder builder = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return string.Empty;
                builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            }
            while (!result.EndOfMessage);
            return builder.ToString();
        }

        private static string ReadType(string json, out string? holderId)
        {
            holderId = null;
            if (string.IsNullOrEmpty(json)) return string.Empty;
            try
            {
                ThroughputMessage? message = JsonSerializer.Deserialize<ThroughputMessage>(json, _JsonOptions);
                if (message == null) return string.Empty;
                holderId = message.HolderId;
                return message.Type ?? string.Empty;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static string BuildWebSocketUrl(string endpoint, string accessKey)
        {
            string trimmed = endpoint.TrimEnd('/');
            string scheme = trimmed.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            string hostPart = trimmed;
            int schemeIndex = trimmed.IndexOf("://", StringComparison.Ordinal);
            if (schemeIndex >= 0) hostPart = trimmed.Substring(schemeIndex + 3);
            return scheme + "://" + hostPart + "/v1.0/lock/connect?accessKey=" + Uri.EscapeDataString(accessKey);
        }

        private static string Redact(string url)
        {
            int keyIndex = url.IndexOf("accessKey=", StringComparison.Ordinal);
            return keyIndex >= 0 ? url.Substring(0, keyIndex) + "accessKey=***" : url;
        }

        private static void PrintReport(ClientResult[] results, int threads, int keyCount, double runtimeSeconds)
        {
            long totalAcquires = 0;
            long totalGranted = 0;
            long totalDenied = 0;
            long totalReleases = 0;
            long totalErrors = 0;
            long[] attemptsByMode = new long[3];
            long[] grantedByMode = new long[3];
            long[] deniedByMode = new long[3];
            List<double> latencies = new List<double>();

            foreach (ClientResult result in results)
            {
                for (int m = 0; m < 3; m++)
                {
                    attemptsByMode[m] += result.AcquireAttempts[m];
                    grantedByMode[m] += result.AcquireGranted[m];
                    deniedByMode[m] += result.AcquireDenied[m];
                    totalAcquires += result.AcquireAttempts[m];
                    totalGranted += result.AcquireGranted[m];
                    totalDenied += result.AcquireDenied[m];
                }
                totalReleases += result.Releases;
                totalErrors += result.Errors;
                latencies.AddRange(result.AcquireLatencyMs);
            }

            long totalOps = totalAcquires + totalReleases;
            double opsPerSecond = runtimeSeconds > 0 ? totalOps / runtimeSeconds : 0;
            double acquiresPerSecond = runtimeSeconds > 0 ? totalAcquires / runtimeSeconds : 0;
            double releasesPerSecond = runtimeSeconds > 0 ? totalReleases / runtimeSeconds : 0;

            latencies.Sort();

            Console.WriteLine();
            Console.WriteLine("======================= Clutch Throughput =======================");
            Console.WriteLine(Pad("Emulated clients", 26) + threads);
            Console.WriteLine(Pad("Key namespace", 26) + keyCount);
            Console.WriteLine(Pad("Runtime", 26) + runtimeSeconds.ToString("F2") + " s");
            Console.WriteLine(Pad("Total operations", 26) + totalOps);
            Console.WriteLine(Pad("Throughput", 26) + opsPerSecond.ToString("F0") + " ops/sec");
            Console.WriteLine(Pad("  acquires", 26) + acquiresPerSecond.ToString("F0") + " /sec");
            Console.WriteLine(Pad("  releases", 26) + releasesPerSecond.ToString("F0") + " /sec");
            Console.WriteLine(Pad("Errors", 26) + totalErrors);
            Console.WriteLine();
            Console.WriteLine("Request distribution by type");
            Console.WriteLine("  " + Pad("Type", 16) + Pad("Count", 12) + Pad("Percent", 10) + Pad("Granted", 10) + "Denied");
            for (int m = 0; m < 3; m++)
            {
                double pct = totalAcquires > 0 ? (100.0 * attemptsByMode[m] / totalAcquires) : 0;
                Console.WriteLine("  " + Pad(_ModeNames[m] + " acquire", 16) + Pad(attemptsByMode[m].ToString(), 12) + Pad(pct.ToString("F1") + "%", 10) + Pad(grantedByMode[m].ToString(), 10) + deniedByMode[m]);
            }
            Console.WriteLine("  " + Pad("Release", 16) + Pad(totalReleases.ToString(), 12) + Pad("-", 10) + Pad("-", 10) + "-");
            Console.WriteLine();
            Console.WriteLine("Acquire latency (ms)");
            Console.WriteLine("  " + Pad("p50", 8) + Percentile(latencies, 0.50).ToString("F2"));
            Console.WriteLine("  " + Pad("p95", 8) + Percentile(latencies, 0.95).ToString("F2"));
            Console.WriteLine("  " + Pad("p99", 8) + Percentile(latencies, 0.99).ToString("F2"));
            Console.WriteLine("=================================================================");
        }

        private static double Percentile(List<double> sorted, double percentile)
        {
            if (sorted.Count == 0) return 0;
            int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
            if (index < 0) index = 0;
            if (index >= sorted.Count) index = sorted.Count - 1;
            return sorted[index];
        }

        private static string Pad(string value, int width)
        {
            if (value.Length >= width) return value + " ";
            return value + new string(' ', width - value.Length);
        }

        #endregion
    }
}

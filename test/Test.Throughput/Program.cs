namespace Test.Throughput
{
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Clutch throughput benchmark. Optionally spins up the Docker stack, drives concurrent WebSocket
    /// lock clients for a fixed duration, and reports operations per second and a request distribution.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">
        /// --duration &lt;sec&gt; --threads &lt;n&gt; --endpoint &lt;url&gt; --access-key &lt;k&gt; --secret-key &lt;k&gt;
        /// --keys &lt;n&gt; --seed &lt;n&gt; --compose &lt;path&gt; --no-docker
        /// </param>
        /// <returns>Process exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            int duration = IntArg(args, "--duration", 30);
            int threads = IntArg(args, "--threads", 16);
            string endpoint = StringArg(args, "--endpoint", "http://localhost:8080");
            string accessKey = StringArg(args, "--access-key", "clutch-default-access-key");
            string? secretKey = HasArg(args, "--secret-key") ? StringArg(args, "--secret-key", "") : null;
            int keys = IntArg(args, "--keys", 32);
            int seed = IntArg(args, "--seed", 20260808);
            string composeFile = StringArg(args, "--compose", "docker/compose.yaml");
            bool noDocker = HasArg(args, "--no-docker");

            bool startedDocker = false;
            try
            {
                if (!noDocker)
                {
                    Console.WriteLine("Starting Docker stack (" + composeFile + ") ...");
                    if (!RunDocker("compose -f \"" + composeFile + "\" up -d"))
                    {
                        Console.WriteLine("Failed to start Docker stack. Provide --no-docker to target an already-running endpoint.");
                        return 2;
                    }
                    startedDocker = true;
                }

                if (!await WaitForHealthAsync(endpoint, 120).ConfigureAwait(false))
                {
                    Console.WriteLine("Endpoint " + endpoint + " did not become healthy.");
                    return 3;
                }

                ThroughputRunner runner = new ThroughputRunner();
                await runner.RunAsync(endpoint, accessKey, secretKey, threads, duration, keys, seed).ConfigureAwait(false);
                return 0;
            }
            finally
            {
                if (startedDocker)
                {
                    Console.WriteLine("Stopping Docker stack ...");
                    RunDocker("compose -f \"" + composeFile + "\" down");
                }
            }
        }

        private static async Task<bool> WaitForHealthAsync(string endpoint, int timeoutSeconds)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(endpoint.TrimEnd('/') + "/v1.0/api/health").ConfigureAwait(false);
                        if (response.IsSuccessStatusCode) return true;
                    }
                    catch (Exception)
                    {
                        // Not up yet.
                    }
                    await Task.Delay(2000).ConfigureAwait(false);
                }
                return false;
            }
        }

        private static bool RunDocker(string arguments)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "docker";
                info.Arguments = arguments;
                info.UseShellExecute = false;
                using (Process? process = Process.Start(info))
                {
                    if (process == null) return false;
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string StringArg(string[] args, string name, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            }
            return fallback;
        }

        private static int IntArg(string[] args, string name, int fallback)
        {
            string value = StringArg(args, name, string.Empty);
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int parsed)) return parsed;
            return fallback;
        }
    }
}

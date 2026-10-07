namespace Test.Aot
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Clutch.Core.Services;
    using Clutch.Sdk.Test;
    using Clutch.Server.Settings;

    /// <summary>
    /// Native AOT smoke test. Publish as a native binary (dotnet publish -c Release -f net10.0 -r &lt;rid&gt;) and run
    /// it: it starts a full Clutch node in-process on a temporary SQLite database, then exercises the settings file,
    /// REST (through the SDK and raw requests), the WebSocket lock protocol, OpenAPI, and MCP. Exit code 0 means
    /// every check passed. A plain build runs the same checks under the JIT with reflection-based JSON disabled.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  Clutch Native AOT Test");
            Console.WriteLine("========================================");
            Console.WriteLine("Native AOT       : " + (!RuntimeFeature.IsDynamicCodeCompiled));
            Console.WriteLine("Reflection JSON  : " + JsonSerializer.IsReflectionEnabledByDefault);
            Console.WriteLine();

            string workDirectory = Path.Combine(Path.GetTempPath(), "clutch-aot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDirectory);
            AotServerHost? host = null;

            try
            {
                await SdkChecks.CheckAsync("Settings: defaults are written and read back with string enums", () =>
                {
                    string file = Path.Combine(workDirectory, "defaults.json");
                    ClutchSettings defaults = ClutchSettings.FromFile(file);
                    string text = File.ReadAllText(file);
                    SdkChecks.Assert(text.Contains("\"Type\": \"Postgresql\"", StringComparison.Ordinal), "the settings file should name the database type: " + text);
                    ClutchSettings reread = ClutchSettings.FromFile(file);
                    SdkChecks.Assert(reread.Rest.Port == defaults.Rest.Port && reread.NodeId == defaults.NodeId, "settings should round-trip");
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

                host = await AotServerHost.StartAsync(workDirectory).ConfigureAwait(false);
                Console.WriteLine("Node             : " + host.Endpoint + " (MCP " + host.McpUrl + ")");
                Console.WriteLine();

                await RestChecks.RunAsync(host).ConfigureAwait(false);
                await SdkChecks.RunAsync(host.Endpoint, DefaultSeeder.DefaultAccessKey).ConfigureAwait(false);
                await WebSocketChecks.RunAsync(host).ConfigureAwait(false);

                using Clutch.Sdk.ClutchAdminClient admin = new Clutch.Sdk.ClutchAdminClient(host.Endpoint);
                string tenantId = (await admin.AuthenticateWithKeyAsync(DefaultSeeder.DefaultAccessKey).ConfigureAwait(false)).TenantId ?? string.Empty;
                await McpChecks.RunAsync(host, tenantId).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                SdkChecks.RecordFailure("FATAL: " + e);
            }
            finally
            {
                host?.Dispose();
                try
                {
                    Directory.Delete(workDirectory, true);
                }
                catch
                {
                    // best effort; SQLite may still hold the file briefly
                }
            }

            int total = SdkChecks.Passed + SdkChecks.Failed;
            Console.WriteLine();
            Console.WriteLine("Total: " + total + "  Passed: " + SdkChecks.Passed + "  Failed: " + SdkChecks.Failed);
            return SdkChecks.Failed == 0 && total > 0 ? 0 : 1;
        }
    }
}

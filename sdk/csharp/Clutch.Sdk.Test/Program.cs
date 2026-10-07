namespace Clutch.Sdk.Test
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Non-interactive test application for the Clutch SDK. Exercises the admin (REST) and lock (WebSocket) clients,
    /// asserts expected behavior, and exits with code 0 on success or 1 on failure.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: Clutch.Sdk.Test <endpoint> <accessKey>");
                Console.WriteLine("Example: Clutch.Sdk.Test http://127.0.0.1:8090 clutch-default-access-key");
                return 1;
            }

            string endpoint = args[0];
            string accessKey = args[1];

            Console.WriteLine("========================================");
            Console.WriteLine("  Clutch SDK Test Application");
            Console.WriteLine("========================================");
            Console.WriteLine($"Endpoint : {endpoint}");
            Console.WriteLine($"AccessKey: {accessKey}");
            Console.WriteLine();

            try
            {
                await SdkChecks.RunAsync(endpoint, accessKey).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                SdkChecks.RecordFailure($"FATAL: {ex.GetType().Name}: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine($"  Passed: {SdkChecks.Passed}   Failed: {SdkChecks.Failed}");
            Console.WriteLine("========================================");
            return SdkChecks.Failed == 0 ? 0 : 1;
        }
    }
}

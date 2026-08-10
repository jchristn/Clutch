namespace Clutch.DataLoader
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Entry point for the Clutch synthetic data loader.</summary>
    public static class Program
    {
        /// <summary>Run the loader.</summary>
        /// <param name="args">Command-line arguments (see <c>--help</c>).</param>
        /// <returns>0 on success, 1 on a usage error, 2 on cancellation.</returns>
        public static async Task<int> Main(string[] args)
        {
            LoaderOptions options;
            try
            {
                options = LoaderOptions.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                Console.Error.WriteLine("Run with --help for usage.");
                return 1;
            }

            if (options.ShowHelp)
            {
                Console.WriteLine(LoaderOptions.HelpText);
                return 0;
            }

            try
            {
                options.Resolve();
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 1;
            }

            using CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            Action<string> log = message => { if (!options.Quiet) Console.WriteLine(message); };

            if (!options.Quiet)
            {
                Console.WriteLine("Clutch.DataLoader");
                Console.WriteLine("  seed=" + options.Seed + "  load=" + options.Load + "  window=" + options.FromUtc.ToString("yyyy-MM-dd HH:mm") + "Z -> " + options.ToUtc.ToString("yyyy-MM-dd HH:mm") + "Z");
            }

            try
            {
                SyntheticLoader loader = new SyntheticLoader(options, log);
                LoadResult result = await loader.RunAsync(cts.Token).ConfigureAwait(false);

                Console.WriteLine();
                Console.WriteLine("Done.");
                if (options.PurgeOnly)
                {
                    Console.WriteLine("  Purged " + result.Purged + " synthetic row(s).");
                }
                else if (!options.DryRun)
                {
                    if (result.Purged > 0) Console.WriteLine("  Purged:        " + result.Purged);
                    Console.WriteLine("  Tenants:       " + result.Tenants);
                    Console.WriteLine("  Users:         " + result.Users);
                    Console.WriteLine("  Credentials:   " + result.Credentials);
                    Console.WriteLine("  Lock audit:    " + result.AuditEvents);
                    Console.WriteLine("  Request rows:  " + result.Requests);
                    Console.WriteLine("  Active locks:  " + result.ActiveLocks);
                    Console.WriteLine("  Window:        " + result.FromUtc.ToString("u") + " -> " + result.ToUtc.ToString("u"));
                }
                return 0;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Cancelled.");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Load failed: " + ex.Message);
                if (options.Verbose) Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}

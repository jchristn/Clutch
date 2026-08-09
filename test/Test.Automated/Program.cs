namespace Test.Automated
{
    using System;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Automated test runner for the Clutch shared suites.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command-line arguments. Supports "--results &lt;path&gt;" to export JSON results.</param>
        /// <returns>Process exit code: 0 on success, non-zero on failure.</returns>
        public static async Task<int> Main(string[] args)
        {
            return await ConsoleRunner.RunAsync(
                ClutchSuites.GetSuites(),
                resultsPath: ParseResultsPath(args)).ConfigureAwait(false);
        }

        private static string? ParseResultsPath(string[] args)
        {
            if (args == null || args.Length < 2) return null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (String.Equals(args[i], "--results", StringComparison.Ordinal)) return args[i + 1];
            }
            return null;
        }
    }
}

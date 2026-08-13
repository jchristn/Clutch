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
        /// <param name="args">
        /// Command-line arguments. Supports database selection (<c>--type</c>, <c>--host</c>, <c>--port</c>,
        /// <c>--database</c>, <c>--schema</c>, <c>--username</c>, <c>--password</c>, <c>--filepath</c>,
        /// <c>--providers</c>), <c>--results &lt;path&gt;</c> to export JSON results, and <c>--help</c>.
        /// </param>
        /// <returns>Process exit code: 0 on success, non-zero on failure.</returns>
        public static async Task<int> Main(string[] args)
        {
            DatabaseCliOptions options;

            try
            {
                options = DatabaseCliOptions.Parse(args);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine(DatabaseCliOptions.UsageText());
                return 2;
            }

            if (options.ShowHelp)
            {
                Console.WriteLine(DatabaseCliOptions.UsageText());
                return 0;
            }

            options.ApplyToEnvironment();

            return await ConsoleRunner.RunAsync(
                ClutchSuites.GetSuites(),
                resultsPath: options.ResultsPath).ConfigureAwait(false);
        }
    }
}

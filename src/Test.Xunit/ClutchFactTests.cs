namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// Runs every Clutch shared suite descriptor sequentially through the Touchstone executor in a single
    /// xUnit fact, honoring suite lifecycle and order.
    /// </summary>
    public sealed class ClutchFactTests : TouchstoneFactBase
    {
        /// <summary>
        /// The shared suites.
        /// </summary>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return ClutchSuites.GetSuites(); }
        }

        /// <summary>
        /// Run all descriptors.
        /// </summary>
        /// <returns>Awaitable task.</returns>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}

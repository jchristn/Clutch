namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs every Clutch shared suite descriptor through the Touchstone executor in a single NUnit test.
    /// </summary>
    [TestFixture]
    public sealed class ClutchNunitFactTests : TouchstoneNunitBase
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
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}

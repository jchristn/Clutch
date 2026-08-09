namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Exposes each Clutch shared descriptor as a separate NUnit test case for per-test visibility.
    /// </summary>
    [TestFixture]
    public sealed class ClutchNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(ClutchSuites.GetSuites());
        }

        /// <summary>
        /// Run a single descriptor.
        /// </summary>
        /// <param name="testCase">Descriptor to run.</param>
        /// <returns>Awaitable task.</returns>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}

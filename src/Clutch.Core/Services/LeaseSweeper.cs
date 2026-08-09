namespace Clutch.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;

    /// <summary>
    /// Background service that periodically reclaims holders whose lease has expired and signals local
    /// waiters on the affected keys. This is the safety net for keys with no active contention; contended
    /// keys are also self-healed inside each acquire transaction.
    /// </summary>
    public class LeaseSweeper
    {
        #region Private-Members

        private readonly ILockHolderMethods _Holders;
        private readonly LockCoordinator _Coordinator;
        private readonly string _NodeId;
        private readonly int _IntervalMs;
        private CancellationTokenSource? _Cts;
        private Task? _Loop;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="holders">Lock holder methods.</param>
        /// <param name="coordinator">Waiter coordinator.</param>
        /// <param name="nodeId">Node identifier for audit.</param>
        /// <param name="intervalMs">Sweep interval in milliseconds. Minimum 100.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public LeaseSweeper(ILockHolderMethods holders, LockCoordinator coordinator, string nodeId, int intervalMs)
        {
            _Holders = holders ?? throw new ArgumentNullException(nameof(holders));
            _Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            if (string.IsNullOrEmpty(nodeId)) throw new ArgumentNullException(nameof(nodeId));
            _NodeId = nodeId;
            _IntervalMs = intervalMs < 100 ? 100 : intervalMs;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the sweep loop.
        /// </summary>
        /// <param name="token">Cancellation token linked to the loop lifetime.</param>
        public void Start(CancellationToken token = default)
        {
            _Cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Loop = Task.Run(() => RunAsync(_Cts.Token));
        }

        /// <summary>
        /// Stop the sweep loop.
        /// </summary>
        /// <returns>Awaitable task.</returns>
        public async Task StopAsync()
        {
            if (_Cts != null) _Cts.Cancel();
            if (_Loop != null)
            {
                try
                {
                    await _Loop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            if (_Cts != null) _Cts.Dispose();
        }

        /// <summary>
        /// Run a single sweep pass immediately.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of keys affected.</returns>
        public async Task<int> SweepOnceAsync(CancellationToken token = default)
        {
            List<string> keys = await _Holders.PurgeExpiredAsync(DateTime.UtcNow, _NodeId, token).ConfigureAwait(false);
            foreach (string composite in keys) _Coordinator.SignalComposite(composite);
            return keys.Count;
        }

        #endregion

        #region Private-Methods

        private async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_IntervalMs, token).ConfigureAwait(false);
                    await SweepOnceAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Swallow transient sweep failures; the next interval retries.
                }
            }
        }

        #endregion
    }
}

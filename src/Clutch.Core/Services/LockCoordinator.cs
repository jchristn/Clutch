namespace Clutch.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Per-node registry of blocked waiters, keyed by tenant and lock key. When a key is signaled by a
    /// local release, revoke, or expiry, all waiters on that key are released early to retry. This is a
    /// same-node optimization only; cross-node waiters wake via bounded polling (WaiterPollMs). This state
    /// is purely an optimization; the database remains the sole authority for grants.
    /// </summary>
    public class LockCoordinator
    {
        #region Private-Members

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, TaskCompletionSource<bool>>> _Waiters =
            new ConcurrentDictionary<string, ConcurrentDictionary<long, TaskCompletionSource<bool>>>();
        private long _WaiterSequence = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// The current number of blocked waiters across all keys on this node.
        /// </summary>
        public int WaiterCount
        {
            get
            {
                int total = 0;
                foreach (ConcurrentDictionary<long, TaskCompletionSource<bool>> bag in _Waiters.Values) total += bag.Count;
                return total;
            }
        }

        /// <summary>
        /// Wait until the given key is signaled or the timeout elapses, whichever comes first.
        /// </summary>
        /// <param name="tenantKey">Composite tenant and key value.</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task that completes on signal, timeout, or cancellation.</returns>
        public async Task WaitAsync(string tenantKey, int timeoutMs, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(tenantKey)) throw new ArgumentNullException(nameof(tenantKey));
            if (timeoutMs <= 0) return;

            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            long id = Interlocked.Increment(ref _WaiterSequence);
            ConcurrentDictionary<long, TaskCompletionSource<bool>> bag = _Waiters.GetOrAdd(tenantKey, _ => new ConcurrentDictionary<long, TaskCompletionSource<bool>>());
            bag[id] = completion;

            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                Task delay = Task.Delay(timeoutMs, linked.Token);
                try
                {
                    Task finished = await Task.WhenAny(completion.Task, delay).ConfigureAwait(false);
                    if (finished == delay)
                    {
                        // Timed out or cancelled; nothing else to do.
                    }
                }
                catch (TaskCanceledException)
                {
                    // Cancellation requested.
                }
                finally
                {
                    linked.Cancel();
                    bag.TryRemove(id, out _);
                    if (bag.IsEmpty) _Waiters.TryRemove(tenantKey, out _);
                }
            }
        }

        /// <summary>
        /// Signal all waiters on a key so they retry.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="lockKey">Lock key.</param>
        public void SignalKey(string tenantId, string lockKey)
        {
            SignalComposite(tenantId + "|" + lockKey);
        }

        /// <summary>
        /// Signal all waiters on a composite tenant and key value.
        /// </summary>
        /// <param name="tenantKey">Composite tenant and key value.</param>
        public void SignalComposite(string tenantKey)
        {
            if (string.IsNullOrEmpty(tenantKey)) return;
            if (_Waiters.TryGetValue(tenantKey, out ConcurrentDictionary<long, TaskCompletionSource<bool>>? bag))
            {
                foreach (TaskCompletionSource<bool> completion in bag.Values)
                {
                    completion.TrySetResult(true);
                }
            }
        }

        #endregion
    }
}

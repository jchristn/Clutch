namespace Clutch.Sdk.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Sdk;

    /// <summary>
    /// The SDK checks: exercises the admin (REST) and lock (WebSocket) clients against a running server and
    /// counts passes and failures. Shared by the SDK test application and the Native AOT test (Test.Aot), which
    /// compiles this file into its native binary.
    /// </summary>
    internal static class SdkChecks
    {
        #region Public-Members

        /// <summary>
        /// Number of checks that passed.
        /// </summary>
        public static int Passed
        {
            get
            {
                return _Passed;
            }
        }

        /// <summary>
        /// Number of checks that failed.
        /// </summary>
        public static int Failed
        {
            get
            {
                return _Failed;
            }
        }

        #endregion

        #region Private-Members

        private static int _Passed;
        private static int _Failed;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Run every SDK check against a running server.
        /// </summary>
        /// <param name="endpoint">Server base URL, for example http://127.0.0.1:8080.</param>
        /// <param name="accessKey">An application access key for the default tenant.</param>
        /// <returns>A task that completes when every check has run.</returns>
        public static async Task RunAsync(string endpoint, string accessKey)
        {
            using ClutchAdminClient admin = new ClutchAdminClient(endpoint);

            // ---- Admin (REST) ----

            await CheckAsync("Health check is healthy", async () =>
            {
                HealthResponse health = await admin.GetHealthAsync().ConfigureAwait(false);
                Assert(!string.IsNullOrEmpty(health.Status), "status should be present");
            }).ConfigureAwait(false);

            await CheckAsync("Authenticate with application key", async () =>
            {
                TokenResponse token = await admin.AuthenticateWithKeyAsync(accessKey).ConfigureAwait(false);
                Assert(!string.IsNullOrEmpty(token.Token), "token should be returned");
                Assert(!string.IsNullOrEmpty(token.TenantId), "tenantId should be returned");
            }).ConfigureAwait(false);

            string tenantId = string.Empty;
            await CheckAsync("Get token details", async () =>
            {
                TokenDetails details = await admin.GetTokenDetailsAsync().ConfigureAwait(false);
                Assert(details.Authenticated, "principal should be authenticated");
                Assert(!string.IsNullOrEmpty(details.TenantId), "tenantId should be present");
                tenantId = details.TenantId!;
            }).ConfigureAwait(false);

            await CheckAsync("Get server info", async () =>
            {
                ServerInfo info = await admin.GetServerInfoAsync().ConfigureAwait(false);
                Assert(string.Equals(info.Product, "Clutch", StringComparison.OrdinalIgnoreCase), $"product should be Clutch, was '{info.Product}'");
            }).ConfigureAwait(false);

            await CheckAsync("List tenants includes at least one tenant", async () =>
            {
                List<Tenant> tenants = (await admin.ListTenantsAsync().ConfigureAwait(false)).Objects;
                Assert(tenants.Count >= 1, "expected at least one tenant");
            }).ConfigureAwait(false);

            string createdTenantId = string.Empty;
            await CheckAsync("Create, read, and delete a tenant", async () =>
            {
                string name = "sdk-test-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                Tenant created = await admin.CreateTenantAsync(name, 7, 30000, 300000).ConfigureAwait(false);
                Assert(!string.IsNullOrEmpty(created.Id), "created tenant should have an id");
                createdTenantId = created.Id!;

                Tenant fetched = await admin.GetTenantAsync(createdTenantId).ConfigureAwait(false);
                Assert(string.Equals(fetched.Name, name, StringComparison.Ordinal), "fetched tenant name should match");

                await admin.DeleteTenantAsync(createdTenantId).ConfigureAwait(false);
                createdTenantId = string.Empty;
            }).ConfigureAwait(false);

            await CheckAsync("Create, list, and delete a credential", async () =>
            {
                string credName = "sdk-test-cred-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                Credential created = await admin.CreateCredentialAsync(tenantId, credName).ConfigureAwait(false);
                Assert(!string.IsNullOrEmpty(created.Id), "created credential should have an id");
                Assert(!string.IsNullOrEmpty(created.AccessKey), "created credential should return an access key");

                List<Credential> credentials = (await admin.ListCredentialsAsync(tenantId).ConfigureAwait(false)).Objects;
                Assert(credentials.Exists(c => c.Id == created.Id), "created credential should appear in the list");

                await admin.DeleteCredentialAsync(tenantId, created.Id!).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CheckAsync("List active locks (REST observability)", async () =>
            {
                List<LockHolder> holders = (await admin.ListLocksAsync(tenantId).ConfigureAwait(false)).Objects;
                Assert(holders != null, "locks listing should not be null");
            }).ConfigureAwait(false);

            await CheckAsync("Read lock audit page", async () =>
            {
                EnumerationResult<LockAuditEntry> audit = await admin.GetLockAuditAsync(tenantId, maxResults: 5).ConfigureAwait(false);
                Assert(audit != null, "audit page should not be null");
            }).ConfigureAwait(false);

            await CheckAsync("Read request-history summary", async () =>
            {
                RequestHistorySummary summary = await admin.GetRequestHistorySummaryAsync(bucketMinutes: 60).ConfigureAwait(false);
                Assert(summary != null, "summary should not be null");
                Assert(summary!.TotalCount >= 0, "total count should be non-negative");
            }).ConfigureAwait(false);

            // ---- Lock (WebSocket) ----

            using ClutchLockClient locks = new ClutchLockClient(endpoint, accessKey);

            await CheckAsync("Connect lock client and receive welcome", async () =>
            {
                WelcomeInfo welcome = await locks.ConnectAsync().ConfigureAwait(false);
                Assert(!string.IsNullOrEmpty(welcome.SessionId), "welcome should include a session id");
                Assert(welcome.HeartbeatIntervalMs > 0, "welcome should include a heartbeat interval");
            }).ConfigureAwait(false);

            string writeKey = "sdk-test/" + Guid.NewGuid().ToString("N");
            long firstToken = 0;
            await CheckAsync("Acquire and release a write lock; obtain a fencing token", async () =>
            {
                AcquiredLock acquired = await locks.AcquireAsync(writeKey, LockMode.Write).ConfigureAwait(false);
                Assert(!string.IsNullOrEmpty(acquired.HolderId), "acquired lock should have a holder id");
                Assert(acquired.FencingToken >= 1, "fencing token should be at least 1");
                Assert(acquired.Mode == LockMode.Write, "mode should be Write");
                firstToken = acquired.FencingToken;

                bool released = await locks.ReleaseAsync(acquired.HolderId).ConfigureAwait(false);
                Assert(released, "release should report true");
            }).ConfigureAwait(false);

            await CheckAsync("Fencing token increases on re-acquire of the same key", async () =>
            {
                AcquiredLock acquired = await locks.AcquireAsync(writeKey, LockMode.Write).ConfigureAwait(false);
                Assert(acquired.FencingToken > firstToken, $"fencing token should increase (was {firstToken}, got {acquired.FencingToken})");
                await locks.ReleaseAsync(acquired.HolderId).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CheckAsync("Multiple readers may share a key", async () =>
            {
                string readKey = "sdk-test-read/" + Guid.NewGuid().ToString("N");
                AcquiredLock r1 = await locks.AcquireAsync(readKey, LockMode.Read).ConfigureAwait(false);
                AcquiredLock r2 = await locks.AcquireAsync(readKey, LockMode.Read).ConfigureAwait(false);
                Assert(!string.IsNullOrEmpty(r1.HolderId) && !string.IsNullOrEmpty(r2.HolderId), "both read holders should be granted");
                Assert(r1.HolderId != r2.HolderId, "two read holders should be distinct");
                await locks.ReleaseAsync(r1.HolderId).ConfigureAwait(false);
                await locks.ReleaseAsync(r2.HolderId).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CheckAsync("A write lock denies a fail-fast read on the same key", async () =>
            {
                string key = "sdk-test-conflict/" + Guid.NewGuid().ToString("N");
                AcquiredLock w = await locks.AcquireAsync(key, LockMode.Write).ConfigureAwait(false);
                try
                {
                    LockDeniedException denied = await AssertThrowsAsync<LockDeniedException>(
                        () => locks.AcquireAsync(key, LockMode.Read, new AcquireOptions { Behavior = LockBehavior.FailFast })).ConfigureAwait(false);
                    Assert(denied.Result == AcquireResult.Denied, $"result should be Denied, was {denied.Result}");
                }
                finally
                {
                    await locks.ReleaseAsync(w.HolderId).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            await CheckAsync("A waiting read times out while a write is held", async () =>
            {
                string key = "sdk-test-timeout/" + Guid.NewGuid().ToString("N");
                AcquiredLock w = await locks.AcquireAsync(key, LockMode.Write).ConfigureAwait(false);
                try
                {
                    LockDeniedException denied = await AssertThrowsAsync<LockDeniedException>(
                        () => locks.AcquireAsync(key, LockMode.Read, new AcquireOptions { Behavior = LockBehavior.Wait, TimeoutMs = 500 })).ConfigureAwait(false);
                    Assert(denied.Result == AcquireResult.Timeout, $"result should be Timeout, was {denied.Result}");
                }
                finally
                {
                    await locks.ReleaseAsync(w.HolderId).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            await CheckAsync("Heartbeat renews a held lease", async () =>
            {
                string key = "sdk-test-hb/" + Guid.NewGuid().ToString("N");
                TaskCompletionSource<bool> renewed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                void Handler(object? sender, IReadOnlyList<AcquiredLock> list) => renewed.TrySetResult(true);
                locks.HeartbeatReceived += Handler;
                AcquiredLock acquired = await locks.AcquireAsync(key, LockMode.Write).ConfigureAwait(false);
                try
                {
                    await locks.HeartbeatAsync(new[] { acquired.HolderId }).ConfigureAwait(false);
                    Task completed = await Task.WhenAny(renewed.Task, Task.Delay(3000)).ConfigureAwait(false);
                    Assert(completed == renewed.Task, "expected a heartbeat renewal frame within 3 seconds");
                }
                finally
                {
                    locks.HeartbeatReceived -= Handler;
                    await locks.ReleaseAsync(acquired.HolderId).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            await CheckAsync("Close the lock connection", async () =>
            {
                await locks.CloseAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CheckAsync("Logout", async () =>
            {
                await admin.LogoutAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Run one named check, counting and printing its outcome.
        /// </summary>
        /// <param name="name">Check name.</param>
        /// <param name="action">Check body; throwing fails the check.</param>
        /// <returns>A task that completes when the check has run.</returns>
        public static async Task CheckAsync(string name, Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
                _Passed++;
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception ex)
            {
                _Failed++;
                Console.WriteLine($"[FAIL] {name}");
                Console.WriteLine($"       {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Throw when a condition is false.
        /// </summary>
        /// <param name="condition">Condition that must hold.</param>
        /// <param name="message">Failure message.</param>
        public static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Run an action and return the exception of the expected type it throws.
        /// </summary>
        /// <typeparam name="T">Expected exception type.</typeparam>
        /// <param name="action">Action expected to throw.</param>
        /// <returns>The thrown exception.</returns>
        public static async Task<T> AssertThrowsAsync<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (T expected)
            {
                return expected;
            }
            catch (Exception other)
            {
                throw new InvalidOperationException($"Expected {typeof(T).Name} but caught {other.GetType().Name}: {other.Message}");
            }
            throw new InvalidOperationException($"Expected {typeof(T).Name} but no exception was thrown.");
        }

        /// <summary>
        /// Count a failure that happened outside a named check.
        /// </summary>
        /// <param name="message">Failure description.</param>
        public static void RecordFailure(string message)
        {
            _Failed++;
            Console.WriteLine($"[FAIL] {message}");
        }

        #endregion
    }
}

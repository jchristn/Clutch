namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Services;
    using Clutch.Server.Services;
    using Touchstone.Core;

    /// <summary>
    /// Clutch shared test suites: pure compatibility logic plus a database-backed matrix that runs the lock
    /// engine correctness, tenant isolation, polling-wakeup, and randomized soak suites once per available
    /// provider. SQLite runs in-process and is always available; PostgreSQL, MySQL, and SQL Server run when
    /// their connection details are supplied and the provider is listed in CLUTCH_TEST_PROVIDERS.
    /// </summary>
    public static class ClutchSuites
    {
        #region Private-Members

        private static bool _Initialized = false;
        private static readonly List<ProviderContext> _Providers = new List<ProviderContext>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get the shared Touchstone test suites.
        /// </summary>
        /// <returns>Test suite descriptors.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> GetSuites()
        {
            InitProviders();

            List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>();

            // Pure compatibility logic (no database), run once.
            List<TestCaseDescriptor> compat = new List<TestCaseDescriptor>();
            compat.Add(SyncCase("compat", "compat-read-shared", "Compat: reads are shared", CompatReadShared));
            compat.Add(SyncCase("compat", "compat-read-max", "Compat: reader maximum enforced", CompatReadMax));
            compat.Add(SyncCase("compat", "compat-write-blocks-reads", "Compat: write blocks reads by policy", CompatWriteBlocksReads));
            compat.Add(SyncCase("compat", "compat-write-exclusive", "Compat: writes are exclusive", CompatWriteExclusive));
            compat.Add(SyncCase("compat", "compat-write-shared", "Compat: shared writers up to max", CompatWriteShared));
            compat.Add(SyncCase("compat", "compat-delete-exclusive", "Compat: delete is fully exclusive", CompatDeleteExclusive));
            suites.Add(new TestSuiteDescriptor("compat", "Clutch Compatibility Suite", compat));

            // MCP tool-argument parsing (no database), run once.
            List<TestCaseDescriptor> mcp = new List<TestCaseDescriptor>();
            mcp.Add(SyncCase("mcp-args", "mcp-getstring-present", "MCP: GetString reads string and numeric values", McpGetStringPresent));
            mcp.Add(SyncCase("mcp-args", "mcp-getstring-absent", "MCP: GetString returns empty for missing/invalid input", McpGetStringAbsent));
            mcp.Add(SyncCase("mcp-args", "mcp-getint-present", "MCP: GetInt parses numbers and numeric strings", McpGetIntPresent));
            mcp.Add(SyncCase("mcp-args", "mcp-getint-absent", "MCP: GetInt returns null for missing/non-numeric input", McpGetIntAbsent));
            mcp.Add(SyncCase("mcp-args", "mcp-buildquery-defaults", "MCP: BuildQuery uses defaults when args are absent", McpBuildQueryDefaults));
            mcp.Add(SyncCase("mcp-args", "mcp-buildquery-values", "MCP: BuildQuery reads and clamps paging values", McpBuildQueryValues));
            suites.Add(new TestSuiteDescriptor("mcp-args", "Clutch MCP Argument Suite", mcp));

            // Database-backed matrix, one suite per provider.
            foreach (ProviderContext provider in _Providers)
            {
                suites.Add(new TestSuiteDescriptor(provider.SuiteId, "Clutch " + provider.Name + " Suite", BuildDatabaseCases(provider)));
            }

            return suites;
        }

        #endregion

        #region Case-Builder

        private static List<TestCaseDescriptor> BuildDatabaseCases(ProviderContext provider)
        {
            bool skip = !provider.Available;
            string reason = provider.Name + " not available (" + provider.Hint + ").";
            string suiteId = provider.SuiteId;

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            cases.Add(DbCase(suiteId, "engine-read-shared", "Engine: two reads share a key", provider, EngineReadSharedAsync, skip, reason));
            cases.Add(DbCase(suiteId, "engine-read-max-policy", "Engine: first-acquirer read maximum", provider, EngineReadMaxPolicyAsync, skip, reason));
            cases.Add(DbCase(suiteId, "engine-write-blocks-read", "Engine: held read blocks write", provider, EngineWriteBlocksReadAsync, skip, reason));
            cases.Add(DbCase(suiteId, "engine-write-exclusive", "Engine: writes are exclusive", provider, EngineWriteExclusiveAsync, skip, reason));
            cases.Add(DbCase(suiteId, "engine-delete-exclusive", "Engine: delete requires an empty key", provider, EngineDeleteExclusiveAsync, skip, reason));
            cases.Add(DbCase(suiteId, "engine-fencing-monotonic", "Engine: fencing tokens are monotonic", provider, EngineFencingMonotonicAsync, skip, reason));
            cases.Add(DbCase(suiteId, "engine-lease-expiry", "Engine: expired holder is reclaimed", provider, EngineLeaseExpiryAsync, skip, reason));
            cases.Add(DbCase(suiteId, "engine-release-session", "Engine: release all for a session", provider, EngineReleaseSessionAsync, skip, reason));
            cases.Add(DbCase(suiteId, "engine-wait-granted", "Engine: waiter is granted on release", provider, EngineWaitGrantedAsync, skip, reason));
            cases.Add(DbCase(suiteId, "engine-wait-timeout", "Engine: waiter times out", provider, EngineWaitTimeoutAsync, skip, reason));
            cases.Add(DbCase(suiteId, "waiter-poll-wakeup", "Engine: waiter woken by polling after unsignaled release", provider, WaiterPollWakeupAsync, skip, reason));
            cases.Add(DbCase(suiteId, "db-tenant-crud", "Database: tenant CRUD", provider, DbTenantCrudAsync, skip, reason));
            cases.Add(DbCase(suiteId, "db-user-isolation", "Database: user tenant isolation", provider, DbUserIsolationAsync, skip, reason));
            cases.Add(DbCase(suiteId, "db-credential-accesskey", "Database: credential by access key", provider, DbCredentialByAccessKeyAsync, skip, reason));
            cases.Add(DbCase(suiteId, "db-cascade-delete", "Database: tenant cascade delete", provider, DbCascadeDeleteAsync, skip, reason));
            cases.Add(DbCase(suiteId, "soak-randomized", "Soak: randomized concurrency invariants", provider, SoakRandomizedAsync, skip, reason));
            return cases;
        }

        #endregion

        #region Compatibility-Tests

        private static void CompatReadShared()
        {
            LockDefinition def = new LockDefinition();
            def.TenantId = "t"; def.LockKey = "k";
            HolderCounts counts = new HolderCounts();
            counts.Read = 5;
            Assert(LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Read).Compatible, "reads should be shared when unlimited");
        }

        private static void CompatReadMax()
        {
            LockDefinition def = new LockDefinition();
            def.TenantId = "t"; def.LockKey = "k"; def.ReadMaxHolders = 2;
            HolderCounts counts = new HolderCounts();
            counts.Read = 2;
            Assert(!LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Read).Compatible, "reader at max should be blocked");
            counts.Read = 1;
            Assert(LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Read).Compatible, "reader under max should be allowed");
        }

        private static void CompatWriteBlocksReads()
        {
            LockDefinition def = new LockDefinition();
            def.TenantId = "t"; def.LockKey = "k"; def.WriteBlocksReads = true;
            HolderCounts counts = new HolderCounts();
            counts.Write = 1;
            Assert(!LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Read).Compatible, "read should be blocked by write when WriteBlocksReads");
            def.WriteBlocksReads = false;
            Assert(LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Read).Compatible, "read should be allowed when WriteBlocksReads is false");
        }

        private static void CompatWriteExclusive()
        {
            LockDefinition def = new LockDefinition();
            def.TenantId = "t"; def.LockKey = "k"; def.WriteExclusivity = WriteExclusivityEnum.Exclusive;
            HolderCounts counts = new HolderCounts();
            counts.Write = 1;
            Assert(!LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Write).Compatible, "second exclusive write should be blocked");
        }

        private static void CompatWriteShared()
        {
            LockDefinition def = new LockDefinition();
            def.TenantId = "t"; def.LockKey = "k"; def.WriteExclusivity = WriteExclusivityEnum.Shared; def.WriteMaxHolders = 3; def.WriteBlocksReads = false;
            HolderCounts counts = new HolderCounts();
            counts.Write = 2;
            Assert(LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Write).Compatible, "shared writer under max should be allowed");
            counts.Write = 3;
            Assert(!LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Write).Compatible, "shared writer at max should be blocked");
        }

        private static void CompatDeleteExclusive()
        {
            LockDefinition def = new LockDefinition();
            def.TenantId = "t"; def.LockKey = "k";
            HolderCounts counts = new HolderCounts();
            counts.Read = 1;
            Assert(!LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Delete).Compatible, "delete should require an empty key");
            counts.Read = 0;
            Assert(LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Delete).Compatible, "delete should be allowed on an empty key");
            counts.Delete = 1;
            Assert(!LockCompatibilityEvaluator.Evaluate(def, counts, LockModeEnum.Read).Compatible, "held delete should block reads");
        }

        #endregion

        #region Mcp-Argument-Tests

        // MCP clients deliver tool arguments as a JSON object (System.Text.Json). These cases lock in the
        // parsing behavior of McpToolArguments after the Voltaic.Mcp migration replaced the previous
        // RpcParameters accessor with raw JsonElement handling.

        private static JsonElement Json(string json)
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }

        private static void McpGetStringPresent()
        {
            JsonElement args = Json("{\"tenantId\":\"t-123\",\"count\":42}");
            Assert(McpToolArguments.GetString(args, "tenantId") == "t-123", "string property should be returned verbatim");
            Assert(McpToolArguments.GetString(args, "count") == "42", "numeric property should be returned as its textual form");
        }

        private static void McpGetStringAbsent()
        {
            JsonElement obj = Json("{\"flag\":true,\"nested\":{\"a\":1},\"nothing\":null}");
            Assert(McpToolArguments.GetString(obj, "missing") == string.Empty, "missing property should yield an empty string");
            Assert(McpToolArguments.GetString(obj, "flag") == string.Empty, "boolean property should yield an empty string");
            Assert(McpToolArguments.GetString(obj, "nested") == string.Empty, "object property should yield an empty string");
            Assert(McpToolArguments.GetString(obj, "nothing") == string.Empty, "JSON null property should yield an empty string");
            Assert(McpToolArguments.GetString(null, "tenantId") == string.Empty, "null args should yield an empty string");
            Assert(McpToolArguments.GetString(Json("[1,2,3]"), "tenantId") == string.Empty, "non-object args should yield an empty string");
        }

        private static void McpGetIntPresent()
        {
            JsonElement args = Json("{\"max\":25,\"skipStr\":\"7\",\"neg\":-3}");
            Assert(McpToolArguments.GetInt(args, "max") == 25, "JSON number should parse to an int");
            Assert(McpToolArguments.GetInt(args, "skipStr") == 7, "numeric string should parse to an int");
            Assert(McpToolArguments.GetInt(args, "neg") == -3, "negative JSON number should parse to an int");
        }

        private static void McpGetIntAbsent()
        {
            JsonElement args = Json("{\"word\":\"abc\",\"flag\":false,\"real\":1.5}");
            Assert(McpToolArguments.GetInt(args, "missing") == null, "missing property should yield null");
            Assert(McpToolArguments.GetInt(args, "word") == null, "non-numeric string should yield null");
            Assert(McpToolArguments.GetInt(args, "flag") == null, "boolean property should yield null");
            Assert(McpToolArguments.GetInt(null, "max") == null, "null args should yield null");
        }

        private static void McpBuildQueryDefaults()
        {
            EnumerationQuery fromNull = McpToolArguments.BuildQuery(null);
            Assert(fromNull.MaxResults == 25 && fromNull.Skip == 0, "null args should leave the query at its defaults");
            EnumerationQuery fromEmpty = McpToolArguments.BuildQuery(Json("{}"));
            Assert(fromEmpty.MaxResults == 25 && fromEmpty.Skip == 0, "empty args should leave the query at its defaults");
        }

        private static void McpBuildQueryValues()
        {
            EnumerationQuery query = McpToolArguments.BuildQuery(Json("{\"maxResults\":50,\"skip\":\"10\"}"));
            Assert(query.MaxResults == 50, "maxResults should be read from a JSON number");
            Assert(query.Skip == 10, "skip should be read from a numeric string");

            // EnumerationQuery clamps out-of-range paging values; the parser must feed them through unchanged.
            EnumerationQuery clamped = McpToolArguments.BuildQuery(Json("{\"maxResults\":5000,\"skip\":-4}"));
            Assert(clamped.MaxResults == 1000, "oversized maxResults should be clamped to the query maximum");
            Assert(clamped.Skip == 0, "negative skip should be clamped to zero");
        }

        #endregion

        #region Engine-Tests

        private static async Task EngineReadSharedAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            LockResult r1 = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "sessA"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult r2 = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "sessB"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(r1.IsGranted() && r2.IsGranted(), "two reads should both be granted");
            List<LockHolder> holders = await db.LockHolders.EnumerateByKeyAsync(tenant.Id, key, ct).ConfigureAwait(false);
            Assert(holders.Count == 2, "expected two active read holders");
        }

        private static async Task EngineReadMaxPolicyAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            LockPolicySpec policy = new LockPolicySpec();
            policy.ReadMaxHolders = 2;
            await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "s1", policy), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "s2"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult third = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "s3"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(third.Result == LockResultEnum.Denied, "third reader should be denied by first-acquirer policy");
        }

        private static async Task EngineWriteBlocksReadAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "s1"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult write = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s2"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(write.Result == LockResultEnum.Denied, "write should be denied while a read is held");
        }

        private static async Task EngineWriteExclusiveAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            LockResult w1 = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s1"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult w2 = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s2"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(w1.IsGranted() && w2.Result == LockResultEnum.Denied, "second exclusive write should be denied");
        }

        private static async Task EngineDeleteExclusiveAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "s1"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult del = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Delete, "s2"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(del.Result == LockResultEnum.Denied, "delete should be denied while a read is held");

            string key2 = NewKey();
            LockResult del2 = await engine.AcquireAsync(Req(tenant.Id, key2, LockModeEnum.Delete, "s1"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult read2 = await engine.AcquireAsync(Req(tenant.Id, key2, LockModeEnum.Read, "s2"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(del2.IsGranted() && read2.Result == LockResultEnum.Denied, "held delete should block reads");
        }

        private static async Task EngineFencingMonotonicAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            long previous = 0;
            for (int i = 0; i < 5; i++)
            {
                LockResult r = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s" + i), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
                Assert(r.IsGranted(), "acquire should be granted after release");
                Assert(r.FencingToken > previous, "fencing token must strictly increase (got " + r.FencingToken + " after " + previous + ")");
                previous = r.FencingToken;
                Assert(r.Holder != null, "granted result must carry a holder");
                await engine.ReleaseAsync(tenant.Id, r.Holder!.Id, "s" + i, ct).ConfigureAwait(false);
            }
            Assert(previous == 5, "expected five increments of the fencing counter");
        }

        private static async Task EngineLeaseExpiryAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            LockResult write = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s1", null, 1000), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(write.IsGranted(), "initial write should be granted");
            await Task.Delay(1300, ct).ConfigureAwait(false);
            LockResult read = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "s2"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(read.IsGranted(), "read should be granted after the write's lease expired and was reclaimed");
        }

        private static async Task EngineReleaseSessionAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key1 = NewKey();
            string key2 = NewKey();

            await engine.AcquireAsync(Req(tenant.Id, key1, LockModeEnum.Read, "shared-session"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            await engine.AcquireAsync(Req(tenant.Id, key2, LockModeEnum.Read, "shared-session"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            await engine.ReleaseAllForSessionAsync("shared-session", ct).ConfigureAwait(false);

            List<LockHolder> h1 = await db.LockHolders.EnumerateByKeyAsync(tenant.Id, key1, ct).ConfigureAwait(false);
            List<LockHolder> h2 = await db.LockHolders.EnumerateByKeyAsync(tenant.Id, key2, ct).ConfigureAwait(false);
            Assert(h1.Count == 0 && h2.Count == 0, "release-all-for-session should free both keys");
        }

        private static async Task EngineWaitGrantedAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            LockResult held = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "holder"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(held.IsGranted() && held.Holder != null, "initial write should be granted");

            Task<LockResult> waiter = engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "waiter"), LockBehaviorEnum.Wait, 5000, ct);
            await Task.Delay(400, ct).ConfigureAwait(false);
            await engine.ReleaseAsync(tenant.Id, held.Holder!.Id, "holder", ct).ConfigureAwait(false);

            LockResult result = await waiter.ConfigureAwait(false);
            Assert(result.IsGranted(), "waiter should be granted after the holder released");
            Assert(result.Attempts >= 2, "waiter should have retried at least once");
        }

        private static async Task EngineWaitTimeoutAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "holder"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult result = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "waiter"), LockBehaviorEnum.Wait, 600, ct).ConfigureAwait(false);
            Assert(result.Result == LockResultEnum.Timeout, "waiter should time out when the lock is never released");
        }

        private static async Task WaiterPollWakeupAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            // Prove the polling fallback wakes a waiter even when no in-process signal is delivered: the
            // holder is removed via the DB revoke path, which does not signal the coordinator.
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            LockResult held = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "holder"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(held.IsGranted() && held.Holder != null, "initial write should be granted");

            Task<LockResult> waiter = engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "waiter"), LockBehaviorEnum.Wait, 5000, ct);
            await Task.Delay(300, ct).ConfigureAwait(false);
            await db.LockHolders.RevokeAsync(tenant.Id, held.Holder!.Id, "poll-wakeup-test", ct).ConfigureAwait(false);

            LockResult result = await waiter.ConfigureAwait(false);
            Assert(result.IsGranted(), "waiter should be granted by polling after an unsignaled release");
        }

        #endregion

        #region Database-Tests

        private static async Task DbTenantCrudAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            Tenant tenant = new Tenant();
            tenant.Name = "crud-" + Guid.NewGuid().ToString("N");
            tenant = await db.Tenants.CreateAsync(tenant, ct).ConfigureAwait(false);

            Tenant? read = await db.Tenants.ReadAsync(tenant.Id, ct).ConfigureAwait(false);
            Assert(read != null && read!.Name == tenant.Name, "tenant read should return the created tenant");

            Tenant? byName = await db.Tenants.ReadByNameAsync(tenant.Name, ct).ConfigureAwait(false);
            Assert(byName != null && byName!.Id == tenant.Id, "tenant read-by-name should match");

            read!.LockHistoryRetentionDays = 14;
            await db.Tenants.UpdateAsync(read, ct).ConfigureAwait(false);
            Tenant? updated = await db.Tenants.ReadAsync(tenant.Id, ct).ConfigureAwait(false);
            Assert(updated!.LockHistoryRetentionDays == 14, "tenant update should persist");

            bool deleted = await db.Tenants.DeleteAsync(tenant.Id, ct).ConfigureAwait(false);
            Tenant? afterDelete = await db.Tenants.ReadAsync(tenant.Id, ct).ConfigureAwait(false);
            Assert(deleted && afterDelete == null, "tenant delete should remove the tenant");
        }

        private static async Task DbUserIsolationAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            Tenant a = await NewTenantAsync(db, ct).ConfigureAwait(false);
            Tenant b = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string email = "user@example.com";

            User ua = new User(); ua.TenantId = a.Id; ua.Email = email; ua.PasswordSha256 = new string('a', 64);
            ua = await db.Users.CreateAsync(ua, ct).ConfigureAwait(false);
            User ub = new User(); ub.TenantId = b.Id; ub.Email = email; ub.PasswordSha256 = new string('b', 64);
            ub = await db.Users.CreateAsync(ub, ct).ConfigureAwait(false);

            User? fromA = await db.Users.ReadByEmailAsync(a.Id, email, ct).ConfigureAwait(false);
            Assert(fromA != null && fromA!.Id == ua.Id, "email lookup should be scoped to tenant A");

            List<User> enumB = await db.Users.EnumerateAsync(b.Id, ct).ConfigureAwait(false);
            Assert(enumB.All(u => u.TenantId == b.Id) && enumB.Any(u => u.Id == ub.Id), "enumeration must not leak cross-tenant users");
            Assert(!enumB.Any(u => u.Id == ua.Id), "tenant B enumeration must not include tenant A's user");
        }

        private static async Task DbCredentialByAccessKeyAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string accessKey = "access_" + Guid.NewGuid().ToString("N");

            Credential credential = new Credential();
            credential.TenantId = tenant.Id;
            credential.Name = "test-key";
            credential.AccessKey = accessKey;
            credential = await db.Credentials.CreateAsync(credential, ct).ConfigureAwait(false);

            Credential? found = await db.Credentials.ReadByAccessKeyAsync(accessKey, ct).ConfigureAwait(false);
            Assert(found != null && found!.Id == credential.Id, "credential should be found by access key");
        }

        private static async Task DbCascadeDeleteAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);

            User user = new User(); user.TenantId = tenant.Id; user.Email = "cascade@example.com"; user.PasswordSha256 = new string('a', 64);
            user = await db.Users.CreateAsync(user, ct).ConfigureAwait(false);
            Credential credential = new Credential(); credential.TenantId = tenant.Id; credential.UserId = user.Id; credential.Name = "k"; credential.AccessKey = "access_" + Guid.NewGuid().ToString("N");
            await db.Credentials.CreateAsync(credential, ct).ConfigureAwait(false);
            await engine.AcquireAsync(Req(tenant.Id, NewKey(), LockModeEnum.Write, "s1"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);

            await db.Tenants.DeleteAsync(tenant.Id, ct).ConfigureAwait(false);

            List<User> users = await db.Users.EnumerateAsync(tenant.Id, ct).ConfigureAwait(false);
            List<Credential> creds = await db.Credentials.EnumerateAsync(tenant.Id, ct).ConfigureAwait(false);
            List<LockHolder> holders = await db.LockHolders.EnumerateByTenantAsync(tenant.Id, null, null, ct).ConfigureAwait(false);
            Assert(users.Count == 0 && creds.Count == 0 && holders.Count == 0, "deleting a tenant should cascade to users, credentials, and holders");
        }

        #endregion

        #region Soak-Test

        private static async Task SoakRandomizedAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            LockEngine engine = MakeEngine(db, "soak");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);

            int durationMs = EnvInt("CLUTCH_SOAK_MS", 4000);
            int clientCount = EnvInt("CLUTCH_SOAK_CLIENTS", 16);
            int seed = EnvInt("CLUTCH_SOAK_SEED", 20260808);

            int keyCount = 8;
            string[] keys = new string[keyCount];
            for (int i = 0; i < keyCount; i++)
            {
                keys[i] = "soak-key-" + i;
                LockPolicySpec policy = new LockPolicySpec();
                if (i % 3 == 0) { policy.WriteExclusivity = WriteExclusivityEnum.Shared; policy.WriteMaxHolders = 3; policy.WriteBlocksReads = false; }
                if (i % 4 == 0) { policy.ReadMaxHolders = 5; }
                LockResult seeded = await engine.AcquireAsync(Req(tenant.Id, keys[i], LockModeEnum.Read, "seed", policy), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
                if (seeded.IsGranted() && seeded.Holder != null) await engine.ReleaseAsync(tenant.Id, seeded.Holder!.Id, "seed", ct).ConfigureAwait(false);
            }

            ConcurrentDictionary<string, byte> fencingSeen = new ConcurrentDictionary<string, byte>();
            ConcurrentBag<string> violations = new ConcurrentBag<string>();
            long ops = 0;
            long grants = 0;
            using CancellationTokenSource stop = new CancellationTokenSource();

            LockModeEnum[] modes = { LockModeEnum.Read, LockModeEnum.Read, LockModeEnum.Write, LockModeEnum.Delete };

            List<Task> clients = new List<Task>();
            for (int c = 0; c < clientCount; c++)
            {
                int clientIndex = c;
                clients.Add(Task.Run(async () =>
                {
                    Random rng = new Random(seed + clientIndex);
                    string session = "soak-s" + clientIndex;
                    while (!stop.IsCancellationRequested)
                    {
                        string key = keys[rng.Next(keyCount)];
                        LockModeEnum mode = modes[rng.Next(modes.Length)];
                        Interlocked.Increment(ref ops);
                        LockResult r = await engine.AcquireAsync(Req(tenant.Id, key, mode, session), LockBehaviorEnum.FailFast, null, CancellationToken.None).ConfigureAwait(false);
                        if (r.IsGranted() && r.Holder != null)
                        {
                            Interlocked.Increment(ref grants);
                            string fenceKey = key + "|" + r.FencingToken;
                            if (!fencingSeen.TryAdd(fenceKey, 1)) violations.Add("duplicate fencing token " + fenceKey);
                            await Task.Delay(rng.Next(2, 25)).ConfigureAwait(false);
                            await engine.ReleaseAsync(tenant.Id, r.Holder!.Id, session, CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                }, CancellationToken.None));
            }

            Task checker = Task.Run(async () =>
            {
                Random rng = new Random(seed + 9999);
                while (!stop.IsCancellationRequested)
                {
                    string key = keys[rng.Next(keyCount)];
                    string? problem = await CheckKeyInvariantAsync(db, tenant.Id, key, CancellationToken.None).ConfigureAwait(false);
                    if (problem != null) violations.Add(problem);
                    await Task.Delay(15).ConfigureAwait(false);
                }
            }, CancellationToken.None);

            await Task.Delay(durationMs, ct).ConfigureAwait(false);
            stop.Cancel();
            await Task.WhenAll(clients).ConfigureAwait(false);
            await checker.ConfigureAwait(false);

            foreach (string key in keys)
            {
                string? problem = await CheckKeyInvariantAsync(db, tenant.Id, key, ct).ConfigureAwait(false);
                if (problem != null) violations.Add(problem);
            }
            List<LockHolder> remaining = await db.LockHolders.EnumerateByTenantAsync(tenant.Id, null, null, ct).ConfigureAwait(false);

            if (!violations.IsEmpty)
            {
                throw new Exception("Soak invariants violated (seed " + seed + ", " + ops + " ops, " + grants + " grants): " + string.Join(" | ", violations.Take(5)));
            }
            Assert(remaining.Count == 0, "no holders should remain after all clients release (seed " + seed + ", remaining " + remaining.Count + ")");
            Assert(ops > 0 && grants > 0, "soak should make forward progress (ops " + ops + ", grants " + grants + ")");
        }

        private static async Task<string?> CheckKeyInvariantAsync(DatabaseDriverBase db, string tenantId, string key, CancellationToken ct)
        {
            LockDefinition? def = await db.LockDefinitions.ReadAsync(tenantId, key, ct).ConfigureAwait(false);
            List<LockHolder> holders = await db.LockHolders.EnumerateByKeyAsync(tenantId, key, ct).ConfigureAwait(false);
            int read = holders.Count(h => h.Mode == LockModeEnum.Read);
            int write = holders.Count(h => h.Mode == LockModeEnum.Write);
            int delete = holders.Count(h => h.Mode == LockModeEnum.Delete);

            if (delete > 0 && (read > 0 || write > 0 || delete > 1)) return "delete not exclusive on " + key + " (r=" + read + ",w=" + write + ",d=" + delete + ")";
            if (def != null)
            {
                int maxWriters = def.WriteExclusivity == WriteExclusivityEnum.Shared ? def.WriteMaxHolders : 1;
                if (write > maxWriters) return "writers over max on " + key + " (w=" + write + ",max=" + maxWriters + ")";
                if (def.WriteBlocksReads && read > 0 && write > 0) return "read/write coexist on " + key + " (r=" + read + ",w=" + write + ")";
                if (def.ReadMaxHolders >= 0 && read > def.ReadMaxHolders) return "readers over max on " + key + " (r=" + read + ",max=" + def.ReadMaxHolders + ")";
            }
            return null;
        }

        #endregion

        #region Provider-Initialization

        private static void InitProviders()
        {
            if (_Initialized) return;
            _Initialized = true;

            string list = Env("CLUTCH_TEST_PROVIDERS", Env("CLUTCH_TEST_DB_TYPE", "sqlite"));
            HashSet<string> requested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) requested.Add(part);

            if (requested.Contains("sqlite")) _Providers.Add(BuildProvider("SQLite", "clutch-sqlite", SqliteSettings()));
            if (requested.Contains("postgresql") || requested.Contains("postgres")) _Providers.Add(BuildProvider("PostgreSQL", "clutch-postgresql", PostgresSettings()));
            if (requested.Contains("mysql")) _Providers.Add(BuildProvider("MySQL", "clutch-mysql", MysqlSettings()));
            if (requested.Contains("sqlserver") || requested.Contains("mssql")) _Providers.Add(BuildProvider("SQL Server", "clutch-sqlserver", SqlServerSettings()));

            if (_Providers.Count == 0) _Providers.Add(BuildProvider("SQLite", "clutch-sqlite", SqliteSettings()));
        }

        private static ProviderContext BuildProvider(string name, string suiteId, DatabaseSettings settings)
        {
            ProviderContext context = new ProviderContext();
            context.Name = name;
            context.SuiteId = suiteId;
            context.Hint = settings.Type == DatabaseTypeEnum.Sqlite ? settings.FilePath : settings.Host + ":" + settings.Port + "/" + settings.DatabaseName;

            try
            {
                DatabaseDriverBase driver = DatabaseDriverFactory.Create(settings);
                driver.InitializeAsync().GetAwaiter().GetResult();
                bool ok = driver.PingAsync().GetAwaiter().GetResult();
                if (ok)
                {
                    context.Driver = driver;
                    context.Available = true;
                }
                else
                {
                    driver.Dispose();
                }
            }
            catch (Exception e)
            {
                context.Hint = context.Hint + "; " + e.Message;
                context.Available = false;
            }

            return context;
        }

        private static DatabaseSettings SqliteSettings()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Type = DatabaseTypeEnum.Sqlite;
            settings.FilePath = Env("CLUTCH_TEST_SQLITE_FILEPATH", Path.Combine(Path.GetTempPath(), "clutch-test-" + Guid.NewGuid().ToString("N") + ".db"));
            return settings;
        }

        private static DatabaseSettings PostgresSettings()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Type = DatabaseTypeEnum.Postgresql;
            settings.Host = Env("CLUTCH_TEST_PG_HOST", "localhost");
            settings.Port = EnvInt("CLUTCH_TEST_PG_PORT", 5432);
            settings.DatabaseName = Env("CLUTCH_TEST_PG_DATABASE", "clutch");
            settings.Username = Env("CLUTCH_TEST_PG_USERNAME", "postgres");
            settings.Password = Env("CLUTCH_TEST_PG_PASSWORD", "postgres");
            settings.Schema = EnvOrNull("CLUTCH_TEST_PG_SCHEMA");
            return settings;
        }

        private static DatabaseSettings MysqlSettings()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Type = DatabaseTypeEnum.Mysql;
            settings.Host = Env("CLUTCH_TEST_MYSQL_HOST", "localhost");
            settings.Port = EnvInt("CLUTCH_TEST_MYSQL_PORT", 3306);
            settings.DatabaseName = Env("CLUTCH_TEST_MYSQL_DATABASE", "clutch");
            settings.Username = Env("CLUTCH_TEST_MYSQL_USERNAME", "root");
            settings.Password = Env("CLUTCH_TEST_MYSQL_PASSWORD", "root");
            return settings;
        }

        private static DatabaseSettings SqlServerSettings()
        {
            DatabaseSettings settings = new DatabaseSettings();
            settings.Type = DatabaseTypeEnum.SqlServer;
            settings.Host = Env("CLUTCH_TEST_MSSQL_HOST", "localhost");
            settings.Port = EnvInt("CLUTCH_TEST_MSSQL_PORT", 1433);
            settings.DatabaseName = Env("CLUTCH_TEST_MSSQL_DATABASE", "clutch");
            settings.Username = Env("CLUTCH_TEST_MSSQL_USERNAME", "sa");
            settings.Password = Env("CLUTCH_TEST_MSSQL_PASSWORD", "Clutch_Test_123");
            settings.Schema = EnvOrNull("CLUTCH_TEST_MSSQL_SCHEMA");
            return settings;
        }

        #endregion

        #region Private-Helpers

        private static LockEngine MakeEngine(DatabaseDriverBase db, string nodeId)
        {
            LockEngineOptions options = new LockEngineOptions();
            options.NodeId = nodeId;
            options.WaiterPollMs = 100;
            options.MaxWaitMs = 600000;
            options.DefaultLeaseMs = 30000;
            return new LockEngine(db.LockHolders, db.LockAudit, new LockCoordinator(), options);
        }

        private static async Task<Tenant> NewTenantAsync(DatabaseDriverBase db, CancellationToken ct)
        {
            Tenant tenant = new Tenant();
            tenant.Name = "test-" + Guid.NewGuid().ToString("N");
            return await db.Tenants.CreateAsync(tenant, ct).ConfigureAwait(false);
        }

        private static string NewKey()
        {
            return "k-" + Guid.NewGuid().ToString("N");
        }

        private static AcquireRequest Req(string tenantId, string key, LockModeEnum mode, string session, LockPolicySpec? policy = null, int? leaseMs = null)
        {
            AcquireRequest request = new AcquireRequest();
            request.TenantId = tenantId;
            request.LockKey = key;
            request.Mode = mode;
            request.CredentialId = "test-cred";
            request.SessionId = session;
            request.NodeId = "testnode";
            request.Policy = policy;
            request.RequestedLeaseMs = leaseMs;
            return request;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static string Env(string name, string fallback)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static string? EnvOrNull(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static int EnvInt(string name, int fallback)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int parsed)) return parsed;
            return fallback;
        }

        private static TestCaseDescriptor SyncCase(string suiteId, string caseId, string displayName, Action execute)
        {
            return new TestCaseDescriptor(
                suiteId,
                caseId,
                displayName,
                token =>
                {
                    token.ThrowIfCancellationRequested();
                    execute();
                    return Task.CompletedTask;
                },
                new[] { suiteId });
        }

        private static TestCaseDescriptor DbCase(string suiteId, string caseId, string displayName, ProviderContext provider, Func<DatabaseDriverBase, CancellationToken, Task> executeAsync, bool skip, string skipReason)
        {
            return new TestCaseDescriptor(
                suiteId,
                caseId,
                displayName,
                async token =>
                {
                    token.ThrowIfCancellationRequested();
                    await executeAsync(provider.Driver!, token).ConfigureAwait(false);
                },
                new[] { suiteId })
            {
                Skip = skip,
                SkipReason = skip ? skipReason : null
            };
        }

        #endregion

        #region Provider-Context

        private class ProviderContext
        {
            public string Name { get; set; } = string.Empty;
            public string SuiteId { get; set; } = string.Empty;
            public string Hint { get; set; } = string.Empty;
            public bool Available { get; set; } = false;
            public DatabaseDriverBase? Driver { get; set; } = null;
        }

        #endregion
    }
}

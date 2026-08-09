namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core;
    using Clutch.Core.Database;
    using Clutch.Core.Database.Postgresql;
    using Clutch.Core.Database.Postgresql.Notifications;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Services;
    using Touchstone.Core;

    /// <summary>
    /// Clutch shared test suites: pure compatibility logic, database-backed lock engine correctness,
    /// LISTEN/NOTIFY, tenant isolation, and a randomized soak with an invariant oracle.
    /// </summary>
    public static class ClutchSuites
    {
        #region Private-Members

        private const string _SuiteId = "clutch";
        private static DatabaseDriverBase? _Database;
        private static bool _DbAvailable = false;
        private static string _DbHint = string.Empty;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get the shared Touchstone test suites.
        /// </summary>
        /// <returns>Test suite descriptors.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> GetSuites()
        {
            InitDatabase();
            bool skipDb = !_DbAvailable;
            string dbReason = "PostgreSQL not available (" + _DbHint + ").";

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            // Pure compatibility logic (no database).
            cases.Add(SyncCase("compat-read-shared", "Compat: reads are shared", CompatReadShared));
            cases.Add(SyncCase("compat-read-max", "Compat: reader maximum enforced", CompatReadMax));
            cases.Add(SyncCase("compat-write-blocks-reads", "Compat: write blocks reads by policy", CompatWriteBlocksReads));
            cases.Add(SyncCase("compat-write-exclusive", "Compat: writes are exclusive", CompatWriteExclusive));
            cases.Add(SyncCase("compat-write-shared", "Compat: shared writers up to max", CompatWriteShared));
            cases.Add(SyncCase("compat-delete-exclusive", "Compat: delete is fully exclusive", CompatDeleteExclusive));

            // Database-backed lock engine.
            cases.Add(AsyncCase("engine-read-shared", "Engine: two reads share a key", EngineReadSharedAsync, skipDb, dbReason));
            cases.Add(AsyncCase("engine-read-max-policy", "Engine: first-acquirer read maximum", EngineReadMaxPolicyAsync, skipDb, dbReason));
            cases.Add(AsyncCase("engine-write-blocks-read", "Engine: held read blocks write", EngineWriteBlocksReadAsync, skipDb, dbReason));
            cases.Add(AsyncCase("engine-write-exclusive", "Engine: writes are exclusive", EngineWriteExclusiveAsync, skipDb, dbReason));
            cases.Add(AsyncCase("engine-delete-exclusive", "Engine: delete requires an empty key", EngineDeleteExclusiveAsync, skipDb, dbReason));
            cases.Add(AsyncCase("engine-fencing-monotonic", "Engine: fencing tokens are monotonic", EngineFencingMonotonicAsync, skipDb, dbReason));
            cases.Add(AsyncCase("engine-lease-expiry", "Engine: expired holder is reclaimed", EngineLeaseExpiryAsync, skipDb, dbReason));
            cases.Add(AsyncCase("engine-release-session", "Engine: release all for a session", EngineReleaseSessionAsync, skipDb, dbReason));
            cases.Add(AsyncCase("engine-wait-granted", "Engine: waiter is granted on release", EngineWaitGrantedAsync, skipDb, dbReason));
            cases.Add(AsyncCase("engine-wait-timeout", "Engine: waiter times out", EngineWaitTimeoutAsync, skipDb, dbReason));

            // LISTEN/NOTIFY.
            cases.Add(AsyncCase("notify-fires", "Notify: release fires LISTEN/NOTIFY", NotifyFiresAsync, skipDb, dbReason));

            // Database and isolation.
            cases.Add(AsyncCase("db-tenant-crud", "Database: tenant CRUD", DbTenantCrudAsync, skipDb, dbReason));
            cases.Add(AsyncCase("db-user-isolation", "Database: user tenant isolation", DbUserIsolationAsync, skipDb, dbReason));
            cases.Add(AsyncCase("db-credential-accesskey", "Database: credential by access key", DbCredentialByAccessKeyAsync, skipDb, dbReason));
            cases.Add(AsyncCase("db-cascade-delete", "Database: tenant cascade delete", DbCascadeDeleteAsync, skipDb, dbReason));

            // Randomized soak with invariant oracle.
            cases.Add(AsyncCase("soak-randomized", "Soak: randomized concurrency invariants", SoakRandomizedAsync, skipDb, dbReason));

            List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>();
            suites.Add(new TestSuiteDescriptor(_SuiteId, "Clutch Shared Suite", cases));
            return suites;
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

        #region Engine-Tests

        private static async Task EngineReadSharedAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            LockResult r1 = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "sessA"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult r2 = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "sessB"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(r1.IsGranted() && r2.IsGranted(), "two reads should both be granted");
            List<LockHolder> holders = await db.LockHolders.EnumerateByKeyAsync(tenant.Id, key, ct).ConfigureAwait(false);
            Assert(holders.Count == 2, "expected two active read holders");
        }

        private static async Task EngineReadMaxPolicyAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
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

        private static async Task EngineWriteBlocksReadAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "s1"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult write = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s2"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(write.Result == LockResultEnum.Denied, "write should be denied while a read is held");
        }

        private static async Task EngineWriteExclusiveAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            LockResult w1 = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s1"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult w2 = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s2"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(w1.IsGranted() && w2.Result == LockResultEnum.Denied, "second exclusive write should be denied");
        }

        private static async Task EngineDeleteExclusiveAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
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

        private static async Task EngineFencingMonotonicAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
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

        private static async Task EngineLeaseExpiryAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            LockResult write = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s1", null, 1000), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(write.IsGranted(), "initial write should be granted");
            await Task.Delay(1300, ct).ConfigureAwait(false);
            LockResult read = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Read, "s2"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            Assert(read.IsGranted(), "read should be granted after the write's lease expired and was reclaimed");
        }

        private static async Task EngineReleaseSessionAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
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

        private static async Task EngineWaitGrantedAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
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

        private static async Task EngineWaitTimeoutAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "holder"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
            LockResult result = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "waiter"), LockBehaviorEnum.Wait, 600, ct).ConfigureAwait(false);
            Assert(result.Result == LockResultEnum.Timeout, "waiter should time out when the lock is never released");
        }

        #endregion

        #region Notify-Test

        private static async Task NotifyFiresAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
            PostgresqlDatabaseDriver pg = (PostgresqlDatabaseDriver)db;
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string key = NewKey();

            bool fired = false;
            string firedKey = string.Empty;
            PostgresNotificationListener listener = new PostgresNotificationListener(pg, Constants.LockReleaseChannel);
            listener.KeyReleased += (t, k) =>
            {
                if (t == tenant.Id && k == key)
                {
                    fired = true;
                    firedKey = k;
                }
            };
            await listener.StartAsync(ct).ConfigureAwait(false);

            try
            {
                LockResult held = await engine.AcquireAsync(Req(tenant.Id, key, LockModeEnum.Write, "s1"), LockBehaviorEnum.FailFast, null, ct).ConfigureAwait(false);
                await engine.ReleaseAsync(tenant.Id, held.Holder!.Id, "s1", ct).ConfigureAwait(false);

                for (int i = 0; i < 50 && !fired; i++) await Task.Delay(100, ct).ConfigureAwait(false);
            }
            finally
            {
                await listener.DisposeAsync().ConfigureAwait(false);
            }

            Assert(fired && firedKey == key, "LISTEN/NOTIFY should deliver a release notification for the key");
        }

        #endregion

        #region Database-Tests

        private static async Task DbTenantCrudAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
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

        private static async Task DbUserIsolationAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
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

        private static async Task DbCredentialByAccessKeyAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);
            string accessKey = "access_" + Guid.NewGuid().ToString("N");

            Credential credential = new Credential();
            credential.TenantId = tenant.Id;
            credential.Name = "test-key";
            credential.AccessKey = accessKey;
            credential.SecretKeyEncrypted = new string('c', 64);
            credential = await db.Credentials.CreateAsync(credential, ct).ConfigureAwait(false);

            Credential? found = await db.Credentials.ReadByAccessKeyAsync(accessKey, ct).ConfigureAwait(false);
            Assert(found != null && found!.Id == credential.Id, "credential should be found by access key");
        }

        private static async Task DbCascadeDeleteAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
            LockEngine engine = MakeEngine(db, "n1");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);

            User user = new User(); user.TenantId = tenant.Id; user.Email = "cascade@example.com"; user.PasswordSha256 = new string('a', 64);
            user = await db.Users.CreateAsync(user, ct).ConfigureAwait(false);
            Credential credential = new Credential(); credential.TenantId = tenant.Id; credential.UserId = user.Id; credential.Name = "k"; credential.AccessKey = "access_" + Guid.NewGuid().ToString("N"); credential.SecretKeyEncrypted = new string('c', 64);
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

        private static async Task SoakRandomizedAsync(CancellationToken ct)
        {
            DatabaseDriverBase db = Db();
            LockEngine engine = MakeEngine(db, "soak");
            Tenant tenant = await NewTenantAsync(db, ct).ConfigureAwait(false);

            int durationMs = EnvInt("CLUTCH_SOAK_MS", 4000);
            int clientCount = EnvInt("CLUTCH_SOAK_CLIENTS", 16);
            int seed = EnvInt("CLUTCH_SOAK_SEED", 20260808);

            // Fixed array of keys with varied policies, set by pre-seeding definitions.
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

            // Invariant checker.
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

            // Final invariant sweep and quiescence check.
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

        #region Private-Helpers

        private static void InitDatabase()
        {
            if (_Database != null || _DbAvailable) return;
            DatabaseSettings settings = new DatabaseSettings();
            settings.Host = Env("CLUTCH_TEST_DB_HOST", "localhost");
            settings.Port = EnvInt("CLUTCH_TEST_DB_PORT", 5544);
            settings.DatabaseName = Env("CLUTCH_TEST_DB_DATABASE", "clutch");
            settings.Username = Env("CLUTCH_TEST_DB_USERNAME", "postgres");
            settings.Password = Env("CLUTCH_TEST_DB_PASSWORD", "postgres");
            _DbHint = settings.Host + ":" + settings.Port + "/" + settings.DatabaseName;

            try
            {
                DatabaseDriverBase driver = DatabaseDriverFactory.Create(settings);
                driver.InitializeAsync().GetAwaiter().GetResult();
                bool ok = driver.PingAsync().GetAwaiter().GetResult();
                if (ok)
                {
                    _Database = driver;
                    _DbAvailable = true;
                }
                else
                {
                    driver.Dispose();
                }
            }
            catch (Exception e)
            {
                _DbHint = _DbHint + "; " + e.Message;
                _DbAvailable = false;
            }
        }

        private static DatabaseDriverBase Db()
        {
            if (_Database == null) throw new Exception("Database is not available.");
            return _Database;
        }

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

        private static int EnvInt(string name, int fallback)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int parsed)) return parsed;
            return fallback;
        }

        private static TestCaseDescriptor SyncCase(string caseId, string displayName, Action execute)
        {
            return new TestCaseDescriptor(
                _SuiteId,
                caseId,
                displayName,
                token =>
                {
                    token.ThrowIfCancellationRequested();
                    execute();
                    return Task.CompletedTask;
                },
                new[] { _SuiteId });
        }

        private static TestCaseDescriptor AsyncCase(string caseId, string displayName, Func<CancellationToken, Task> executeAsync, bool skip = false, string? skipReason = null)
        {
            return new TestCaseDescriptor(
                _SuiteId,
                caseId,
                displayName,
                async token =>
                {
                    token.ThrowIfCancellationRequested();
                    await executeAsync(token).ConfigureAwait(false);
                },
                new[] { _SuiteId })
            {
                Skip = skip,
                SkipReason = skip ? skipReason : null
            };
        }

        #endregion
    }
}

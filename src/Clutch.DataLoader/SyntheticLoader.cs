namespace Clutch.DataLoader
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Core.Security;
    using Npgsql;

    /// <summary>Outcome of a load run.</summary>
    public sealed class LoadResult
    {
        public int Tenants;
        public int Users;
        public int Credentials;
        public long AuditEvents;
        public long Requests;
        public int ActiveLocks;
        public long Purged;
        public DateTime FromUtc;
        public DateTime ToUtc;
    }

    /// <summary>
    /// Generates realistic, backdated Clutch activity by writing through the Clutch data layer. Lock-audit
    /// and request-history rows carry explicit historical timestamps so the dashboard charts show a full
    /// history; synthetic rows are marked (node id / request header) so a re-run can safely replace them.
    /// </summary>
    public sealed class SyntheticLoader
    {
        private const string BaseUrl = "http://localhost:8080";

        private readonly LoaderOptions _Options;
        private readonly RandomSource _Random;
        private readonly Action<string> _Log;

        private static readonly IReadOnlyList<Weighted<LockModeEnum>> ModeWeights = new List<Weighted<LockModeEnum>>
        {
            new Weighted<LockModeEnum>(LockModeEnum.Write, 55), new Weighted<LockModeEnum>(LockModeEnum.Read, 35), new Weighted<LockModeEnum>(LockModeEnum.Delete, 10)
        };

        private static readonly IReadOnlyList<Weighted<LockEventTypeEnum>> EventWeights = new List<Weighted<LockEventTypeEnum>>
        {
            new Weighted<LockEventTypeEnum>(LockEventTypeEnum.Acquired, 34), new Weighted<LockEventTypeEnum>(LockEventTypeEnum.Released, 30), new Weighted<LockEventTypeEnum>(LockEventTypeEnum.HeartbeatRenewed, 14),
            new Weighted<LockEventTypeEnum>(LockEventTypeEnum.Waited, 6), new Weighted<LockEventTypeEnum>(LockEventTypeEnum.Denied, 7), new Weighted<LockEventTypeEnum>(LockEventTypeEnum.Expired, 5),
            new Weighted<LockEventTypeEnum>(LockEventTypeEnum.Revoked, 2), new Weighted<LockEventTypeEnum>(LockEventTypeEnum.PolicyCreated, 2)
        };

        /// <summary>Instantiate.</summary>
        public SyntheticLoader(LoaderOptions options, Action<string> log)
        {
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            _Log = log ?? (_ => { });
            _Random = new RandomSource(options.Seed);
        }

        /// <summary>Run the load (or purge) and return counts.</summary>
        public async Task<LoadResult> RunAsync(CancellationToken token)
        {
            LoadResult result = new LoadResult { FromUtc = _Options.FromUtc, ToUtc = _Options.ToUtc };

            if (_Options.DryRun)
            {
                PrintPlan();
                return result;
            }

            DatabaseSettings dbSettings = new DatabaseSettings
            {
                Type = DatabaseTypeEnum.Postgresql,
                Host = _Options.DbHost,
                Port = _Options.DbPort,
                DatabaseName = _Options.DbName,
                Username = _Options.DbUser,
                Password = _Options.DbPassword
            };
            string connectionString = dbSettings.ToPostgresConnectionString();

            DatabaseDriverBase driver = DatabaseDriverFactory.Create(dbSettings);
            try
            {
                await driver.InitializeAsync(token).ConfigureAwait(false);
                _Log("Connected to " + _Options.DbHost + ":" + _Options.DbPort + "/" + _Options.DbName);

                if (_Options.Replace || _Options.PurgeOnly)
                {
                    result.Purged = await PurgeAsync(connectionString, token).ConfigureAwait(false);
                    _Log("Purged " + result.Purged + " prior synthetic row(s).");
                }
                if (_Options.PurgeOnly) return result;

                List<TenantScope> tenants = await EnsureTenantsAsync(driver, token).ConfigureAwait(false);
                result.Tenants = tenants.Count;
                foreach (TenantScope t in tenants) { result.Users += t.Users.Count; result.Credentials += t.Credentials.Count; }

                foreach (TenantScope tenant in tenants)
                {
                    token.ThrowIfCancellationRequested();
                    if (_Options.IncludeLockAudit)
                    {
                        List<LockAuditEntry> audit = GenerateAudit(tenant);
                        await InsertManyAsync(audit, e => driver.LockAudit.CreateAsync(e, token), token).ConfigureAwait(false);
                        result.AuditEvents += audit.Count;
                        _Log("  " + tenant.Name + ": " + audit.Count + " lock-audit events");
                    }
                    if (_Options.IncludeRequestHistory)
                    {
                        List<RequestHistoryEntry> requests = GenerateRequests(tenant);
                        await InsertManyAsync(requests, e => driver.RequestHistory.CreateAsync(e, token), token).ConfigureAwait(false);
                        result.Requests += requests.Count;
                        _Log("  " + tenant.Name + ": " + requests.Count + " request-history rows");
                    }
                    if (_Options.IncludeActiveLocks)
                    {
                        int active = await CreateActiveLocksAsync(driver, tenant, token).ConfigureAwait(false);
                        result.ActiveLocks += active;
                        _Log("  " + tenant.Name + ": " + active + " active lock holders");
                    }
                }
            }
            finally
            {
                driver.Dispose();
            }

            return result;
        }

        #region Structure

        private sealed class TenantScope
        {
            public string Id = string.Empty;
            public string Name = string.Empty;
            public List<UserRef> Users = new List<UserRef>();
            public List<string> Credentials = new List<string>();
            public List<string> Keys = new List<string>();
            public Dictionary<string, long> Fencing = new Dictionary<string, long>();
        }

        private async Task<List<TenantScope>> EnsureTenantsAsync(DatabaseDriverBase driver, CancellationToken token)
        {
            List<string> names = new List<string>();
            if (_Options.TenantNames.Count > 0)
            {
                names.AddRange(_Options.TenantNames);
            }
            else
            {
                for (int i = 0; i < _Options.TenantCount; i++)
                {
                    names.Add(i < Catalogs.Companies.Count ? Catalogs.Companies[i] : Catalogs.Companies[i % Catalogs.Companies.Count] + " " + (i / Catalogs.Companies.Count + 1));
                }
            }

            List<TenantScope> scopes = new List<TenantScope>();
            foreach (string name in names)
            {
                Tenant? tenant = await driver.Tenants.ReadByNameAsync(name, token).ConfigureAwait(false);
                if (tenant == null && _Options.IncludeTenants)
                {
                    tenant = await driver.Tenants.CreateAsync(new Tenant
                    {
                        Name = name,
                        LockHistoryRetentionDays = 30,
                        DefaultLeaseMs = 30000,
                        MaxLeaseMs = 86400000 // generous so active-lock leases stay live for screenshots
                    }, token).ConfigureAwait(false);
                }
                if (tenant == null) continue;

                TenantScope scope = new TenantScope { Id = tenant.Id, Name = tenant.Name };
                scope.Keys = BuildKeys(scope.Id);
                await EnsureUsersAsync(driver, scope, token).ConfigureAwait(false);
                await EnsureCredentialsAsync(driver, scope, token).ConfigureAwait(false);
                scopes.Add(scope);
            }
            return scopes;
        }

        private async Task EnsureUsersAsync(DatabaseDriverBase driver, TenantScope scope, CancellationToken token)
        {
            List<User> existing = await driver.Users.EnumerateAsync(scope.Id, token).ConfigureAwait(false);
            HashSet<string> emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (User u in existing) { emails.Add(u.Email); scope.Users.Add(new UserRef(u.Id, u.Email, (u.FirstName + " " + u.LastName).Trim())); }
            if (!_Options.IncludeUsers) return;

            string slug = Slug(scope.Name);
            for (int j = 0; scope.Users.Count < _Options.UsersPerTenant && j < 500; j++)
            {
                string first = Catalogs.FirstNames[(j * 7 + scope.Id.Length) % Catalogs.FirstNames.Count];
                string last = Catalogs.LastNames[(j * 13 + scope.Name.Length) % Catalogs.LastNames.Count];
                string email = (first + "." + last + (j == 0 ? string.Empty : j.ToString()) + "@" + slug + ".example").ToLowerInvariant();
                if (emails.Contains(email)) continue;
                User created = await driver.Users.CreateAsync(new User
                {
                    TenantId = scope.Id,
                    Email = email,
                    FirstName = first,
                    LastName = last,
                    PasswordSha256 = PasswordHasher.Hash("demo-password"),
                    IsTenantAdmin = j == 0
                }, token).ConfigureAwait(false);
                emails.Add(email);
                scope.Users.Add(new UserRef(created.Id, email, first + " " + last));
            }
        }

        private async Task EnsureCredentialsAsync(DatabaseDriverBase driver, TenantScope scope, CancellationToken token)
        {
            List<Credential> existing = await driver.Credentials.EnumerateAsync(scope.Id, token).ConfigureAwait(false);
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Credential c in existing) { names.Add(c.Name); scope.Credentials.Add(c.Id); }
            if (!_Options.IncludeCredentials) return;

            for (int k = 0; scope.Credentials.Count < _Options.CredentialsPerTenant && k < Catalogs.CredentialNames.Count * 4; k++)
            {
                string name = k < Catalogs.CredentialNames.Count ? Catalogs.CredentialNames[k] : Catalogs.CredentialNames[k % Catalogs.CredentialNames.Count] + "-" + (k / Catalogs.CredentialNames.Count + 1);
                if (names.Contains(name)) continue;
                Credential created = await driver.Credentials.CreateAsync(new Credential
                {
                    TenantId = scope.Id,
                    Name = name,
                    AccessKey = CredentialKeyGenerator.GenerateAccessKey()
                }, token).ConfigureAwait(false);
                names.Add(name);
                scope.Credentials.Add(created.Id);
            }
        }

        private List<string> BuildKeys(string tenantId)
        {
            List<string> keys = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            int salt = tenantId.Length;
            for (int i = 0; keys.Count < _Options.LockKeysPerTenant && i < _Options.LockKeysPerTenant * 4; i++)
            {
                string resource = Catalogs.KeyResources[(i * 5 + salt) % Catalogs.KeyResources.Count];
                string shape = Catalogs.KeyShapes[(i * 3 + salt) % Catalogs.KeyShapes.Count];
                int number = 1 + (i * 37 + salt * 11) % 900;
                string key = string.Format(shape, resource, number);
                if (seen.Add(key)) keys.Add(key);
            }
            return keys;
        }

        #endregion

        #region Generation

        private List<LockAuditEntry> GenerateAudit(TenantScope tenant)
        {
            List<LockAuditEntry> entries = new List<LockAuditEntry>();
            double perHourBase = _Options.LockEventsPerDay / 24.0;
            DateTime cursor = TruncateToHour(_Options.FromUtc);
            while (cursor < _Options.ToUtc)
            {
                double expected = perHourBase * HourWeight(cursor) * RandFactor(cursor);
                int count = _Random.Poisson(expected);
                for (int i = 0; i < count; i++)
                {
                    DateTime at = ScatterInHour(cursor);
                    if (at < _Options.FromUtc || at >= _Options.ToUtc) continue;
                    LockEventTypeEnum evt = _Random.PickWeighted(EventWeights);
                    string key = _Random.Pick(tenant.Keys);
                    LockModeEnum mode = _Random.PickWeighted(ModeWeights);
                    long fencing = NextFencing(tenant, key, evt);
                    entries.Add(new LockAuditEntry
                    {
                        TenantId = tenant.Id,
                        LockKey = key,
                        Mode = mode,
                        EventType = evt,
                        CredentialId = _Random.Chance(0.9) && tenant.Credentials.Count > 0 ? _Random.Pick(tenant.Credentials) : null,
                        SessionId = NewSession(),
                        NodeId = Catalogs.SyntheticMarker,
                        FencingToken = fencing,
                        Reason = ReasonFor(evt),
                        CreatedUtc = at
                    });
                }
                cursor = cursor.AddHours(1);
            }
            return entries;
        }

        private List<RequestHistoryEntry> GenerateRequests(TenantScope tenant)
        {
            List<RequestHistoryEntry> entries = new List<RequestHistoryEntry>();
            List<Weighted<Catalogs.Route>> routeWeights = new List<Weighted<Catalogs.Route>>();
            foreach (Catalogs.Route r in Catalogs.Routes) routeWeights.Add(new Weighted<Catalogs.Route>(r, r.Weight));

            double perHourBase = _Options.RequestsPerDay / 24.0;
            DateTime cursor = TruncateToHour(_Options.FromUtc);
            while (cursor < _Options.ToUtc)
            {
                double weight = HourWeight(cursor);
                double expected = perHourBase * weight * RandFactor(cursor);
                int count = _Random.Poisson(expected);
                double loadFactor = 1.0 + weight * 0.35;
                for (int i = 0; i < count; i++)
                {
                    DateTime at = ScatterInHour(cursor);
                    if (at < _Options.FromUtc || at >= _Options.ToUtc) continue;
                    entries.Add(BuildRequest(tenant, _Random.PickWeighted(routeWeights), at, loadFactor));
                }
                cursor = cursor.AddHours(1);
            }
            return entries;
        }

        private RequestHistoryEntry BuildRequest(TenantScope tenant, Catalogs.Route route, DateTime at, double loadFactor)
        {
            string key = _Random.Pick(tenant.Keys);
            string sid = NewSession();
            string path = route.Template
                .Replace("{tid}", tenant.Id)
                .Replace("{key}", Uri.EscapeDataString(key))
                .Replace("{sid}", sid);

            int status = route.SuccessStatus;
            bool failed = _Random.Chance(_Options.ErrorRate);
            if (failed)
            {
                status = route.CanConflict && _Random.Chance(0.7)
                    ? 409
                    : _Random.PickWeighted(new List<Weighted<int>> { new Weighted<int>(404, 4), new Weighted<int>(401, 3), new Weighted<int>(400, 3), new Weighted<int>(403, 2), new Weighted<int>(500, 2), new Weighted<int>(503, 1) });
            }

            double mean = route.LatencyMean * loadFactor;
            double duration = Math.Max(1, _Random.Gaussian(mean, mean * 0.45));
            if (_Random.Chance(0.03)) duration *= 4 + _Random.NextDouble() * 6; // occasional slow request
            if (status >= 500) duration *= 1.6;

            bool asUser = tenant.Users.Count > 0 && _Random.Chance(0.55);
            string? userId = null;
            string principal;
            if (asUser)
            {
                UserRef u = _Random.Pick(tenant.Users);
                userId = u.Id;
                principal = u.Email;
            }
            else
            {
                principal = "key:" + (tenant.Credentials.Count > 0 ? _Random.Pick(tenant.Credentials) : "cred");
            }

            DateTime completed = at.AddMilliseconds(duration);
            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                TenantId = tenant.Id,
                UserId = userId,
                PrincipalName = principal,
                Method = route.Method,
                Path = path,
                Url = BaseUrl + path,
                StatusCode = status,
                DurationMs = Math.Round(duration, 1),
                SourceIp = RandomIp(),
                CreatedUtc = at,
                CompletedUtc = completed
            };
            entry.RequestHeaders = new Dictionary<string, string>
            {
                { "Accept", "application/json" },
                { "User-Agent", _Random.PickWeighted(Catalogs.UserAgents) },
                { "x-clutch-synthetic", Catalogs.SyntheticMarker }
            };
            entry.ResponseHeaders = new Dictionary<string, string> { { "Content-Type", "application/json" } };
            if (route.Body != null && route.Method != "GET")
            {
                entry.RequestBody = route.Body;
                entry.RequestBodyBytes = route.Body.Length;
            }
            if (status >= 400)
            {
                string body = "{\"error\":\"" + ErrorCode(status) + "\"}";
                entry.ResponseBody = body;
                entry.ResponseBodyBytes = body.Length;
            }
            return entry;
        }

        private async Task<int> CreateActiveLocksAsync(DatabaseDriverBase driver, TenantScope tenant, CancellationToken token)
        {
            int granted = 0;
            int target = _Options.ActiveLocksPerTenant;
            HashSet<string> used = new HashSet<string>();
            for (int i = 0; granted < target && i < target * 4 && i < tenant.Keys.Count; i++)
            {
                string key = tenant.Keys[i];
                if (!used.Add(key)) continue;
                AcquireRequest request = new AcquireRequest
                {
                    TenantId = tenant.Id,
                    LockKey = key,
                    Mode = _Random.PickWeighted(ModeWeights),
                    CredentialId = tenant.Credentials.Count > 0 ? _Random.Pick(tenant.Credentials) : string.Empty,
                    SessionId = NewSession(),
                    NodeId = Catalogs.SyntheticMarker,
                    RequestedLeaseMs = 6 * 60 * 60 * 1000 // 6h so holders stay live for screenshots
                };
                try
                {
                    AcquireOutcome outcome = await driver.LockHolders.TryAcquireAsync(request, 30000, token).ConfigureAwait(false);
                    if (outcome.Result == AcquireResultEnum.Granted) granted++;
                }
                catch
                {
                    // key may already be held from a prior run; skip.
                }
            }
            return granted;
        }

        #endregion

        #region Insert + purge

        private async Task InsertManyAsync<T>(IReadOnlyList<T> items, Func<T, Task> insert, CancellationToken token)
        {
            using SemaphoreSlim gate = new SemaphoreSlim(_Options.Concurrency);
            List<Task> tasks = new List<Task>(items.Count);
            foreach (T item in items)
            {
                token.ThrowIfCancellationRequested();
                await gate.WaitAsync(token).ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    try { await insert(item).ConfigureAwait(false); }
                    finally { gate.Release(); }
                }, token));
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task<long> PurgeAsync(string connectionString, CancellationToken token)
        {
            long total = 0;
            await using NpgsqlConnection connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            total += await ExecAsync(connection, "DELETE FROM lock_holders WHERE nodeid = @m;", "@m", Catalogs.SyntheticMarker, token).ConfigureAwait(false);
            total += await ExecAsync(connection, "DELETE FROM lock_audit WHERE nodeid = @m;", "@m", Catalogs.SyntheticMarker, token).ConfigureAwait(false);
            total += await ExecAsync(connection, "DELETE FROM request_history WHERE requestheaders::text LIKE @m;", "@m", "%" + Catalogs.SyntheticMarker + "%", token).ConfigureAwait(false);
            return total;
        }

        private static async Task<long> ExecAsync(NpgsqlConnection connection, string sql, string param, string value, CancellationToken token)
        {
            await using NpgsqlCommand command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(param, value);
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        #endregion

        #region Helpers

        private double HourWeight(DateTime hourUtc)
        {
            if (!_Options.BusinessHours) return 1.0;
            int h = hourUtc.Hour;
            double business = h >= 8 && h <= 18 ? 1.0 : h >= 19 && h <= 22 ? 0.5 : 0.15;
            bool weekend = hourUtc.DayOfWeek == DayOfWeek.Saturday || hourUtc.DayOfWeek == DayOfWeek.Sunday;
            return business * (weekend ? 0.5 : 1.0);
        }

        private double RandFactor(DateTime hourUtc)
        {
            double factor = RandomSource.Clamp(_Random.Gaussian(1.0, 0.15 + _Options.Randomness * 0.5), 0.1, 3.5);
            if (_Random.Chance(0.02 + _Options.Randomness * 0.03)) factor *= 2.0 + _Random.NextDouble() * 2.5;
            return factor;
        }

        private DateTime ScatterInHour(DateTime hour)
        {
            return hour.AddMinutes(_Random.NextInt(0, 60)).AddSeconds(_Random.NextInt(0, 60)).AddMilliseconds(_Random.NextInt(0, 1000));
        }

        private long NextFencing(TenantScope tenant, string key, LockEventTypeEnum evt)
        {
            if (!tenant.Fencing.TryGetValue(key, out long current)) current = _Random.NextInt(1, 40);
            if (evt == LockEventTypeEnum.Acquired) current++;
            tenant.Fencing[key] = current;
            return current;
        }

        private string NewSession()
        {
            return "sess_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private static string? ReasonFor(LockEventTypeEnum evt)
        {
            return evt switch
            {
                LockEventTypeEnum.Denied => "Incompatible with an existing holder.",
                LockEventTypeEnum.Expired => "Lease expired and was reclaimed.",
                LockEventTypeEnum.Revoked => "Force-released by administrator.",
                LockEventTypeEnum.Waited => "Waiting for the lock to become available.",
                LockEventTypeEnum.PolicyCreated => "Lock policy created by first acquirer.",
                _ => null
            };
        }

        private string RandomIp()
        {
            int a = _Random.PickWeighted(new List<Weighted<int>> { new Weighted<int>(10, 5), new Weighted<int>(172, 3), new Weighted<int>(192, 2) });
            return a + "." + _Random.NextInt(0, 255) + "." + _Random.NextInt(0, 255) + "." + _Random.NextInt(1, 254);
        }

        private static string ErrorCode(int status)
        {
            return status switch
            {
                400 => "BadRequest",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "NotFound",
                409 => "Conflict",
                503 => "ServiceUnavailable",
                _ => "InternalError"
            };
        }

        private static DateTime TruncateToHour(DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
        }

        private static string Slug(string name)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' && sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            string s = sb.ToString().Trim('-');
            return s.Length == 0 ? "tenant" : s;
        }

        private void PrintPlan()
        {
            int days = (int)Math.Ceiling((_Options.ToUtc - _Options.FromUtc).TotalDays);
            long audit = (long)_Options.LockEventsPerDay * days * _Options.TenantCount;
            long requests = (long)_Options.RequestsPerDay * days * _Options.TenantCount;
            _Log("DRY RUN — no data written.");
            _Log("  Window:       " + _Options.FromUtc.ToString("u") + " -> " + _Options.ToUtc.ToString("u") + " (" + days + " day(s))");
            _Log("  Tenants:      " + _Options.TenantCount + " x " + _Options.UsersPerTenant + " users, " + _Options.CredentialsPerTenant + " credentials, " + _Options.LockKeysPerTenant + " keys");
            _Log("  Load:         " + _Options.Load + " (" + _Options.LockEventsPerDay + " lock events/day, " + _Options.RequestsPerDay + " requests/day per tenant)");
            _Log("  ~Lock audit:  " + audit + " rows (business-hours weighted, so actual is lower)");
            _Log("  ~Requests:    " + requests + " rows");
            _Log("  ~Active locks:" + (_Options.ActiveLocksPerTenant * _Options.TenantCount));
            _Log("  Error rate:   " + _Options.ErrorRate.ToString("0.###"));
        }

        #endregion
    }
}

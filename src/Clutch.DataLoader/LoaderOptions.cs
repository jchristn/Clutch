namespace Clutch.DataLoader
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>Preset load levels controlling record density.</summary>
    public enum LoadLevel
    {
        /// <summary>Sparse activity.</summary>
        Light,
        /// <summary>Moderate activity.</summary>
        Medium,
        /// <summary>Dense activity (good for screenshots).</summary>
        Heavy,
        /// <summary>Very dense stress load.</summary>
        Extreme
    }

    /// <summary>
    /// Parsed command-line options for the data loader. Densities are per tenant per day; the loader scales
    /// them across the <c>[FromUtc, ToUtc]</c> window with business-hour and weekend weighting.
    /// </summary>
    public sealed class LoaderOptions
    {
        // Connection
        public string DbHost { get; set; } = "localhost";
        public int DbPort { get; set; } = 5432;
        public string DbName { get; set; } = "clutch";
        public string DbUser { get; set; } = "postgres";
        public string DbPassword { get; set; } = "postgres";
        public string? SettingsFile { get; set; } = null;

        // Time window
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-7);

        // Structure
        public LoadLevel Load { get; set; } = LoadLevel.Heavy;
        public int TenantCount { get; set; } = 3;
        public int UsersPerTenant { get; set; } = 6;
        public int CredentialsPerTenant { get; set; } = 4;
        public int LockKeysPerTenant { get; set; } = 30;
        public int ActiveLocksPerTenant { get; set; } = 0; // 0 => from preset

        // Density (per tenant per day). 0 => from preset.
        public int LockEventsPerDay { get; set; } = 0;
        public int RequestsPerDay { get; set; } = 0;
        public double ErrorRate { get; set; } = -1; // <0 => from preset
        public double Randomness { get; set; } = 0.5;
        public bool BusinessHours { get; set; } = true;

        // Entity selection
        public bool IncludeTenants { get; set; } = true;
        public bool IncludeUsers { get; set; } = true;
        public bool IncludeCredentials { get; set; } = true;
        public bool IncludeLockAudit { get; set; } = true;
        public bool IncludeRequestHistory { get; set; } = true;
        public bool IncludeActiveLocks { get; set; } = true;

        // Control
        public int Seed { get; set; } = 0;
        public bool SeedProvided { get; set; } = false;
        public bool Replace { get; set; } = true;
        public bool PurgeOnly { get; set; } = false;
        public bool DryRun { get; set; } = false;
        public int Concurrency { get; set; } = 16;
        public bool Quiet { get; set; } = false;
        public bool Verbose { get; set; } = false;
        public bool ShowHelp { get; set; } = false;

        /// <summary>Resolve preset-driven densities and validate.</summary>
        public void Resolve()
        {
            (int lockEvents, int requests, int active, double error) = Load switch
            {
                LoadLevel.Light => (200, 400, 5, 0.08),
                LoadLevel.Medium => (800, 1500, 8, 0.10),
                LoadLevel.Heavy => (2000, 4500, 12, 0.12),
                LoadLevel.Extreme => (5000, 12000, 20, 0.15),
                _ => (800, 1500, 8, 0.10)
            };
            if (LockEventsPerDay <= 0) LockEventsPerDay = lockEvents;
            if (RequestsPerDay <= 0) RequestsPerDay = requests;
            if (ActiveLocksPerTenant <= 0) ActiveLocksPerTenant = active;
            if (ErrorRate < 0) ErrorRate = error;

            if (ToUtc <= FromUtc) throw new ArgumentException("--to must be later than --from (check --days/--from/--to).");
            if (TenantCount < 1) throw new ArgumentException("--tenants must be at least 1.");
            if (Randomness < 0 || Randomness > 1) throw new ArgumentException("--randomness must be in [0,1].");
            if (Concurrency < 1) Concurrency = 1;
            if (!SeedProvided) Seed = Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7fffffff);
        }

        /// <summary>Parse arguments. Supports <c>--name value</c>, <c>--name=value</c>, and bare boolean flags.</summary>
        public static LoaderOptions Parse(string[] args)
        {
            LoaderOptions o = new LoaderOptions();
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string token = args[i];
                if (string.IsNullOrEmpty(token)) continue;
                if (!token.StartsWith("-")) throw new ArgumentException("Unexpected argument: " + token);
                string name = token.TrimStart('-');
                string value;
                int eq = name.IndexOf('=');
                if (eq >= 0)
                {
                    value = name.Substring(eq + 1);
                    name = name.Substring(0, eq);
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    value = args[++i];
                }
                else
                {
                    value = "true";
                }
                map[name] = value;
            }

            if (map.ContainsKey("help") || map.ContainsKey("h") || map.ContainsKey("?")) { o.ShowHelp = true; return o; }

            // Apply a settings file first so that any explicit --db-* flags below override it.
            if (map.TryGetValue("settings", out string? settingsFile) && !string.IsNullOrEmpty(settingsFile))
            {
                o.SettingsFile = settingsFile;
                SettingsLoader.Apply(o, settingsFile);
            }

            foreach (KeyValuePair<string, string> kv in map)
            {
                string k = kv.Key.ToLowerInvariant();
                string v = kv.Value;
                switch (k)
                {
                    case "db-host": o.DbHost = v; break;
                    case "db-port": o.DbPort = ParseInt(v, k); break;
                    case "db-name": o.DbName = v; break;
                    case "db-user": o.DbUser = v; break;
                    case "db-password": o.DbPassword = v; break;
                    case "settings": break; // already applied above

                    case "to": o.ToUtc = ParseUtc(v, k); break;
                    case "from": o.FromUtc = ParseUtc(v, k); break;
                    case "days": o.FromUtc = o.ToUtc.AddDays(-ParseDouble(v, k)); break;

                    case "load": o.Load = ParseLoad(v); break;
                    case "tenants": o.TenantCount = ParseInt(v, k); break;
                    case "users-per-tenant": o.UsersPerTenant = ParseInt(v, k); break;
                    case "credentials-per-tenant": o.CredentialsPerTenant = ParseInt(v, k); break;
                    case "lock-keys": o.LockKeysPerTenant = ParseInt(v, k); break;
                    case "active-locks": o.ActiveLocksPerTenant = ParseInt(v, k); break;

                    case "lock-events-per-day": o.LockEventsPerDay = ParseInt(v, k); break;
                    case "requests-per-day": o.RequestsPerDay = ParseInt(v, k); break;
                    case "error-rate": o.ErrorRate = ParseDouble(v, k); break;
                    case "randomness": o.Randomness = ParseDouble(v, k); break;
                    case "business-hours": o.BusinessHours = ParseBool(v); break;
                    case "no-business-hours": o.BusinessHours = false; break;

                    case "only": ApplyOnly(o, v); break;
                    case "skip": ApplySkip(o, v); break;

                    case "seed": o.Seed = ParseInt(v, k); o.SeedProvided = true; break;
                    case "replace": o.Replace = ParseBool(v); break;
                    case "no-replace": o.Replace = false; break;
                    case "purge-only": o.PurgeOnly = ParseBool(v); break;
                    case "dry-run": o.DryRun = ParseBool(v); break;
                    case "concurrency": o.Concurrency = ParseInt(v, k); break;
                    case "quiet": o.Quiet = ParseBool(v); break;
                    case "verbose": o.Verbose = ParseBool(v); break;
                    default: throw new ArgumentException("Unknown option: --" + k);
                }
            }

            return o;
        }

        private static void ApplyOnly(LoaderOptions o, string csv)
        {
            o.IncludeTenants = o.IncludeUsers = o.IncludeCredentials = o.IncludeLockAudit = o.IncludeRequestHistory = o.IncludeActiveLocks = false;
            SetEntities(o, csv, true);
        }

        private static void ApplySkip(LoaderOptions o, string csv)
        {
            SetEntities(o, csv, false);
        }

        private static void SetEntities(LoaderOptions o, string csv, bool value)
        {
            foreach (string raw in csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (raw.Trim().ToLowerInvariant())
                {
                    case "tenants": o.IncludeTenants = value; break;
                    case "users": o.IncludeUsers = value; break;
                    case "credentials": o.IncludeCredentials = value; break;
                    case "lock-audit": case "lockaudit": case "audit": o.IncludeLockAudit = value; break;
                    case "request-history": case "requests": case "requesthistory": o.IncludeRequestHistory = value; break;
                    case "active-locks": case "locks": case "activelocks": o.IncludeActiveLocks = value; break;
                    default: throw new ArgumentException("Unknown entity in --only/--skip: " + raw);
                }
            }
        }

        private static int ParseInt(string v, string name)
        {
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) return r;
            throw new ArgumentException("--" + name + " must be an integer, got '" + v + "'.");
        }

        private static double ParseDouble(string v, string name)
        {
            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double r)) return r;
            throw new ArgumentException("--" + name + " must be a number, got '" + v + "'.");
        }

        private static bool ParseBool(string v)
        {
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime ParseUtc(string v, string name)
        {
            if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime r)) return r;
            throw new ArgumentException("--" + name + " must be an ISO-8601 date/time, got '" + v + "'.");
        }

        private static LoadLevel ParseLoad(string v)
        {
            return v.ToLowerInvariant() switch
            {
                "light" => LoadLevel.Light,
                "medium" => LoadLevel.Medium,
                "heavy" => LoadLevel.Heavy,
                "extreme" => LoadLevel.Extreme,
                _ => throw new ArgumentException("--load must be light|medium|heavy|extreme, got '" + v + "'.")
            };
        }

        /// <summary>Help text.</summary>
        public const string HelpText = @"Clutch.DataLoader — fill a Clutch deployment with realistic, backdated activity.

Writes lock-audit, request-history, active-lock, tenant, user, and credential records directly
through the Clutch data layer, with timestamps spread across a time window so the dashboard charts
show a lifelike history. Safe to re-run: synthetic rows are marked and (by default) replaced.

USAGE
  Clutch.DataLoader [options]

CONNECTION
  --db-host <host>              Postgres host (default: localhost)
  --db-port <port>             Postgres port (default: 5432)
  --db-name <name>             Database name (default: clutch)
  --db-user <user>             Username (default: postgres)
  --db-password <pw>           Password (default: postgres)
  --settings <path>            Read the Database section from a clutch.json instead of the flags above

TIME WINDOW
  --days <n>                   Window length ending now (default: 7). Sets --from = --to - n days.
  --from <iso>                 Explicit window start (UTC ISO-8601). Overrides --days.
  --to <iso>                   Explicit window end (default: now)

STRUCTURE
  --tenants <n>                Number of tenants to populate (default: 3; reuses/creates by company name)
  --users-per-tenant <n>       Users per tenant (default: 6)
  --credentials-per-tenant <n> Application keys per tenant (default: 4)
  --lock-keys <n>              Distinct lock keys per tenant (default: 30)
  --active-locks <n>           Live lock holders per tenant now (default: from --load)

DENSITY (per tenant per day; scaled across the window)
  --load <level>               light | medium | heavy | extreme (default: heavy)
  --lock-events-per-day <n>    Override lock-audit events per tenant per day
  --requests-per-day <n>       Override request-history rows per tenant per day
  --error-rate <0..1>          Fraction of failed requests / denied locks (default: from --load)
  --randomness <0..1>          Variance in rates, spikes, and latencies (default: 0.5)
  --business-hours <bool>      Weight activity toward weekday business hours (default: true)
  --no-business-hours          Flat, round-the-clock activity

SELECTION
  --only <a,b,...>             Generate only these: tenants,users,credentials,lock-audit,request-history,active-locks
  --skip <a,b,...>             Skip these entity types

CONTROL
  --seed <n>                   RNG seed for reproducible output (default: random)
  --replace <bool>             Purge prior synthetic rows before loading (default: true)
  --no-replace                 Append without purging
  --purge-only                 Purge synthetic rows and exit (no generation)
  --dry-run                    Print the plan and counts without writing
  --concurrency <n>            Parallel insert workers (default: 16)
  --verbose                    Extra progress output
  --quiet                      Only print the final summary
  --help                       Show this help

EXAMPLES
  # A dense week for screenshots, against the local Docker Postgres (from inside the compose network):
  Clutch.DataLoader --db-host postgres --load heavy --days 7 --tenants 4

  # Reproducible medium load over a custom window:
  Clutch.DataLoader --load medium --from 2026-08-01T00:00:00Z --to 2026-08-08T00:00:00Z --seed 42

  # Remove everything this loader created:
  Clutch.DataLoader --purge-only
";
    }
}

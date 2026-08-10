namespace Clutch.DataLoader
{
    using System.Collections.Generic;

    /// <summary>
    /// Curated value lists that give the synthetic data a realistic feel: tenant company names, people
    /// names, lock-key shapes, and the REST route catalog used for request-history rows.
    /// </summary>
    public static class Catalogs
    {
        /// <summary>Marker written to synthetic rows so they can be safely purged and re-loaded.</summary>
        public const string SyntheticMarker = "clutch-data-loader";

        /// <summary>Tenant company names.</summary>
        public static readonly IReadOnlyList<string> Companies = new List<string>
        {
            "Acme Robotics", "Northwind Trading", "Globex Systems", "Umbrella Foods", "Initech Software",
            "Hooli Cloud", "Stark Industries", "Wayne Logistics", "Wonka Manufacturing", "Cyberdyne Analytics",
            "Soylent Retail", "Tyrell Media", "Aperture Labs", "Massive Dynamic", "Vandelay Imports"
        };

        /// <summary>First names for synthetic users.</summary>
        public static readonly IReadOnlyList<string> FirstNames = new List<string>
        {
            "Ava", "Liam", "Noah", "Emma", "Olivia", "Mateo", "Sofia", "Kai", "Priya", "Yuki",
            "Lucas", "Mia", "Ethan", "Amara", "Diego", "Hana", "Omar", "Zoe", "Ivan", "Nadia"
        };

        /// <summary>Last names for synthetic users.</summary>
        public static readonly IReadOnlyList<string> LastNames = new List<string>
        {
            "Nguyen", "Patel", "Garcia", "Kim", "Muller", "Rossi", "Silva", "Okafor", "Haddad", "Novak",
            "Anderson", "Ivanova", "Costa", "Tanaka", "Diallo", "Reyes", "Larsson", "Weber", "Cohen", "Park"
        };

        /// <summary>Credential (application key) names.</summary>
        public static readonly IReadOnlyList<string> CredentialNames = new List<string>
        {
            "ci-runner", "prod-worker", "batch-scheduler", "inventory-sync", "checkout-service",
            "reporting-job", "cache-warmer", "migration-tool", "edge-agent", "backup-runner"
        };

        /// <summary>Lock-key name fragments; combined into realistic keys.</summary>
        public static readonly IReadOnlyList<string> KeyResources = new List<string>
        {
            "orders", "inventory", "account", "invoice", "shipment", "payment", "cart", "session",
            "ledger", "job-queue", "sku", "warehouse", "customer", "batch", "report", "index"
        };

        /// <summary>Key naming shapes (0 = resource, 1 = number).</summary>
        public static readonly IReadOnlyList<string> KeyShapes = new List<string>
        {
            "{0}-{1}", "{0}:{1}", "{0}/{1}", "{0}.lock.{1}", "{0}_{1}"
        };

        /// <summary>User agents for request-history rows.</summary>
        public static readonly IReadOnlyList<(string Item, double Weight)> UserAgents = new List<(string, double)>
        {
            ("clutch-sdk-csharp/0.1.0", 5),
            ("clutch-sdk-python/0.1.0", 3),
            ("clutch-sdk-js/0.1.0", 3),
            ("PostmanRuntime/7.39.0", 1),
            ("curl/8.6.0", 1)
        };

        /// <summary>
        /// A REST route the loader emits into request history. StatusKind selects the success-path status.
        /// </summary>
        public sealed class Route
        {
            /// <summary>HTTP method.</summary>
            public string Method { get; set; } = "GET";

            /// <summary>Route template; {tid} and {key} are substituted per request.</summary>
            public string Template { get; set; } = "/";

            /// <summary>Relative frequency weight.</summary>
            public double Weight { get; set; } = 1;

            /// <summary>Success status code (200 or 201).</summary>
            public int SuccessStatus { get; set; } = 200;

            /// <summary>Typical latency mean in milliseconds.</summary>
            public double LatencyMean { get; set; } = 10;

            /// <summary>Whether this route can be denied with 409 (lock acquire).</summary>
            public bool CanConflict { get; set; } = false;

            /// <summary>Whether a request body should be recorded.</summary>
            public string? Body { get; set; } = null;
        }

        /// <summary>The weighted route catalog, dominated by lock traffic with a realistic read/write mix.</summary>
        public static readonly IReadOnlyList<Route> Routes = new List<Route>
        {
            new Route { Method = "POST", Template = "/v1.0/api/tenants/{tid}/locks/{key}/acquire", Weight = 34, SuccessStatus = 201, LatencyMean = 14, CanConflict = true, Body = "{\"mode\":\"Write\",\"behavior\":\"FailFast\",\"leaseMs\":30000}" },
            new Route { Method = "POST", Template = "/v1.0/api/tenants/{tid}/locks/{key}/release", Weight = 26, SuccessStatus = 200, LatencyMean = 9, Body = "{\"holderId\":\"lkh_...\",\"sessionId\":\"...\"}" },
            new Route { Method = "POST", Template = "/v1.0/api/tenants/{tid}/lock-sessions/{sid}/heartbeat", Weight = 16, SuccessStatus = 200, LatencyMean = 7, Body = "{\"holderIds\":[\"lkh_...\"]}" },
            new Route { Method = "GET", Template = "/v1.0/api/tenants/{tid}/locks", Weight = 8, SuccessStatus = 200, LatencyMean = 12 },
            new Route { Method = "GET", Template = "/v1.0/api/tenants/{tid}/locks/{key}", Weight = 5, SuccessStatus = 200, LatencyMean = 8 },
            new Route { Method = "GET", Template = "/v1.0/api/tenants/{tid}/lock-audit", Weight = 4, SuccessStatus = 200, LatencyMean = 22 },
            new Route { Method = "GET", Template = "/v1.0/api/tenants/{tid}/lock-audit/summary", Weight = 3, SuccessStatus = 200, LatencyMean = 41 },
            new Route { Method = "GET", Template = "/v1.0/api/request-history", Weight = 2, SuccessStatus = 200, LatencyMean = 28 },
            new Route { Method = "GET", Template = "/v1.0/api/tenants/{tid}/users", Weight = 2, SuccessStatus = 200, LatencyMean = 11 },
            new Route { Method = "GET", Template = "/v1.0/api/tenants/{tid}/credentials", Weight = 2, SuccessStatus = 200, LatencyMean = 11 },
            new Route { Method = "POST", Template = "/v1.0/token", Weight = 3, SuccessStatus = 200, LatencyMean = 18, Body = "{\"accessKey\":\"...\"}" },
            new Route { Method = "GET", Template = "/v1.0/api/health", Weight = 3, SuccessStatus = 200, LatencyMean = 3 },
            new Route { Method = "GET", Template = "/v1.0/api/server-info", Weight = 1, SuccessStatus = 200, LatencyMean = 6 }
        };
    }
}

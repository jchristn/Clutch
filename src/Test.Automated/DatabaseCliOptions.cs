namespace Test.Automated
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Parses database selection and connection details from command-line arguments and projects them onto
    /// the <c>CLUTCH_TEST_*</c> environment variables that <see cref="Test.Shared.ClutchSuites"/> reads. This
    /// keeps a single configuration seam: the console runner accepts friendly flags, while the shared suite
    /// layer (and therefore the xUnit and NUnit runners) continues to resolve everything from the environment.
    /// </summary>
    public sealed class DatabaseCliOptions
    {
        #region Public-Members

        /// <summary>
        /// Path to export JSON results to, or null when not requested (<c>--results &lt;path&gt;</c>).
        /// </summary>
        public string? ResultsPath { get; private set; } = null;

        /// <summary>
        /// True when the caller asked for usage help (<c>--help</c> or <c>-h</c>).
        /// </summary>
        public bool ShowHelp { get; private set; } = false;

        #endregion

        #region Private-Members

        private readonly Dictionary<string, string> _EnvOverrides = new Dictionary<string, string>(StringComparer.Ordinal);

        #endregion

        #region Constructors-and-Factories

        private DatabaseCliOptions()
        {
        }

        /// <summary>
        /// Parses the supplied argument vector into a <see cref="DatabaseCliOptions"/> instance.
        /// </summary>
        /// <param name="args">Raw command-line arguments; may be null or empty.</param>
        /// <returns>The parsed options. Never null.</returns>
        /// <exception cref="ArgumentException">Thrown when a flag that requires a value is missing one, or when an unknown database type is supplied.</exception>
        public static DatabaseCliOptions Parse(string[] args)
        {
            DatabaseCliOptions options = new DatabaseCliOptions();
            if (args == null || args.Length == 0) return options;

            string? prefix = null;
            string? host = null;
            string? port = null;
            string? database = null;
            string? schema = null;
            string? username = null;
            string? password = null;
            string? filePath = null;
            string? providers = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    case "--results":
                        options.ResultsPath = RequireValue(args, ref i, arg);
                        break;
                    case "--type":
                    case "--provider":
                        prefix = ResolvePrefix(RequireValue(args, ref i, arg), out string canonical);
                        providers = canonical;
                        break;
                    case "--providers":
                        providers = RequireValue(args, ref i, arg);
                        break;
                    case "--host":
                        host = RequireValue(args, ref i, arg);
                        break;
                    case "--port":
                        port = RequireValue(args, ref i, arg);
                        break;
                    case "--database":
                    case "--db":
                        database = RequireValue(args, ref i, arg);
                        break;
                    case "--schema":
                        schema = RequireValue(args, ref i, arg);
                        break;
                    case "--username":
                    case "--user":
                        username = RequireValue(args, ref i, arg);
                        break;
                    case "--password":
                    case "--pass":
                        password = RequireValue(args, ref i, arg);
                        break;
                    case "--filepath":
                    case "--file":
                        filePath = RequireValue(args, ref i, arg);
                        break;
                    default:
                        throw new ArgumentException("Unknown argument: " + arg + ". Use --help for usage.");
                }
            }

            if (!string.IsNullOrEmpty(providers)) options._EnvOverrides["CLUTCH_TEST_PROVIDERS"] = providers!;

            if (!string.IsNullOrEmpty(filePath)) options._EnvOverrides["CLUTCH_TEST_SQLITE_FILEPATH"] = filePath!;

            if (prefix != null)
            {
                Assign(options._EnvOverrides, prefix, "HOST", host);
                Assign(options._EnvOverrides, prefix, "PORT", port);
                Assign(options._EnvOverrides, prefix, "DATABASE", database);
                Assign(options._EnvOverrides, prefix, "SCHEMA", schema);
                Assign(options._EnvOverrides, prefix, "USERNAME", username);
                Assign(options._EnvOverrides, prefix, "PASSWORD", password);
            }
            else if (host != null || port != null || database != null || schema != null || username != null || password != null)
            {
                throw new ArgumentException("Connection flags (--host/--port/--database/--schema/--username/--password) require --type to select a provider.");
            }

            return options;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Writes the resolved overrides into the current process environment so that the shared suite layer
        /// resolves the requested provider and connection details. Existing environment values are replaced.
        /// </summary>
        public void ApplyToEnvironment()
        {
            foreach (KeyValuePair<string, string> pair in _EnvOverrides)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Returns the usage text describing every supported flag.
        /// </summary>
        /// <returns>A multi-line usage string.</returns>
        public static string UsageText()
        {
            return
                "Test.Automated — Clutch shared test runner\n\n" +
                "Usage: Test.Automated [options]\n\n" +
                "Database selection:\n" +
                "  --type <t>        Provider to run: sqlite | postgresql | mysql | sqlserver\n" +
                "  --providers <csv> Run a comma-separated matrix (e.g. sqlite,postgresql,mysql,sqlserver)\n" +
                "  --host <h>        Database host (networked providers)\n" +
                "  --port <p>        Database port\n" +
                "  --database <d>    Database/catalog name\n" +
                "  --schema <s>      Schema/namespace (PostgreSQL and SQL Server)\n" +
                "  --username <u>    Login user\n" +
                "  --password <pw>   Login password\n" +
                "  --filepath <f>    SQLite database file path\n\n" +
                "Output:\n" +
                "  --results <path>  Export JSON results to the given path\n" +
                "  --help, -h        Show this help\n\n" +
                "Any flag may also be supplied via CLUTCH_TEST_* environment variables; the flags above take precedence.";
        }

        #endregion

        #region Private-Methods

        private static string RequireValue(string[] args, ref int index, string flag)
        {
            if (index + 1 >= args.Length) throw new ArgumentException("Missing value for " + flag + ".");
            index++;
            return args[index];
        }

        private static void Assign(Dictionary<string, string> map, string prefix, string suffix, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            map["CLUTCH_TEST_" + prefix + "_" + suffix] = value!;
        }

        private static string ResolvePrefix(string type, out string canonical)
        {
            string normalized = type.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "sqlite":
                    canonical = "sqlite";
                    return "SQLITE";
                case "postgres":
                case "postgresql":
                case "pg":
                    canonical = "postgresql";
                    return "PG";
                case "mysql":
                case "mariadb":
                    canonical = "mysql";
                    return "MYSQL";
                case "sqlserver":
                case "mssql":
                case "sql-server":
                    canonical = "sqlserver";
                    return "MSSQL";
                default:
                    throw new ArgumentException("Unknown database type: " + type + ". Expected sqlite, postgresql, mysql, or sqlserver.");
            }
        }

        #endregion
    }
}

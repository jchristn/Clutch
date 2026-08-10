namespace Clutch.DataLoader
{
    using System;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Reads the <c>Database</c> section of a Clutch settings file (clutch.json) into loader options, so the
    /// loader can point at the same database the server uses without repeating the connection flags.
    /// </summary>
    public static class SettingsLoader
    {
        /// <summary>Apply the Database section of a settings JSON file to the options.</summary>
        /// <param name="options">Options to update.</param>
        /// <param name="path">Settings file path.</param>
        public static void Apply(LoaderOptions options, string path)
        {
            if (!File.Exists(path)) throw new ArgumentException("--settings file not found: " + path);
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Database", out JsonElement db)
                && !doc.RootElement.TryGetProperty("database", out db))
            {
                return;
            }

            if (TryString(db, "Host", "host", out string host)) options.DbHost = host;
            if (TryInt(db, "Port", "port", out int port)) options.DbPort = port;
            if (TryString(db, "DatabaseName", "databaseName", out string name)) options.DbName = name;
            if (TryString(db, "Username", "username", out string user)) options.DbUser = user;
            if (TryString(db, "Password", "password", out string pw)) options.DbPassword = pw;
        }

        private static bool TryString(JsonElement obj, string a, string b, out string value)
        {
            if ((obj.TryGetProperty(a, out JsonElement e) || obj.TryGetProperty(b, out e)) && e.ValueKind == JsonValueKind.String)
            {
                value = e.GetString() ?? string.Empty;
                return !string.IsNullOrEmpty(value);
            }
            value = string.Empty;
            return false;
        }

        private static bool TryInt(JsonElement obj, string a, string b, out int value)
        {
            if ((obj.TryGetProperty(a, out JsonElement e) || obj.TryGetProperty(b, out e)) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out value))
            {
                return true;
            }
            value = 0;
            return false;
        }
    }
}

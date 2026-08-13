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
        #region Private-Members

        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        #endregion

        #region Public-Methods

        /// <summary>Apply the Database section of a settings JSON file to the options.</summary>
        /// <param name="options">Options to update.</param>
        /// <param name="path">Settings file path.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="path"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the settings file does not exist or cannot be parsed as JSON.</exception>
        public static void Apply(LoaderOptions options, string path)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new ArgumentException("--settings file not found: " + path);

            SettingsDocument? document;

            try
            {
                document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(path), _JsonOptions);
            }
            catch (JsonException e)
            {
                throw new ArgumentException("--settings file is not valid JSON: " + path, e);
            }

            DatabaseSection? database = document?.Database;
            if (database == null) return;

            if (!string.IsNullOrEmpty(database.Host)) options.DbHost = database.Host;
            if (database.Port.HasValue) options.DbPort = database.Port.Value;
            if (!string.IsNullOrEmpty(database.DatabaseName)) options.DbName = database.DatabaseName;
            if (!string.IsNullOrEmpty(database.Username)) options.DbUser = database.Username;
            if (!string.IsNullOrEmpty(database.Password)) options.DbPassword = database.Password;
        }

        #endregion
    }
}

namespace Clutch.Server.Serialization
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;

    /// <summary>
    /// Centralized JSON serialization for API responses and WebSocket messages. Enums serialize as
    /// strings; property names use camelCase to match the dashboard and SDK expectations.
    /// </summary>
    public static class Json
    {
        #region Private-Members

        private static readonly JsonSerializerOptions _Options = BuildOptions();

        #endregion

        #region Public-Methods

        /// <summary>
        /// The shared serializer options.
        /// </summary>
        public static JsonSerializerOptions Options
        {
            get
            {
                return _Options;
            }
        }

        /// <summary>
        /// Serialize a value to JSON.
        /// </summary>
        /// <param name="value">Value to serialize.</param>
        /// <returns>JSON string.</returns>
        public static string Serialize(object? value)
        {
            if (value == null) return "null";
            return JsonSerializer.Serialize(value, _Options.GetTypeInfo(value.GetType()));
        }

        /// <summary>
        /// Deserialize JSON to a typed value.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>The deserialized value, or null.</returns>
        public static T? Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            return JsonSerializer.Deserialize(json, (JsonTypeInfo<T>)_Options.GetTypeInfo(typeof(T)));
        }

        #endregion

        #region Private-Methods

        private static JsonSerializerOptions BuildOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.TypeInfoResolver = ServerJsonContext.Default;
            return options;
        }

        #endregion
    }
}

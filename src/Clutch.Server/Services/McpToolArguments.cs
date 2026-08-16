namespace Clutch.Server.Services
{
    using System.Text.Json;
    using Clutch.Core.Enumeration;

    /// <summary>
    /// Helpers for reading MCP tool-call arguments out of the <see cref="JsonElement"/> payload delivered by
    /// the Voltaic.Mcp server. MCP clients send tool arguments as a JSON object, so these helpers tolerate a
    /// missing object, absent properties, and values encoded either as native JSON types or as strings.
    /// </summary>
    internal static class McpToolArguments
    {
        /// <summary>
        /// Build a pagination query from the optional <c>maxResults</c> and <c>skip</c> arguments. Absent or
        /// unparseable values leave the query at its defaults (which the query itself clamps to valid ranges).
        /// </summary>
        /// <param name="args">The MCP tool-call argument object, or null when the client sent none.</param>
        /// <returns>A populated enumeration query.</returns>
        public static EnumerationQuery BuildQuery(JsonElement? args)
        {
            EnumerationQuery query = new EnumerationQuery();
            int? maxResults = GetInt(args, "maxResults");
            if (maxResults.HasValue) query.MaxResults = maxResults.Value;
            int? skip = GetInt(args, "skip");
            if (skip.HasValue) query.Skip = skip.Value;
            return query;
        }

        /// <summary>
        /// Read a string argument. Numbers are returned as their textual form; missing properties, non-object
        /// payloads, and other value kinds yield an empty string.
        /// </summary>
        /// <param name="args">The MCP tool-call argument object, or null.</param>
        /// <param name="name">The property name to read.</param>
        /// <returns>The string value, or an empty string when absent or not a scalar.</returns>
        public static string GetString(JsonElement? args, string name)
        {
            if (!TryGetProperty(args, name, out JsonElement prop)) return string.Empty;
            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString() ?? string.Empty,
                JsonValueKind.Number => prop.ToString(),
                _ => string.Empty
            };
        }

        /// <summary>
        /// Read an integer argument. Accepts a JSON number or a numeric string; anything else yields null.
        /// </summary>
        /// <param name="args">The MCP tool-call argument object, or null.</param>
        /// <param name="name">The property name to read.</param>
        /// <returns>The integer value, or null when absent or not numeric.</returns>
        public static int? GetInt(JsonElement? args, string name)
        {
            if (!TryGetProperty(args, name, out JsonElement prop)) return null;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out long number)) return (int)number;
            if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out long parsed)) return (int)parsed;
            return null;
        }

        private static bool TryGetProperty(JsonElement? args, string name, out JsonElement value)
        {
            if (args.HasValue && args.Value.ValueKind == JsonValueKind.Object && args.Value.TryGetProperty(name, out value))
                return true;
            value = default;
            return false;
        }
    }
}

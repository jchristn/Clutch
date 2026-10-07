namespace Clutch.Core.Serialization
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Clutch.Core.Security;

    /// <summary>
    /// Source-generated JSON metadata for the values Clutch.Core serializes itself (session token payloads and
    /// stored request headers). Uses the default serializer options, so the JSON is identical to the
    /// reflection-based output it replaces, and keeps the library trimming and Native AOT safe.
    /// </summary>
    [JsonSerializable(typeof(TokenPayload))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class CoreJsonContext : JsonSerializerContext
    {
    }
}

namespace Clutch.Server.Serialization
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Clutch.Core.Database;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Models;
    using Clutch.Core.Requests;
    using Clutch.Core.Responses;
    using Clutch.Server.Responses;
    using Clutch.Server.Settings;
    using Clutch.Server.WebSocket;

    /// <summary>
    /// Source-generated JSON metadata for every type the server reads or writes: REST request and response
    /// bodies, WebSocket frames, MCP tool results, and the settings file. It is the only metadata source the
    /// server's serializer options use, so a type missing here fails in every test run (not only under Native
    /// AOT). The options it is attached to keep their own naming, casing, and null-handling settings.
    /// </summary>
    // Requests.
    [JsonSerializable(typeof(LoginRequest))]
    [JsonSerializable(typeof(CreateCredentialRequest))]
    [JsonSerializable(typeof(CreateUserRequest))]
    [JsonSerializable(typeof(LockAcquireHttpRequest))]
    [JsonSerializable(typeof(LockHeartbeatHttpRequest))]
    [JsonSerializable(typeof(LockReleaseHttpRequest))]
    [JsonSerializable(typeof(NukeTenantRequest))]
    [JsonSerializable(typeof(WsInboundMessage))]
    // Models and Core responses.
    [JsonSerializable(typeof(Tenant))]
    [JsonSerializable(typeof(User))]
    [JsonSerializable(typeof(Credential))]
    [JsonSerializable(typeof(CredentialResponse))]
    [JsonSerializable(typeof(RequestHistoryEntry))]
    [JsonSerializable(typeof(RequestHistorySummary))]
    [JsonSerializable(typeof(LockChartSummary))]
    [JsonSerializable(typeof(NukeTenantResult))]
    [JsonSerializable(typeof(ErrorResponse))]
    [JsonSerializable(typeof(EnumerationResult<Tenant>))]
    [JsonSerializable(typeof(EnumerationResult<User>))]
    [JsonSerializable(typeof(EnumerationResult<Credential>))]
    [JsonSerializable(typeof(EnumerationResult<CredentialResponse>))]
    [JsonSerializable(typeof(EnumerationResult<LockHolder>))]
    [JsonSerializable(typeof(EnumerationResult<LockAuditEntry>))]
    [JsonSerializable(typeof(EnumerationResult<RequestHistoryEntry>))]
    // Server responses and WebSocket frames.
    [JsonSerializable(typeof(LockKeyResponse))]
    [JsonSerializable(typeof(LockAcquireGrantedResponse))]
    [JsonSerializable(typeof(LockAcquireDeniedResponse))]
    [JsonSerializable(typeof(LockReleaseResponse))]
    [JsonSerializable(typeof(SessionHeartbeatResponse))]
    [JsonSerializable(typeof(SessionReleaseResponse))]
    [JsonSerializable(typeof(ForceReleaseResponse))]
    [JsonSerializable(typeof(ServerInfoResponse))]
    [JsonSerializable(typeof(SettingsSaveResponse))]
    [JsonSerializable(typeof(RestartResponse))]
    [JsonSerializable(typeof(DatabaseTestResponse))]
    [JsonSerializable(typeof(DeleteCountResponse))]
    [JsonSerializable(typeof(McpServerInfoResult))]
    [JsonSerializable(typeof(WsWelcomeMessage))]
    [JsonSerializable(typeof(WsTypeMessage))]
    [JsonSerializable(typeof(WsErrorMessage))]
    [JsonSerializable(typeof(WsAcquiredMessage))]
    [JsonSerializable(typeof(WsDeniedMessage))]
    [JsonSerializable(typeof(WsReleasedMessage))]
    [JsonSerializable(typeof(WsHeartbeatMessage))]
    // Settings.
    [JsonSerializable(typeof(ClutchSettings))]
    [JsonSerializable(typeof(DatabaseSettings))]
    // Loosely typed bodies (token and principal responses) and their value types.
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(long))]
    internal partial class ServerJsonContext : JsonSerializerContext
    {
    }
}

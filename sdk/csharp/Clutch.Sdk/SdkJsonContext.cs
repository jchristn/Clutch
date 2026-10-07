namespace Clutch.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Source-generated JSON metadata for every type the SDK sends or receives. The clients attach it to their
    /// runtime serializer options (metadata mode), so their naming, casing, null-handling, and enum settings keep
    /// applying exactly as before, and the SDK is trimming and Native AOT safe.
    /// </summary>
    [JsonSerializable(typeof(AccessKeyLoginBody))]
    [JsonSerializable(typeof(PasswordLoginBody))]
    [JsonSerializable(typeof(TenantBody))]
    [JsonSerializable(typeof(CreateUserBody))]
    [JsonSerializable(typeof(UpdateUserBody))]
    [JsonSerializable(typeof(CreateCredentialBody))]
    [JsonSerializable(typeof(TokenResponse))]
    [JsonSerializable(typeof(TokenDetails))]
    [JsonSerializable(typeof(HealthResponse))]
    [JsonSerializable(typeof(ServerInfo))]
    [JsonSerializable(typeof(Tenant))]
    [JsonSerializable(typeof(User))]
    [JsonSerializable(typeof(Credential))]
    [JsonSerializable(typeof(LockKeyDetail))]
    [JsonSerializable(typeof(ReleaseLockResult))]
    [JsonSerializable(typeof(LockAuditSummary))]
    [JsonSerializable(typeof(RequestHistorySummary))]
    [JsonSerializable(typeof(RequestHistoryEntry))]
    [JsonSerializable(typeof(DeleteResult))]
    [JsonSerializable(typeof(EnumerationResult<Tenant>))]
    [JsonSerializable(typeof(EnumerationResult<User>))]
    [JsonSerializable(typeof(EnumerationResult<Credential>))]
    [JsonSerializable(typeof(EnumerationResult<LockHolder>))]
    [JsonSerializable(typeof(EnumerationResult<LockAuditEntry>))]
    [JsonSerializable(typeof(EnumerationResult<RequestHistoryEntry>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(LockPolicy))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(int))]
    internal partial class SdkJsonContext : JsonSerializerContext
    {
    }
}

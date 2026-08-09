namespace Clutch.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Npgsql;

    /// <summary>
    /// Maps PostgreSQL result rows to Clutch domain models, and provides typed column-read helpers.
    /// </summary>
    public static class Converters
    {
        #region Public-Methods

        /// <summary>
        /// Read a non-null string column.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="column">Column name.</param>
        /// <returns>The value, or empty string if null.</returns>
        public static string Str(NpgsqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        /// <summary>
        /// Read a nullable string column.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="column">Column name.</param>
        /// <returns>The value, or null.</returns>
        public static string? NullableStr(NpgsqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        /// <summary>
        /// Read a 32-bit integer column.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="column">Column name.</param>
        /// <returns>The value, or 0 if null.</returns>
        public static int Int(NpgsqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }

        /// <summary>
        /// Read a 64-bit integer column.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="column">Column name.</param>
        /// <returns>The value, or 0 if null.</returns>
        public static long Long(NpgsqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt64(ordinal);
        }

        /// <summary>
        /// Read a nullable 64-bit integer column.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="column">Column name.</param>
        /// <returns>The value, or null.</returns>
        public static long? NullableLong(NpgsqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
        }

        /// <summary>
        /// Read a double column.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="column">Column name.</param>
        /// <returns>The value, or 0 if null.</returns>
        public static double Double(NpgsqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetDouble(ordinal);
        }

        /// <summary>
        /// Read a boolean column.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="column">Column name.</param>
        /// <returns>The value, or false if null.</returns>
        public static bool Bool(NpgsqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
        }

        /// <summary>
        /// Read a non-null UTC timestamp column.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="column">Column name.</param>
        /// <returns>The value, or DateTime.UtcNow if null.</returns>
        public static DateTime Date(NpgsqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? DateTime.UtcNow : DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
        }

        /// <summary>
        /// Read a nullable UTC timestamp column.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="column">Column name.</param>
        /// <returns>The value, or null.</returns>
        public static DateTime? NullableDate(NpgsqlDataReader reader, string column)
        {
            int ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
        }

        /// <summary>
        /// Map a row to a tenant.
        /// </summary>
        /// <param name="reader">Reader positioned on a tenants row.</param>
        /// <returns>The tenant.</returns>
        public static Tenant ToTenant(NpgsqlDataReader reader)
        {
            Tenant tenant = new Tenant();
            tenant.Id = Str(reader, "id");
            tenant.Name = Str(reader, "name");
            tenant.LockHistoryRetentionDays = Int(reader, "lockhistoryretentiondays");
            tenant.DefaultLeaseMs = Int(reader, "defaultleasems");
            tenant.MaxLeaseMs = Int(reader, "maxleasems");
            tenant.Active = Bool(reader, "active");
            tenant.IsProtected = Bool(reader, "isprotected");
            tenant.CreatedUtc = Date(reader, "createdutc");
            tenant.LastUpdateUtc = Date(reader, "lastupdateutc");
            return tenant;
        }

        /// <summary>
        /// Map a row to a user.
        /// </summary>
        /// <param name="reader">Reader positioned on a users row.</param>
        /// <returns>The user.</returns>
        public static User ToUser(NpgsqlDataReader reader)
        {
            User user = new User();
            user.Id = Str(reader, "id");
            user.TenantId = Str(reader, "tenantid");
            user.FirstName = Str(reader, "firstname");
            user.LastName = Str(reader, "lastname");
            user.Email = Str(reader, "email");
            user.PasswordSha256 = Str(reader, "passwordsha256");
            user.IsSystemAdmin = Bool(reader, "issystemadmin");
            user.IsTenantAdmin = Bool(reader, "istenantadmin");
            user.Active = Bool(reader, "active");
            user.IsProtected = Bool(reader, "isprotected");
            user.CreatedUtc = Date(reader, "createdutc");
            user.LastUpdateUtc = Date(reader, "lastupdateutc");
            return user;
        }

        /// <summary>
        /// Map a row to a credential.
        /// </summary>
        /// <param name="reader">Reader positioned on a credentials row.</param>
        /// <returns>The credential.</returns>
        public static Credential ToCredential(NpgsqlDataReader reader)
        {
            Credential credential = new Credential();
            credential.Id = Str(reader, "id");
            credential.TenantId = Str(reader, "tenantid");
            credential.UserId = NullableStr(reader, "userid");
            credential.Name = Str(reader, "name");
            credential.AccessKey = Str(reader, "accesskey");
            credential.AuthMode = Enum.Parse<CredentialAuthModeEnum>(Str(reader, "authmode"));
            credential.LastUsedUtc = NullableDate(reader, "lastusedutc");
            credential.ExpiresUtc = NullableDate(reader, "expiresutc");
            credential.Active = Bool(reader, "active");
            credential.IsProtected = Bool(reader, "isprotected");
            credential.CreatedUtc = Date(reader, "createdutc");
            credential.LastUpdateUtc = Date(reader, "lastupdateutc");
            return credential;
        }

        /// <summary>
        /// Map a row to an authentication session.
        /// </summary>
        /// <param name="reader">Reader positioned on an auth_sessions row.</param>
        /// <returns>The session.</returns>
        public static AuthSession ToAuthSession(NpgsqlDataReader reader)
        {
            AuthSession session = new AuthSession();
            session.Id = Str(reader, "id");
            session.TenantId = Str(reader, "tenantid");
            session.UserId = NullableStr(reader, "userid");
            session.CredentialId = NullableStr(reader, "credentialid");
            session.PrincipalType = Enum.Parse<PrincipalTypeEnum>(Str(reader, "principaltype"));
            session.TokenId = Str(reader, "tokenid");
            session.SourceIp = NullableStr(reader, "sourceip");
            session.UserAgent = NullableStr(reader, "useragent");
            session.ExpiresUtc = Date(reader, "expiresutc");
            session.LastUsedUtc = NullableDate(reader, "lastusedutc");
            session.RevokedUtc = NullableDate(reader, "revokedutc");
            session.RevocationReason = NullableStr(reader, "revocationreason");
            session.Active = Bool(reader, "active");
            session.IsProtected = Bool(reader, "isprotected");
            session.CreatedUtc = Date(reader, "createdutc");
            session.LastUpdateUtc = Date(reader, "lastupdateutc");
            return session;
        }

        /// <summary>
        /// Map a row to a lock definition.
        /// </summary>
        /// <param name="reader">Reader positioned on a lock_definitions row.</param>
        /// <returns>The definition.</returns>
        public static LockDefinition ToLockDefinition(NpgsqlDataReader reader)
        {
            LockDefinition definition = new LockDefinition();
            definition.Id = Str(reader, "id");
            definition.TenantId = Str(reader, "tenantid");
            definition.LockKey = Str(reader, "lockkey");
            definition.ReadMaxHolders = Int(reader, "readmaxholders");
            definition.WriteExclusivity = Enum.Parse<WriteExclusivityEnum>(Str(reader, "writeexclusivity"));
            definition.WriteMaxHolders = Int(reader, "writemaxholders");
            definition.WriteBlocksReads = Bool(reader, "writeblocksreads");
            definition.DefaultLeaseMs = Int(reader, "defaultleasems");
            definition.MaxLeaseMs = Int(reader, "maxleasems");
            definition.MaxHoldMs = Int(reader, "maxholdms");
            definition.FencingCounter = Long(reader, "fencingcounter");
            definition.FirstAcquiredByCredentialId = NullableStr(reader, "firstacquiredbycredentialid");
            definition.Active = Bool(reader, "active");
            definition.CreatedUtc = Date(reader, "createdutc");
            definition.LastUpdateUtc = Date(reader, "lastupdateutc");
            return definition;
        }

        /// <summary>
        /// Map a row to a lock holder.
        /// </summary>
        /// <param name="reader">Reader positioned on a lock_holders row.</param>
        /// <returns>The holder.</returns>
        public static LockHolder ToLockHolder(NpgsqlDataReader reader)
        {
            LockHolder holder = new LockHolder();
            holder.Id = Str(reader, "id");
            holder.TenantId = Str(reader, "tenantid");
            holder.LockKey = Str(reader, "lockkey");
            holder.LockDefinitionId = Str(reader, "lockdefinitionid");
            holder.Mode = Enum.Parse<LockModeEnum>(Str(reader, "mode"));
            holder.CredentialId = Str(reader, "credentialid");
            holder.SessionId = Str(reader, "sessionid");
            holder.NodeId = Str(reader, "nodeid");
            holder.FencingToken = Long(reader, "fencingtoken");
            holder.AcquiredUtc = Date(reader, "acquiredutc");
            holder.LeaseExpiresUtc = Date(reader, "leaseexpiresutc");
            holder.LastHeartbeatUtc = Date(reader, "lastheartbeatutc");
            holder.Active = Bool(reader, "active");
            holder.CreatedUtc = Date(reader, "createdutc");
            holder.LastUpdateUtc = Date(reader, "lastupdateutc");
            return holder;
        }

        /// <summary>
        /// Map a row to a lock audit entry.
        /// </summary>
        /// <param name="reader">Reader positioned on a lock_audit row.</param>
        /// <returns>The entry.</returns>
        public static LockAuditEntry ToLockAuditEntry(NpgsqlDataReader reader)
        {
            LockAuditEntry entry = new LockAuditEntry();
            entry.Id = Str(reader, "id");
            entry.TenantId = Str(reader, "tenantid");
            entry.LockKey = Str(reader, "lockkey");
            string? modeStr = NullableStr(reader, "mode");
            entry.Mode = modeStr == null ? null : Enum.Parse<LockModeEnum>(modeStr);
            entry.EventType = Enum.Parse<LockEventTypeEnum>(Str(reader, "eventtype"));
            entry.CredentialId = NullableStr(reader, "credentialid");
            entry.SessionId = NullableStr(reader, "sessionid");
            entry.NodeId = Str(reader, "nodeid");
            entry.FencingToken = NullableLong(reader, "fencingtoken");
            entry.Reason = NullableStr(reader, "reason");
            entry.CreatedUtc = Date(reader, "createdutc");
            return entry;
        }

        /// <summary>
        /// Map a row to a request history entry. Body columns are optional in the projection.
        /// </summary>
        /// <param name="reader">Reader positioned on a request_history row.</param>
        /// <param name="includeBodies">Whether the projection includes header and body columns.</param>
        /// <returns>The entry.</returns>
        public static RequestHistoryEntry ToRequestHistoryEntry(NpgsqlDataReader reader, bool includeBodies)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry();
            entry.Id = Str(reader, "id");
            entry.TenantId = NullableStr(reader, "tenantid");
            entry.UserId = NullableStr(reader, "userid");
            entry.PrincipalName = NullableStr(reader, "principalname");
            entry.Method = Str(reader, "method");
            entry.Path = Str(reader, "path");
            entry.Url = Str(reader, "url");
            entry.StatusCode = Int(reader, "statuscode");
            entry.DurationMs = Double(reader, "durationms");
            entry.SourceIp = NullableStr(reader, "sourceip");
            entry.RequestBodyBytes = Long(reader, "requestbodybytes");
            entry.RequestBodyTruncated = Bool(reader, "requestbodytruncated");
            entry.ResponseBodyBytes = Long(reader, "responsebodybytes");
            entry.ResponseBodyTruncated = Bool(reader, "responsebodytruncated");
            entry.CreatedUtc = Date(reader, "createdutc");
            entry.CompletedUtc = NullableDate(reader, "completedutc");

            if (includeBodies)
            {
                entry.RequestHeaders = DeserializeHeaders(NullableStr(reader, "requestheaders"));
                entry.RequestBody = NullableStr(reader, "requestbody");
                entry.ResponseHeaders = DeserializeHeaders(NullableStr(reader, "responseheaders"));
                entry.ResponseBody = NullableStr(reader, "responsebody");
            }

            return entry;
        }

        /// <summary>
        /// Serialize a header dictionary to JSON for storage.
        /// </summary>
        /// <param name="headers">Headers.</param>
        /// <returns>JSON string.</returns>
        public static string SerializeHeaders(Dictionary<string, string> headers)
        {
            return JsonSerializer.Serialize(headers ?? new Dictionary<string, string>());
        }

        #endregion

        #region Private-Methods

        private static Dictionary<string, string> DeserializeHeaders(string? json)
        {
            if (string.IsNullOrEmpty(json)) return new Dictionary<string, string>();
            Dictionary<string, string>? result = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return result ?? new Dictionary<string, string>();
        }

        #endregion
    }
}

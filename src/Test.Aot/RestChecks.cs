namespace Test.Aot
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Clutch.Core.Services;
    using Clutch.Server.Settings;
    using static Clutch.Sdk.Test.SdkChecks;

    /// <summary>
    /// Raw REST checks for the routes and response shapes the SDK checks do not reach: OpenAPI, token context,
    /// error bodies, user and tenant updates, the HTTP lock lifecycle, settings, request history, and the
    /// administrative tenant purge. Each check asserts the JSON the server writes, so a type missing from the
    /// server's source-generated metadata, or a changed property name or enum spelling, fails here.
    /// </summary>
    internal static class RestChecks
    {
        #region Public-Methods

        /// <summary>
        /// Run the REST checks.
        /// </summary>
        /// <param name="host">The running node.</param>
        /// <returns>A task that completes when every check has run.</returns>
        public static async Task RunAsync(AotServerHost host)
        {
            using ApiClient api = new ApiClient(host.Endpoint);
            string tenantId = string.Empty;

            await CheckAsync("REST: OpenAPI document is generated", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Get, "/openapi.json").ConfigureAwait(false);
                Assert(r.Status == 200, "openapi.json: " + r.Status);
                Assert(r.String("openapi") != null, "document should carry an openapi version");
                Assert(r.Json.GetProperty("paths").TryGetProperty("/v1.0/api/tenants", out _), "paths should include /v1.0/api/tenants");
            }).ConfigureAwait(false);

            await CheckAsync("REST: health reports healthy", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Get, "/v1.0/api/health").ConfigureAwait(false);
                Assert(r.Status == 200 && r.String("status") == "healthy", r.ToString());
            }).ConfigureAwait(false);

            await CheckAsync("REST: authenticate with an access key", async () =>
            {
                string body = ApiClient.Body(w => w.WriteString("accessKey", DefaultSeeder.DefaultAccessKey));
                ApiResponse r = await api.SendAsync(HttpMethod.Post, "/v1.0/token", body).ConfigureAwait(false);
                Assert(r.Status == 200, r.ToString());
                Assert(!string.IsNullOrEmpty(r.String("token")), "token should be returned");
                Assert(r.String("principalType") == "Credential", "principalType should be Credential: " + r.Body);
                tenantId = r.String("tenantId") ?? string.Empty;
                Assert(tenantId.Length > 0, "tenantId should be returned");
                api.Token = r.String("token");
            }).ConfigureAwait(false);

            await CheckAsync("REST: token context describes the principal", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Get, "/v1.0/token").ConfigureAwait(false);
                Assert(r.Status == 200, r.ToString());
                Assert(r.Json.GetProperty("authenticated").GetBoolean(), "should be authenticated");
                Assert(r.Json.GetProperty("isAdmin").GetBoolean(), "default key belongs to the system administrator");
                Assert(r.String("principalType") == "Credential", r.Body);
            }).ConfigureAwait(false);

            await CheckAsync("REST: server info reports node, telemetry, and principal", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Get, "/v1.0/api/server-info").ConfigureAwait(false);
                Assert(r.Status == 200, r.ToString());
                Assert(r.String("product") == "Clutch" && r.String("node") == "aot-node", r.Body);
                Assert(r.String("database") == "Sqlite", "database should be Sqlite: " + r.Body);
                Assert(r.Json.GetProperty("telemetry").TryGetProperty("prometheusPath", out _), "telemetry block expected");
                Assert(r.Json.GetProperty("principal").GetProperty("authenticated").GetBoolean(), "principal block expected");
            }).ConfigureAwait(false);

            await CheckAsync("REST: missing tenant returns a typed error body", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Get, "/v1.0/api/tenants/ten_does_not_exist").ConfigureAwait(false);
                Assert(r.Status == 404, r.ToString());
                Assert(r.String("error") == "NotFound" && !string.IsNullOrEmpty(r.String("message")), r.Body);
            }).ConfigureAwait(false);

            await CheckAsync("REST: create and update a tenant", async () =>
            {
                string name = "aot-" + Guid.NewGuid().ToString("N").Substring(0, 10);
                ApiResponse created = await api.SendAsync(HttpMethod.Post, "/v1.0/api/tenants", ApiClient.Body(w => w.WriteString("name", name))).ConfigureAwait(false);
                Assert(created.Status == 201, created.ToString());
                string id = created.String("id") ?? string.Empty;
                ApiResponse updated = await api.SendAsync(HttpMethod.Put, "/v1.0/api/tenants/" + id, ApiClient.Body(w => w.WriteString("name", name + "-renamed"))).ConfigureAwait(false);
                Assert(updated.Status == 200 && updated.String("name") == name + "-renamed", updated.ToString());
                ApiResponse deleted = await api.SendAsync(HttpMethod.Delete, "/v1.0/api/tenants/" + id).ConfigureAwait(false);
                Assert(deleted.Status >= 200 && deleted.Status < 300, deleted.ToString());
            }).ConfigureAwait(false);

            await CheckAsync("REST: create, read, update, list, and delete a user", async () =>
            {
                string email = "aot-" + Guid.NewGuid().ToString("N").Substring(0, 8) + "@clutch.local";
                string users = "/v1.0/api/tenants/" + tenantId + "/users";
                ApiResponse created = await api.SendAsync(HttpMethod.Post, users, ApiClient.Body(w =>
                {
                    w.WriteString("email", email);
                    w.WriteString("password", "aot-password-1");
                    w.WriteString("firstName", "Aot");
                    w.WriteString("lastName", "User");
                })).ConfigureAwait(false);
                Assert(created.Status == 201, created.ToString());
                string id = created.String("id") ?? string.Empty;
                Assert(!created.Body.Contains("aot-password-1", StringComparison.Ordinal), "the password must not be echoed");

                ApiResponse read = await api.SendAsync(HttpMethod.Get, users + "/" + id).ConfigureAwait(false);
                Assert(read.Status == 200 && read.String("email") == email, read.ToString());

                ApiResponse updated = await api.SendAsync(HttpMethod.Put, users + "/" + id, ApiClient.Body(w =>
                {
                    w.WriteString("email", email);
                    w.WriteString("firstName", "Renamed");
                    w.WriteString("lastName", "User");
                })).ConfigureAwait(false);
                Assert(updated.Status == 200 && updated.String("firstName") == "Renamed", updated.ToString());

                ApiResponse list = await api.SendAsync(HttpMethod.Get, users + "?maxResults=100").ConfigureAwait(false);
                Assert(list.Status == 200 && list.Json.GetProperty("objects").GetArrayLength() >= 2, list.ToString());

                ApiResponse deleted = await api.SendAsync(HttpMethod.Delete, users + "/" + id).ConfigureAwait(false);
                Assert(deleted.Status >= 200 && deleted.Status < 300, deleted.ToString());
            }).ConfigureAwait(false);

            await CheckAsync("REST: credential listing writes enums as strings", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Get, "/v1.0/api/tenants/" + tenantId + "/credentials").ConfigureAwait(false);
                Assert(r.Status == 200, r.ToString());
                JsonElement first = r.Json.GetProperty("objects")[0];
                Assert(first.GetProperty("authMode").ValueKind == JsonValueKind.String, "authMode should be a string: " + r.Body);
            }).ConfigureAwait(false);

            await RunLockChecksAsync(api, tenantId).ConfigureAwait(false);
            await RunSettingsChecksAsync(api, host).ConfigureAwait(false);

            await CheckAsync("REST: request history list, summary, and filtered delete", async () =>
            {
                ApiResponse list = await api.SendAsync(HttpMethod.Get, "/v1.0/api/request-history?maxResults=5").ConfigureAwait(false);
                Assert(list.Status == 200 && list.Json.TryGetProperty("objects", out _), list.ToString());
                ApiResponse summary = await api.SendAsync(HttpMethod.Get, "/v1.0/api/request-history/summary").ConfigureAwait(false);
                Assert(summary.Status == 200 && summary.Json.TryGetProperty("buckets", out _), summary.ToString());
                ApiResponse deleted = await api.SendAsync(HttpMethod.Delete, "/v1.0/api/request-history?pathContains=" + Guid.NewGuid().ToString("N")).ConfigureAwait(false);
                Assert(deleted.Status == 200 && deleted.Json.GetProperty("deletedCount").GetInt32() == 0, deleted.ToString());
            }).ConfigureAwait(false);

            await CheckAsync("REST: administrative tenant purge", async () =>
            {
                ApiResponse created = await api.SendAsync(HttpMethod.Post, "/v1.0/api/tenants", ApiClient.Body(w => w.WriteString("name", "aot-nuke-" + Guid.NewGuid().ToString("N").Substring(0, 8)))).ConfigureAwait(false);
                string id = created.String("id") ?? string.Empty;
                ApiResponse nuked = await api.SendAsync(HttpMethod.Post, "/v1.0/api/admin/nuke/tenant", ApiClient.Body(w =>
                {
                    w.WriteString("tenantId", id);
                    w.WriteString("confirmTenantId", id);
                    w.WriteString("reason", "Native AOT test cleanup.");
                })).ConfigureAwait(false);
                Assert(nuked.Status == 200 && nuked.String("tenantId") == id, nuked.ToString());
                ApiResponse gone = await api.SendAsync(HttpMethod.Get, "/v1.0/api/tenants/" + id).ConfigureAwait(false);
                Assert(gone.Status == 404, "tenant should be gone: " + gone);
            }).ConfigureAwait(false);

            await CheckAsync("REST: logout revokes the token", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Delete, "/v1.0/token").ConfigureAwait(false);
                Assert(r.Status == 204, r.ToString());
                ApiResponse after = await api.SendAsync(HttpMethod.Get, "/v1.0/api/tenants").ConfigureAwait(false);
                Assert(after.Status == 401, "revoked token should be rejected: " + after);
            }).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static async Task RunLockChecksAsync(ApiClient api, string tenantId)
        {
            string locks = "/v1.0/api/tenants/" + tenantId + "/locks/";
            string key = "aot-http-" + Guid.NewGuid().ToString("N").Substring(0, 10);
            string sessionId = "aot-session-" + Guid.NewGuid().ToString("N").Substring(0, 10);
            string holderId = string.Empty;

            await CheckAsync("REST: acquire a write lock over HTTP", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Post, locks + key + "/acquire", ApiClient.Body(w =>
                {
                    w.WriteString("mode", "Write");
                    w.WriteString("sessionId", sessionId);
                })).ConfigureAwait(false);
                Assert(r.Status == 201, r.ToString());
                Assert(r.Json.GetProperty("granted").GetBoolean() && r.String("mode") == "Write" && r.String("sessionId") == sessionId, r.Body);
                Assert(r.Json.GetProperty("fencingToken").GetInt64() >= 1, "fencing token expected: " + r.Body);
                holderId = r.String("holderId") ?? string.Empty;
                Assert(holderId.Length > 0, "holderId expected");
            }).ConfigureAwait(false);

            await CheckAsync("REST: a conflicting fail-fast read is denied", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Post, locks + key + "/acquire", ApiClient.Body(w =>
                {
                    w.WriteString("mode", "Read");
                    w.WriteString("behavior", "FailFast");
                })).ConfigureAwait(false);
                Assert(r.Status == 409, r.ToString());
                Assert(!r.Json.GetProperty("granted").GetBoolean() && r.String("result") == "Denied", r.Body);
            }).ConfigureAwait(false);

            await CheckAsync("REST: read a key's definition and holders", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Get, locks + key).ConfigureAwait(false);
                Assert(r.Status == 200 && r.Json.GetProperty("holders").GetArrayLength() == 1, r.ToString());
                Assert(r.Json.GetProperty("holders")[0].GetProperty("mode").GetString() == "Write", "holder mode should be a string: " + r.Body);
            }).ConfigureAwait(false);

            await CheckAsync("REST: heartbeat renews the session's leases", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Post, "/v1.0/api/tenants/" + tenantId + "/lock-sessions/" + sessionId + "/heartbeat", ApiClient.Body(w =>
                {
                    w.WriteStartArray("holderIds");
                    w.WriteStringValue(holderId);
                    w.WriteEndArray();
                })).ConfigureAwait(false);
                Assert(r.Status == 200 && r.String("sessionId") == sessionId, r.ToString());
                JsonElement renewed = r.Json.GetProperty("renewed");
                Assert(renewed.GetArrayLength() == 1 && renewed[0].GetProperty("holderId").GetString() == holderId, r.Body);
            }).ConfigureAwait(false);

            await CheckAsync("REST: release a holder", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Post, locks + key + "/release", ApiClient.Body(w =>
                {
                    w.WriteString("holderId", holderId);
                    w.WriteString("sessionId", sessionId);
                })).ConfigureAwait(false);
                Assert(r.Status == 200 && r.Json.GetProperty("released").GetBoolean() && r.String("holderId") == holderId, r.ToString());
            }).ConfigureAwait(false);

            await CheckAsync("REST: release every lock held by a session", async () =>
            {
                ApiResponse acquired = await api.SendAsync(HttpMethod.Post, locks + key + "/acquire", ApiClient.Body(w =>
                {
                    w.WriteString("mode", "Read");
                    w.WriteString("sessionId", sessionId);
                })).ConfigureAwait(false);
                Assert(acquired.Status == 201, acquired.ToString());
                ApiResponse r = await api.SendAsync(HttpMethod.Post, "/v1.0/api/tenants/" + tenantId + "/lock-sessions/" + sessionId + "/release").ConfigureAwait(false);
                Assert(r.Status == 200 && r.Json.GetProperty("count").GetInt32() == 1 && r.Json.GetProperty("released").GetArrayLength() == 1, r.ToString());
            }).ConfigureAwait(false);

            await CheckAsync("REST: force-release a key", async () =>
            {
                ApiResponse acquired = await api.SendAsync(HttpMethod.Post, locks + key + "/acquire", ApiClient.Body(w => w.WriteString("mode", "Write"))).ConfigureAwait(false);
                Assert(acquired.Status == 201, acquired.ToString());
                ApiResponse r = await api.SendAsync(HttpMethod.Post, locks + key + "/force-release").ConfigureAwait(false);
                Assert(r.Status == 200 && r.Json.GetProperty("released").GetInt32() == 1 && r.String("key") == key, r.ToString());
            }).ConfigureAwait(false);

            await CheckAsync("REST: lock audit page and chart summary", async () =>
            {
                ApiResponse page = await api.SendAsync(HttpMethod.Get, "/v1.0/api/tenants/" + tenantId + "/lock-audit?maxResults=50").ConfigureAwait(false);
                Assert(page.Status == 200 && page.Json.GetProperty("objects").GetArrayLength() > 0, page.ToString());
                Assert(page.Json.GetProperty("objects")[0].GetProperty("eventType").ValueKind == JsonValueKind.String, "eventType should be a string: " + page.Body);
                ApiResponse summary = await api.SendAsync(HttpMethod.Get, "/v1.0/api/tenants/" + tenantId + "/lock-audit/summary").ConfigureAwait(false);
                Assert(summary.Status == 200 && summary.Json.TryGetProperty("series", out _), summary.ToString());
            }).ConfigureAwait(false);
        }

        private static async Task RunSettingsChecksAsync(ApiClient api, AotServerHost host)
        {
            string settingsBody = string.Empty;

            await CheckAsync("REST: read settings with secrets redacted (admin API key)", async () =>
            {
                using ApiClient admin = new ApiClient(host.Endpoint);
                admin.AdminApiKey = host.Settings.Auth.AdminApiKey;
                ApiResponse r = await admin.SendAsync(HttpMethod.Get, "/v1.0/api/settings").ConfigureAwait(false);
                Assert(r.Status == 200, r.ToString());
                Assert(r.Json.GetProperty("auth").GetProperty("signingKey").GetString() == "***", "signing key should be redacted");
                Assert(r.Json.GetProperty("database").GetProperty("type").GetString() == "Sqlite", "database type should be a string: " + r.Body);
                settingsBody = r.Body;
            }).ConfigureAwait(false);

            await CheckAsync("REST: save settings and persist them to the settings file", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Put, "/v1.0/api/settings", settingsBody).ConfigureAwait(false);
                Assert(r.Status == 200, r.ToString());
                Assert(r.Json.GetProperty("saved").GetBoolean() && r.Json.GetProperty("restartRequired").GetBoolean(), r.Body);
                Assert(r.Json.GetProperty("settings").GetProperty("auth").GetProperty("signingKey").GetString() == "***", "saved settings should be redacted");

                ClutchSettings reloaded = ClutchSettings.FromFile(host.SettingsFile);
                Assert(reloaded.Rest.Port == host.Settings.Rest.Port, "settings file should keep the REST port");
                Assert(reloaded.Auth.SigningKey == host.Settings.Auth.SigningKey, "the redacted placeholder must not overwrite the signing key");
            }).ConfigureAwait(false);

            await CheckAsync("REST: test a database connection", async () =>
            {
                ApiResponse r = await api.SendAsync(HttpMethod.Post, "/v1.0/api/settings/database/test", ApiClient.Body(w =>
                {
                    w.WriteString("type", "Sqlite");
                    w.WriteString("filePath", host.Settings.Database.FilePath);
                })).ConfigureAwait(false);
                Assert(r.Status == 200 && r.Json.GetProperty("ok").GetBoolean() && r.String("provider") == "Sqlite", r.ToString());
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

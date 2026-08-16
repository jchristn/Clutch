namespace Clutch.Server.Services
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enumeration;
    using Clutch.Core.Requests;
    using Clutch.Server.Serialization;
    using Clutch.Server.Settings;
    using SyslogLogging;
    using Voltaic.Mcp;

    /// <summary>
    /// Native Model Context Protocol (MCP) server for Clutch, hosted over streamable HTTP with Server-Sent
    /// Events via the Voltaic library. It exposes read-only observability tools (server info, tenants, active
    /// locks, and lock audit) so AI agents can inspect a Clutch cluster. The MCP server binds its own port,
    /// alongside the REST/WebSocket server.
    /// </summary>
    public class ClutchMcpServer : IDisposable
    {
        #region Private-Members

        private readonly McpSettings _Settings;
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly string _NodeId;
        private readonly string _Product;
        private readonly string _Version;

        private McpHttpServer? _Server;
        private CancellationTokenSource? _Cts;
        private Task? _ServerTask;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">MCP settings.</param>
        /// <param name="database">Database driver used by the read-only tools.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="nodeId">Identifier of this node.</param>
        /// <param name="product">Product name advertised by the server-info tool.</param>
        /// <param name="version">Server version advertised by the server-info tool.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public ClutchMcpServer(McpSettings settings, DatabaseDriverBase database, LoggingModule logging, string nodeId, string product, string version)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            _Product = product ?? "Clutch";
            _Version = version ?? "v1.0";
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the MCP server on its configured host and port. No-op when the transport is disabled.
        /// </summary>
        /// <param name="token">Cancellation token linked to the server lifetime.</param>
        public void Start(CancellationToken token = default)
        {
            if (!_Settings.Enable) return;

            _Cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            // Voltaic.Mcp serves JSON-RPC (POST) at rpcPath and the SSE stream (GET) at eventsPath. Map the
            // configured McpPath onto rpcPath so the streamable-HTTP endpoint honors the setting, and keep the
            // SSE stream on the conventional "/events" path.
            _Server = new McpHttpServer(_Settings.Hostname, _Settings.Port, _Settings.McpPath, "/events", true);
            _Server.ServerName = _Settings.ServerName;
            _Server.ServerVersion = _Version;
            _Server.EnableCors = true;
            _Server.Log += (_, message) => _Logging.Debug("[Clutch.Mcp] " + message);

            RegisterTools(_Server);

            _ServerTask = _Server.StartAsync(_Cts.Token);
            _Logging.Info($"[Clutch.Mcp] MCP server listening on http://{_Settings.Hostname}:{_Settings.Port}{_Settings.McpPath} (streamable HTTP + SSE)");
        }

        /// <summary>
        /// Stop the MCP server.
        /// </summary>
        public void Stop()
        {
            try
            {
                _Cts?.Cancel();
                _Server?.Stop();
            }
            catch (Exception e)
            {
                _Logging.Warn("[Clutch.Mcp] error stopping MCP server: " + e.Message);
            }
        }

        #endregion

        #region Private-Methods

        private void RegisterTools(McpHttpServer server)
        {
            server.RegisterTool(
                "clutch_server_info",
                "Returns Clutch server product, version, and node identifier.",
                new { type = "object", properties = new { } },
                (JsonElement? args) => (object)Json.Serialize(new { product = _Product, version = _Version, nodeId = _NodeId }));

            server.RegisterTool(
                "clutch_list_tenants",
                "Lists tenants (paginated). Optional args: maxResults (1-1000), skip.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        maxResults = new { type = "integer", description = "Page size, 1-1000 (default 25)." },
                        skip = new { type = "integer", description = "Records to skip before the page." }
                    }
                },
                (JsonElement? args) =>
                {
                    EnumerationQuery query = McpToolArguments.BuildQuery(args);
                    EnumerationResult<Core.Models.Tenant> result = _Database.Tenants.EnumerateAsync(query, _Cts!.Token).GetAwaiter().GetResult();
                    return (object)Json.Serialize(result);
                });

            server.RegisterTool(
                "clutch_list_locks",
                "Lists active lock holders in a tenant (paginated). Required arg: tenantId. Optional: name, mode, maxResults, skip.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenantId = new { type = "string", description = "Tenant identifier." },
                        name = new { type = "string", description = "Optional lock-key substring filter." },
                        mode = new { type = "string", description = "Optional mode filter: Read, Write, or Delete." },
                        maxResults = new { type = "integer" },
                        skip = new { type = "integer" }
                    },
                    required = new[] { "tenantId" }
                },
                (JsonElement? args) =>
                {
                    string tenantId = McpToolArguments.GetString(args, "tenantId");
                    if (string.IsNullOrEmpty(tenantId)) throw new ArgumentException("tenantId is required.");
                    string? name = McpToolArguments.GetString(args, "name");
                    Core.Enums.LockModeEnum? mode = null;
                    string modeStr = McpToolArguments.GetString(args, "mode");
                    if (!string.IsNullOrEmpty(modeStr) && Enum.TryParse<Core.Enums.LockModeEnum>(modeStr, true, out Core.Enums.LockModeEnum parsed)) mode = parsed;
                    EnumerationQuery query = McpToolArguments.BuildQuery(args);
                    EnumerationResult<Core.Models.LockHolder> result = _Database.LockHolders
                        .EnumerateByTenantAsync(tenantId, string.IsNullOrEmpty(name) ? null : name, mode, query, _Cts!.Token)
                        .GetAwaiter().GetResult();
                    return (object)Json.Serialize(result);
                });

            server.RegisterTool(
                "clutch_lock_audit",
                "Lists lock audit entries for a tenant (paginated). Required arg: tenantId. Optional: name, mode, maxResults, skip.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        tenantId = new { type = "string", description = "Tenant identifier." },
                        name = new { type = "string", description = "Optional lock-key substring filter." },
                        mode = new { type = "string", description = "Optional mode filter: Read, Write, or Delete." },
                        maxResults = new { type = "integer" },
                        skip = new { type = "integer" }
                    },
                    required = new[] { "tenantId" }
                },
                (JsonElement? args) =>
                {
                    string tenantId = McpToolArguments.GetString(args, "tenantId");
                    if (string.IsNullOrEmpty(tenantId)) throw new ArgumentException("tenantId is required.");
                    LockAuditFilter filter = new LockAuditFilter();
                    filter.TenantId = tenantId;
                    string name = McpToolArguments.GetString(args, "name");
                    if (!string.IsNullOrEmpty(name)) filter.LockKeyContains = name;
                    string modeStr = McpToolArguments.GetString(args, "mode");
                    if (!string.IsNullOrEmpty(modeStr) && Enum.TryParse<Core.Enums.LockModeEnum>(modeStr, true, out Core.Enums.LockModeEnum parsed))
                        filter.Modes = new System.Collections.Generic.List<Core.Enums.LockModeEnum> { parsed };
                    EnumerationQuery page = McpToolArguments.BuildQuery(args);
                    filter.MaxResults = page.MaxResults;
                    filter.Skip = page.Skip;
                    filter.Ordering = page.Ordering;
                    EnumerationResult<Core.Models.LockAuditEntry> result = _Database.LockAudit.EnumerateAsync(filter, _Cts!.Token).GetAwaiter().GetResult();
                    return (object)Json.Serialize(result);
                });
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases the resources used by the MCP server.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            try
            {
                _Server?.Dispose();
                _Cts?.Dispose();
            }
            catch
            {
                // best effort
            }
            _Disposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

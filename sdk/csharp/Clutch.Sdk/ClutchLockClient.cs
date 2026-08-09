namespace Clutch.Sdk
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Client for the Clutch WebSocket lock protocol. Manages a single connection per tenant, acquires and releases
    /// locks, correlates responses by request identifier, and automatically sends heartbeats to keep held leases alive.
    /// </summary>
    /// <remarks>
    /// All locks a connection holds are released automatically by the server when the connection closes. This type is
    /// safe to use concurrently: sends are serialized internally and multiple acquire requests may be outstanding at once.
    /// Call <see cref="ConnectAsync(CancellationToken)"/> before acquiring locks. Implements <see cref="IDisposable"/>.
    /// </remarks>
    public class ClutchLockClient : IDisposable
    {
        private readonly string _WebSocketUrl;
        private readonly string _AccessKey;
        private readonly string? _SecretKey;
        private readonly ClientWebSocket _Socket;
        private readonly JsonSerializerOptions _JsonOptions;
        private readonly SemaphoreSlim _SendLock;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _Pending;
        private readonly ConcurrentDictionary<string, AcquiredLock> _Held;
        private readonly TaskCompletionSource<WelcomeInfo> _WelcomeSource;
        private CancellationTokenSource? _Cts;
        private Task? _ReceiveLoop;
        private Task? _HeartbeatLoop;
        private WelcomeInfo? _Welcome;
        private int _ReceiveBufferSize = 16384;
        private bool _Disposed;

        /// <summary>
        /// The welcome information received from the server, or null until connected.
        /// </summary>
        public WelcomeInfo? Welcome
        {
            get
            {
                return _Welcome;
            }
        }

        /// <summary>
        /// The size, in bytes, of the buffer used to receive WebSocket frames. Default is 16384. Minimum is 1024.
        /// </summary>
        public int ReceiveBufferSize
        {
            get
            {
                return _ReceiveBufferSize;
            }
            set
            {
                if (value < 1024) throw new ArgumentOutOfRangeException(nameof(value), "ReceiveBufferSize must be at least 1024.");
                _ReceiveBufferSize = value;
            }
        }

        /// <summary>
        /// Raised when the server sends a heartbeat frame renewing one or more held leases.
        /// </summary>
        public event EventHandler<IReadOnlyList<AcquiredLock>>? HeartbeatReceived;

        /// <summary>
        /// Raised when the server sends an unsolicited error frame (one not correlated to a pending request).
        /// </summary>
        public event EventHandler<string>? ErrorReceived;

        /// <summary>
        /// Raised when the connection closes, whether cleanly or because of a fault.
        /// </summary>
        public event EventHandler<string>? Closed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClutchLockClient"/> class.
        /// </summary>
        /// <param name="endpoint">The base URL of the Clutch server, for example "http://127.0.0.1:8090". The scheme is converted to ws or wss automatically.</param>
        /// <param name="accessKey">The application access key.</param>
        /// <param name="secretKey">The optional secret key. When supplied it is verified in constant time by the server.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpoint"/> or <paramref name="accessKey"/> is null.</exception>
        public ClutchLockClient(string endpoint, string accessKey, string? secretKey = null)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (accessKey == null) throw new ArgumentNullException(nameof(accessKey));

            _AccessKey = accessKey;
            _SecretKey = secretKey;
            _WebSocketUrl = BuildWebSocketUrl(endpoint);
            _Socket = new ClientWebSocket();
            _SendLock = new SemaphoreSlim(1, 1);
            _Pending = new ConcurrentDictionary<string, TaskCompletionSource<JsonElement>>();
            _Held = new ConcurrentDictionary<string, AcquiredLock>();
            _WelcomeSource = new TaskCompletionSource<WelcomeInfo>(TaskCreationOptions.RunContinuationsAsynchronously);

            _JsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Opens the WebSocket connection, waits for the welcome frame, and starts the receive and heartbeat loops.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The welcome information sent by the server.</returns>
        /// <exception cref="ClutchException">Thrown when the connection cannot be established.</exception>
        public async Task<WelcomeInfo> ConnectAsync(CancellationToken cancellationToken = default)
        {
            _Socket.Options.SetRequestHeader("x-clutch-access-key", _AccessKey);
            if (!string.IsNullOrEmpty(_SecretKey))
            {
                _Socket.Options.SetRequestHeader("x-clutch-secret-key", _SecretKey);
            }

            try
            {
                await _Socket.ConnectAsync(new Uri(_WebSocketUrl), cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException ex)
            {
                throw new ClutchException($"Failed to connect to {_WebSocketUrl}: {ex.Message}", ex);
            }

            _Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ReceiveLoop = Task.Run(() => ReceiveLoopAsync(_Cts.Token));

            using (CancellationTokenRegistration registration = cancellationToken.Register(() => _WelcomeSource.TrySetCanceled(cancellationToken)))
            {
                _Welcome = await _WelcomeSource.Task.ConfigureAwait(false);
            }

            _HeartbeatLoop = Task.Run(() => HeartbeatLoopAsync(_Cts.Token));
            return _Welcome;
        }

        /// <summary>
        /// Acquires a lock on a key.
        /// </summary>
        /// <param name="key">The key to lock.</param>
        /// <param name="mode">The mode in which to acquire the lock.</param>
        /// <param name="options">Optional acquisition options controlling behavior, timeout, lease, and policy.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The acquired lock, including its holder identifier and fencing token.</returns>
        /// <exception cref="LockDeniedException">Thrown when the acquisition is denied, times out, or conflicts with an existing policy.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails at the protocol level.</exception>
        public async Task<AcquiredLock> AcquireAsync(string key, LockMode mode, AcquireOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            EnsureConnected();

            options ??= new AcquireOptions();
            string requestId = Guid.NewGuid().ToString("N");

            Dictionary<string, object?> frame = new Dictionary<string, object?>
            {
                ["type"] = "acquire",
                ["requestId"] = requestId,
                ["key"] = key,
                ["mode"] = mode.ToString(),
                ["behavior"] = options.Behavior.ToString()
            };
            if (options.TimeoutMs.HasValue) frame["timeoutMs"] = options.TimeoutMs.Value;
            if (options.LeaseMs.HasValue) frame["leaseMs"] = options.LeaseMs.Value;
            if (options.Policy != null) frame["policy"] = options.Policy;

            JsonElement response = await SendAndAwaitAsync(requestId, frame, cancellationToken).ConfigureAwait(false);
            string type = GetString(response, "type") ?? string.Empty;

            if (type == "acquired")
            {
                AcquiredLock acquired = new AcquiredLock
                {
                    Key = GetString(response, "key") ?? key,
                    Mode = ParseMode(GetString(response, "mode"), mode),
                    HolderId = GetString(response, "holderId") ?? string.Empty,
                    FencingToken = GetInt64(response, "fencingToken"),
                    LeaseExpiresUtc = GetDateTime(response, "leaseExpiresUtc")
                };
                _Held[acquired.HolderId] = acquired;
                return acquired;
            }

            if (type == "denied")
            {
                AcquireResult result = ParseResult(GetString(response, "result"));
                string reason = GetString(response, "reason") ?? "The lock request was denied.";
                throw new LockDeniedException(result, key, reason);
            }

            if (type == "error")
            {
                throw new ClutchException($"The server rejected the acquire request for '{key}': {GetString(response, "message")}");
            }

            throw new ClutchException($"Unexpected response type '{type}' to the acquire request for '{key}'.");
        }

        /// <summary>
        /// Releases a held lock. Releasing is idempotent.
        /// </summary>
        /// <param name="holderId">The holder identifier returned when the lock was acquired.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>True when a holder was released; false when the holder was already gone.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="holderId"/> is null.</exception>
        /// <exception cref="ClutchException">Thrown when the request fails at the protocol level.</exception>
        public async Task<bool> ReleaseAsync(string holderId, CancellationToken cancellationToken = default)
        {
            if (holderId == null) throw new ArgumentNullException(nameof(holderId));
            EnsureConnected();

            _Held.TryGetValue(holderId, out AcquiredLock? held);
            string key = held?.Key ?? string.Empty;
            string requestId = Guid.NewGuid().ToString("N");

            Dictionary<string, object?> frame = new Dictionary<string, object?>
            {
                ["type"] = "release",
                ["requestId"] = requestId,
                ["key"] = key,
                ["holderId"] = holderId
            };

            JsonElement response = await SendAndAwaitAsync(requestId, frame, cancellationToken).ConfigureAwait(false);
            _Held.TryRemove(holderId, out _);

            string type = GetString(response, "type") ?? string.Empty;
            if (type == "error")
            {
                throw new ClutchException($"The server rejected the release request for holder '{holderId}': {GetString(response, "message")}");
            }

            return GetBool(response, "released");
        }

        /// <summary>
        /// Sends a heartbeat for the supplied holders, or for all currently held locks when none are specified.
        /// The heartbeat loop calls this automatically; call it directly only for manual control.
        /// </summary>
        /// <param name="holderIds">The holders to renew, or null to renew every held lock.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <exception cref="ClutchException">Thrown when the request fails at the protocol level.</exception>
        public async Task HeartbeatAsync(IEnumerable<string>? holderIds = null, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            List<string> ids = holderIds != null ? holderIds.ToList() : _Held.Keys.ToList();
            if (ids.Count == 0) return;

            Dictionary<string, object?> frame = new Dictionary<string, object?>
            {
                ["type"] = "heartbeat",
                ["holderIds"] = ids
            };
            await SendFrameAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Closes the WebSocket connection, releasing every lock held by this connection on the server side.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (_Cts != null && !_Cts.IsCancellationRequested)
            {
                _Cts.Cancel();
            }

            try
            {
                if (_Socket.State == WebSocketState.Open)
                {
                    await _Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client closing", cancellationToken).ConfigureAwait(false);
                }
            }
            catch (WebSocketException)
            {
                // The connection may already be faulted; nothing further to do.
            }
            catch (OperationCanceledException)
            {
                // Closing was cancelled; nothing further to do.
            }
        }

        #region Private-Methods

        private void EnsureConnected()
        {
            if (_Socket.State != WebSocketState.Open)
            {
                throw new ClutchException($"The lock client is not connected (socket state: {_Socket.State}). Call ConnectAsync first.");
            }
        }

        private async Task<JsonElement> SendAndAwaitAsync(string requestId, Dictionary<string, object?> frame, CancellationToken cancellationToken)
        {
            TaskCompletionSource<JsonElement> tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _Pending[requestId] = tcs;

            try
            {
                await SendFrameAsync(frame, cancellationToken).ConfigureAwait(false);

                using (CancellationTokenRegistration registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                _Pending.TryRemove(requestId, out _);
            }
        }

        private async Task SendFrameAsync(Dictionary<string, object?> frame, CancellationToken cancellationToken)
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(frame, _JsonOptions);

            await _SendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _Socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException ex)
            {
                throw new ClutchException($"Failed to send a frame: {ex.Message}", ex);
            }
            finally
            {
                _SendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[_ReceiveBufferSize];
            string closeReason = "closed";

            try
            {
                while (!cancellationToken.IsCancellationRequested && _Socket.State == WebSocketState.Open)
                {
                    using System.IO.MemoryStream ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _Socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            closeReason = result.CloseStatusDescription ?? "closed by server";
                            FailAllPending(new ClutchException($"The connection was closed by the server: {closeReason}"));
                            return;
                        }

                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    string text = Encoding.UTF8.GetString(ms.ToArray());
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    DispatchFrame(text);
                }
            }
            catch (OperationCanceledException)
            {
                closeReason = "cancelled";
            }
            catch (WebSocketException ex)
            {
                closeReason = ex.Message;
                FailAllPending(new ClutchException($"The connection faulted: {ex.Message}", ex));
            }
            finally
            {
                _WelcomeSource.TrySetException(new ClutchException($"The connection closed before a welcome frame was received: {closeReason}"));
                Closed?.Invoke(this, closeReason);
            }
        }

        private void DispatchFrame(string text)
        {
            JsonElement root;
            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                root = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                return;
            }

            string type = GetString(root, "type") ?? string.Empty;

            switch (type)
            {
                case "welcome":
                    WelcomeInfo welcome = new WelcomeInfo
                    {
                        SessionId = GetString(root, "sessionId"),
                        TenantId = GetString(root, "tenantId"),
                        DefaultLeaseMs = (int)GetInt64(root, "defaultLeaseMs"),
                        HeartbeatIntervalMs = (int)GetInt64(root, "heartbeatIntervalMs")
                    };
                    _Welcome = welcome;
                    _WelcomeSource.TrySetResult(welcome);
                    break;

                case "acquired":
                case "denied":
                case "released":
                    CompletePending(root);
                    break;

                case "heartbeat":
                    HandleHeartbeat(root);
                    break;

                case "error":
                    string? requestId = GetString(root, "requestId");
                    if (!string.IsNullOrEmpty(requestId) && _Pending.ContainsKey(requestId))
                    {
                        CompletePending(root);
                    }
                    else
                    {
                        ErrorReceived?.Invoke(this, GetString(root, "message") ?? "unknown error");
                    }
                    break;

                case "pong":
                default:
                    break;
            }
        }

        private void CompletePending(JsonElement root)
        {
            string? requestId = GetString(root, "requestId");
            if (string.IsNullOrEmpty(requestId)) return;
            if (_Pending.TryGetValue(requestId, out TaskCompletionSource<JsonElement>? tcs))
            {
                tcs.TrySetResult(root);
            }
        }

        private void HandleHeartbeat(JsonElement root)
        {
            List<AcquiredLock> renewed = new List<AcquiredLock>();
            if (root.TryGetProperty("renewed", out JsonElement renewedElement) && renewedElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement entry in renewedElement.EnumerateArray())
                {
                    string? holderId = GetString(entry, "holderId");
                    if (string.IsNullOrEmpty(holderId)) continue;
                    DateTime leaseExpires = GetDateTime(entry, "leaseExpiresUtc");
                    if (_Held.TryGetValue(holderId, out AcquiredLock? held))
                    {
                        held.LeaseExpiresUtc = leaseExpires;
                        renewed.Add(held);
                    }
                }
            }

            if (renewed.Count > 0)
            {
                HeartbeatReceived?.Invoke(this, renewed);
            }
        }

        private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
        {
            int interval = _Welcome?.HeartbeatIntervalMs ?? 10000;
            if (interval < 1000) interval = 1000;

            try
            {
                while (!cancellationToken.IsCancellationRequested && _Socket.State == WebSocketState.Open)
                {
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested) break;
                    if (_Held.IsEmpty) continue;

                    try
                    {
                        await HeartbeatAsync(null, cancellationToken).ConfigureAwait(false);
                    }
                    catch (ClutchException)
                    {
                        // A transient send failure will be surfaced by the receive loop if the connection is gone.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }

        private void FailAllPending(Exception exception)
        {
            foreach (KeyValuePair<string, TaskCompletionSource<JsonElement>> pair in _Pending)
            {
                pair.Value.TrySetException(exception);
            }
            _Pending.Clear();
        }

        private static string BuildWebSocketUrl(string endpoint)
        {
            string trimmed = endpoint.TrimEnd('/');
            if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "wss://" + trimmed.Substring("https://".Length);
            }
            else if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = "ws://" + trimmed.Substring("http://".Length);
            }
            return trimmed + "/v1.0/lock/connect";
        }

        private static string? GetString(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
            return null;
        }

        private static long GetInt64(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number)
            {
                return value.GetInt64();
            }
            return 0;
        }

        private static bool GetBool(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.True) return true;
                if (value.ValueKind == JsonValueKind.False) return false;
            }
            return false;
        }

        private static DateTime GetDateTime(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTime(out DateTime parsed))
            {
                return parsed.ToUniversalTime();
            }
            return DateTime.MinValue;
        }

        private static LockMode ParseMode(string? value, LockMode fallback)
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out LockMode parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static AcquireResult ParseResult(string? value)
        {
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out AcquireResult parsed))
            {
                return parsed;
            }
            return AcquireResult.Denied;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Closes the connection and releases the resources used by the client.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Closes the connection and releases the resources used by the client.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>; false when called from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            if (disposing)
            {
                try
                {
                    if (_Cts != null && !_Cts.IsCancellationRequested) _Cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed.
                }

                FailAllPending(new ClutchException("The lock client was disposed."));

                try
                {
                    _Socket.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed.
                }

                _SendLock.Dispose();
                _Cts?.Dispose();
            }
            _Disposed = true;
        }

        #endregion
    }
}

namespace Clutch.Sdk.Console
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Clutch.Sdk;
    using SysConsole = System.Console;

    /// <summary>
    /// Interactive console application for driving the Clutch SDK by hand: authenticate, inspect tenants and locks,
    /// connect the lock client, and acquire, release, and heartbeat locks while watching server pushes.
    /// </summary>
    internal static class Program
    {
        private static ClutchAdminClient? _Admin;
        private static ClutchLockClient? _Locks;
        private static string? _TenantId;
        private static readonly Dictionary<int, AcquiredLock> _Holders = new Dictionary<int, AcquiredLock>();
        private static int _HolderCounter;

        private static async Task Main(string[] args)
        {
            string endpoint = args.Length >= 1 ? args[0] : Prompt("Endpoint [http://127.0.0.1:8090]: ", "http://127.0.0.1:8090");
            _Admin = new ClutchAdminClient(endpoint);

            SysConsole.WriteLine();
            SysConsole.WriteLine("Clutch interactive console. Type 'help' for commands, 'quit' to exit.");
            SysConsole.WriteLine($"Endpoint: {endpoint}");
            SysConsole.WriteLine();

            bool running = true;
            while (running)
            {
                SysConsole.Write("clutch> ");
                string? line = SysConsole.ReadLine();
                if (line == null) break;
                line = line.Trim();
                if (line.Length == 0) continue;

                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string command = parts[0].ToLowerInvariant();

                try
                {
                    switch (command)
                    {
                        case "help": PrintHelp(); break;
                        case "login": await LoginAsync(parts).ConfigureAwait(false); break;
                        case "whoami": await WhoAmIAsync().ConfigureAwait(false); break;
                        case "serverinfo": await ServerInfoAsync().ConfigureAwait(false); break;
                        case "tenants": await TenantsAsync().ConfigureAwait(false); break;
                        case "locks": await LocksAsync().ConfigureAwait(false); break;
                        case "connect": await ConnectAsync(endpoint, parts).ConfigureAwait(false); break;
                        case "acquire": await AcquireAsync(parts).ConfigureAwait(false); break;
                        case "release": await ReleaseAsync(parts).ConfigureAwait(false); break;
                        case "held": Held(); break;
                        case "heartbeat": await HeartbeatAsync().ConfigureAwait(false); break;
                        case "logout": await LogoutAsync().ConfigureAwait(false); break;
                        case "quit":
                        case "exit": running = false; break;
                        default:
                            SysConsole.WriteLine($"Unknown command '{command}'. Type 'help'.");
                            break;
                    }
                }
                catch (LockDeniedException ex)
                {
                    SysConsole.WriteLine($"Lock denied ({ex.Result}): {ex.Message}");
                }
                catch (ClutchException ex)
                {
                    SysConsole.WriteLine($"Error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    SysConsole.WriteLine($"Unexpected: {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (_Locks != null)
            {
                await _Locks.CloseAsync().ConfigureAwait(false);
                _Locks.Dispose();
            }
            _Admin?.Dispose();
            SysConsole.WriteLine("Goodbye.");
        }

        private static void PrintHelp()
        {
            SysConsole.WriteLine("Commands:");
            SysConsole.WriteLine("  login key <accessKey>                 Authenticate with an application key");
            SysConsole.WriteLine("  login user <tenantId> <email> <pw>    Authenticate with user credentials");
            SysConsole.WriteLine("  whoami                                Show the current principal");
            SysConsole.WriteLine("  serverinfo                            Show server information");
            SysConsole.WriteLine("  tenants                               List tenants");
            SysConsole.WriteLine("  locks                                 List active locks in the current tenant");
            SysConsole.WriteLine("  connect <accessKey>                   Open the WebSocket lock connection");
            SysConsole.WriteLine("  acquire <key> <Read|Write|Delete> [wait <ms>]   Acquire a lock");
            SysConsole.WriteLine("  held                                  List locks held on this connection");
            SysConsole.WriteLine("  release <n>                           Release a held lock by its list number");
            SysConsole.WriteLine("  heartbeat                             Send a heartbeat for all held locks");
            SysConsole.WriteLine("  logout                                Revoke the current session");
            SysConsole.WriteLine("  quit                                  Exit");
        }

        private static async Task LoginAsync(string[] parts)
        {
            if (_Admin == null) return;
            if (parts.Length >= 3 && parts[1].ToLowerInvariant() == "key")
            {
                TokenResponse token = await _Admin.AuthenticateWithKeyAsync(parts[2]).ConfigureAwait(false);
                _TenantId = token.TenantId;
                SysConsole.WriteLine($"Authenticated. Tenant: {token.TenantId}");
            }
            else if (parts.Length >= 5 && parts[1].ToLowerInvariant() == "user")
            {
                TokenResponse token = await _Admin.AuthenticateWithPasswordAsync(parts[2], parts[3], parts[4]).ConfigureAwait(false);
                _TenantId = token.TenantId;
                SysConsole.WriteLine($"Authenticated. Tenant: {token.TenantId}");
            }
            else
            {
                SysConsole.WriteLine("Usage: login key <accessKey>  |  login user <tenantId> <email> <password>");
            }
        }

        private static async Task WhoAmIAsync()
        {
            if (_Admin == null) return;
            TokenDetails details = await _Admin.GetTokenDetailsAsync().ConfigureAwait(false);
            SysConsole.WriteLine($"Principal : {details.PrincipalName}");
            SysConsole.WriteLine($"Type      : {details.PrincipalType}");
            SysConsole.WriteLine($"Tenant    : {details.TenantId}");
            SysConsole.WriteLine($"IsAdmin   : {details.IsAdmin}");
            SysConsole.WriteLine($"TenantAdmin: {details.IsTenantAdmin}");
        }

        private static async Task ServerInfoAsync()
        {
            if (_Admin == null) return;
            ServerInfo info = await _Admin.GetServerInfoAsync().ConfigureAwait(false);
            SysConsole.WriteLine($"Product : {info.Product} {info.Version}");
            SysConsole.WriteLine($"Node    : {info.Node}");
            SysConsole.WriteLine($"Database: {info.Database}");
            SysConsole.WriteLine($"WS conns: {info.WebSocketConnections}");
        }

        private static async Task TenantsAsync()
        {
            if (_Admin == null) return;
            List<Tenant> tenants = await _Admin.ListTenantsAsync().ConfigureAwait(false);
            SysConsole.WriteLine($"{tenants.Count} tenant(s):");
            foreach (Tenant t in tenants)
            {
                SysConsole.WriteLine($"  {t.Id}  {t.Name}  (active={t.Active}, protected={t.IsProtected})");
            }
        }

        private static async Task LocksAsync()
        {
            if (_Admin == null) return;
            if (string.IsNullOrEmpty(_TenantId))
            {
                SysConsole.WriteLine("Not authenticated. Use 'login' first.");
                return;
            }
            List<LockHolder> holders = await _Admin.ListLocksAsync(_TenantId!).ConfigureAwait(false);
            SysConsole.WriteLine($"{holders.Count} active holder(s):");
            foreach (LockHolder h in holders)
            {
                SysConsole.WriteLine($"  {h.LockKey}  {h.Mode}  fencing={h.FencingToken}  holder={h.Id}");
            }
        }

        private static async Task ConnectAsync(string endpoint, string[] parts)
        {
            if (parts.Length < 2)
            {
                SysConsole.WriteLine("Usage: connect <accessKey>");
                return;
            }
            string accessKey = parts[1];

            if (_Locks != null)
            {
                await _Locks.CloseAsync().ConfigureAwait(false);
                _Locks.Dispose();
            }

            _Locks = new ClutchLockClient(endpoint, accessKey);
            _Locks.HeartbeatReceived += (sender, renewed) =>
            {
                SysConsole.WriteLine($"[push] heartbeat renewed {renewed.Count} lease(s)");
            };
            _Locks.ErrorReceived += (sender, message) =>
            {
                SysConsole.WriteLine($"[push] error: {message}");
            };
            _Locks.Closed += (sender, reason) =>
            {
                SysConsole.WriteLine($"[push] connection closed: {reason}");
            };

            WelcomeInfo welcome = await _Locks.ConnectAsync().ConfigureAwait(false);
            SysConsole.WriteLine($"Connected. Session {welcome.SessionId}, heartbeat every {welcome.HeartbeatIntervalMs} ms.");
        }

        private static async Task AcquireAsync(string[] parts)
        {
            if (_Locks == null)
            {
                SysConsole.WriteLine("Not connected. Use 'connect' first.");
                return;
            }
            if (parts.Length < 3)
            {
                SysConsole.WriteLine("Usage: acquire <key> <Read|Write|Delete> [wait <ms>]");
                return;
            }
            string key = parts[1];
            if (!Enum.TryParse(parts[2], true, out LockMode mode))
            {
                SysConsole.WriteLine("Mode must be Read, Write, or Delete.");
                return;
            }

            AcquireOptions options = new AcquireOptions();
            if (parts.Length >= 5 && parts[3].ToLowerInvariant() == "wait" && int.TryParse(parts[4], out int ms))
            {
                options.Behavior = LockBehavior.Wait;
                options.TimeoutMs = ms;
            }

            AcquiredLock acquired = await _Locks.AcquireAsync(key, mode, options).ConfigureAwait(false);
            int number = ++_HolderCounter;
            _Holders[number] = acquired;
            SysConsole.WriteLine($"Acquired [{number}] key={acquired.Key} mode={acquired.Mode} fencing={acquired.FencingToken} lease={acquired.LeaseExpiresUtc:o}");
        }

        private static async Task ReleaseAsync(string[] parts)
        {
            if (_Locks == null)
            {
                SysConsole.WriteLine("Not connected. Use 'connect' first.");
                return;
            }
            if (parts.Length < 2 || !int.TryParse(parts[1], out int number) || !_Holders.TryGetValue(number, out AcquiredLock? held))
            {
                SysConsole.WriteLine("Usage: release <n>  (see 'held' for numbers)");
                return;
            }
            bool released = await _Locks.ReleaseAsync(held.HolderId).ConfigureAwait(false);
            _Holders.Remove(number);
            SysConsole.WriteLine($"Released [{number}]: {released}");
        }

        private static void Held()
        {
            if (_Holders.Count == 0)
            {
                SysConsole.WriteLine("No locks held on this connection.");
                return;
            }
            foreach (KeyValuePair<int, AcquiredLock> pair in _Holders)
            {
                SysConsole.WriteLine($"  [{pair.Key}] key={pair.Value.Key} mode={pair.Value.Mode} fencing={pair.Value.FencingToken} holder={pair.Value.HolderId}");
            }
        }

        private static async Task HeartbeatAsync()
        {
            if (_Locks == null)
            {
                SysConsole.WriteLine("Not connected. Use 'connect' first.");
                return;
            }
            await _Locks.HeartbeatAsync().ConfigureAwait(false);
            SysConsole.WriteLine("Heartbeat sent for all held locks.");
        }

        private static async Task LogoutAsync()
        {
            if (_Admin == null) return;
            await _Admin.LogoutAsync().ConfigureAwait(false);
            _TenantId = null;
            SysConsole.WriteLine("Logged out.");
        }

        private static string Prompt(string message, string fallback)
        {
            SysConsole.Write(message);
            string? value = SysConsole.ReadLine();
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}

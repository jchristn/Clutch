"""Clutch Python SDK interactive console.

A REPL for driving the SDK by hand: authenticate, inspect tenants and locks, connect the lock
client, and acquire, release, and heartbeat locks while watching server pushes.

Usage: python console.py [endpoint]
"""

import sys

from clutch_admin_sdk import ClutchAdminClient, ClutchError
from clutch_lock_sdk import ClutchLockClient, ClutchLockDeniedError


def print_help():
    print("Commands:")
    print("  login key <accessKey> [secretKey]     Authenticate with an application key")
    print("  login user <tenantId> <email> <pw>    Authenticate with user credentials")
    print("  whoami                                Show the current principal")
    print("  serverinfo                            Show server information")
    print("  tenants                               List tenants")
    print("  locks                                 List active locks in the current tenant")
    print("  connect <accessKey> [secretKey]       Open the WebSocket lock connection")
    print("  acquire <key> <Read|Write|Delete> [wait <ms>]   Acquire a lock")
    print("  held                                  List locks held on this connection")
    print("  release <n>                           Release a held lock by its list number")
    print("  heartbeat                             Send a heartbeat for all held locks")
    print("  logout                                Revoke the current session")
    print("  quit                                  Exit")


def main():
    endpoint = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8090"
    admin = ClutchAdminClient(endpoint)
    state = {"tenant_id": None, "locks": None, "counter": 0}
    held = {}

    print("")
    print('Clutch interactive console. Type "help" for commands, "quit" to exit.')
    print(f"Endpoint: {endpoint}")
    print("")

    while True:
        try:
            line = input("clutch> ").strip()
        except EOFError:
            break
        if not line:
            continue
        parts = line.split()
        command = parts[0].lower()

        try:
            if command == "help":
                print_help()
            elif command == "login":
                if len(parts) >= 3 and parts[1].lower() == "key":
                    token = admin.authenticate_with_key(parts[2], parts[3] if len(parts) >= 4 else parts[2])
                    state["tenant_id"] = token.get("tenantId")
                    print(f"Authenticated. Tenant: {token.get('tenantId')}")
                elif len(parts) >= 5 and parts[1].lower() == "user":
                    token = admin.authenticate_with_password(parts[2], parts[3], parts[4])
                    state["tenant_id"] = token.get("tenantId")
                    print(f"Authenticated. Tenant: {token.get('tenantId')}")
                else:
                    print("Usage: login key <accessKey> [secretKey]  |  login user <tenantId> <email> <password>")
            elif command == "whoami":
                d = admin.get_token_details()
                print(f"Principal : {d.get('principalName')}")
                print(f"Type      : {d.get('principalType')}")
                print(f"Tenant    : {d.get('tenantId')}")
                print(f"IsAdmin   : {d.get('isAdmin')}")
                print(f"TenantAdmin: {d.get('isTenantAdmin')}")
            elif command == "serverinfo":
                info = admin.get_server_info()
                print(f"Product : {info.get('product')} {info.get('version')}")
                print(f"Node    : {info.get('node')}")
                print(f"Database: {info.get('database')}")
                print(f"WS conns: {info.get('webSocketConnections')}")
            elif command == "tenants":
                tenants = admin.list_tenants()
                print(f"{len(tenants)} tenant(s):")
                for t in tenants:
                    print(f"  {t.get('id')}  {t.get('name')}  (active={t.get('active')}, protected={t.get('isProtected')})")
            elif command == "locks":
                if not state["tenant_id"]:
                    print('Not authenticated. Use "login" first.')
                else:
                    holders = admin.list_locks(state["tenant_id"])
                    print(f"{len(holders)} active holder(s):")
                    for h in holders:
                        print(f"  {h.get('lockKey')}  {h.get('mode')}  fencing={h.get('fencingToken')}  holder={h.get('id')}")
            elif command == "connect":
                if len(parts) < 2:
                    print("Usage: connect <accessKey> [secretKey]")
                else:
                    if state["locks"] is not None:
                        state["locks"].close()
                    client = ClutchLockClient(endpoint, parts[1], parts[2] if len(parts) >= 3 else None)
                    client.on_heartbeat = lambda renewed: print(f"\n[push] heartbeat renewed {len(renewed)} lease(s)")
                    client.on_error = lambda message: print(f"\n[push] error: {message}")
                    client.on_close = lambda reason: print(f"\n[push] connection closed: {reason}")
                    welcome = client.connect()
                    state["locks"] = client
                    print(f"Connected. Session {welcome.get('sessionId')}, heartbeat every {welcome.get('heartbeatIntervalMs')} ms.")
            elif command == "acquire":
                if state["locks"] is None:
                    print('Not connected. Use "connect" first.')
                elif len(parts) < 3:
                    print("Usage: acquire <key> <Read|Write|Delete> [wait <ms>]")
                else:
                    kwargs = {}
                    if len(parts) >= 5 and parts[3].lower() == "wait":
                        kwargs = {"behavior": "Wait", "timeout_ms": int(parts[4])}
                    acquired = state["locks"].acquire(parts[1], parts[2], **kwargs)
                    state["counter"] += 1
                    n = state["counter"]
                    held[n] = acquired
                    print(f"Acquired [{n}] key={acquired.get('key')} mode={acquired.get('mode')} "
                          f"fencing={acquired.get('fencingToken')} lease={acquired.get('leaseExpiresUtc')}")
            elif command == "held":
                if not held:
                    print("No locks held on this connection.")
                else:
                    for n, h in held.items():
                        print(f"  [{n}] key={h.get('key')} mode={h.get('mode')} fencing={h.get('fencingToken')} holder={h.get('holderId')}")
            elif command == "release":
                if state["locks"] is None:
                    print('Not connected. Use "connect" first.')
                elif len(parts) < 2 or not parts[1].isdigit() or int(parts[1]) not in held:
                    print('Usage: release <n>  (see "held" for numbers)')
                else:
                    n = int(parts[1])
                    released = state["locks"].release(held[n]["holderId"])
                    del held[n]
                    print(f"Released [{n}]: {released}")
            elif command == "heartbeat":
                if state["locks"] is None:
                    print('Not connected. Use "connect" first.')
                else:
                    state["locks"].heartbeat()
                    print("Heartbeat sent for all held locks.")
            elif command == "logout":
                admin.logout()
                state["tenant_id"] = None
                print("Logged out.")
            elif command in ("quit", "exit"):
                break
            else:
                print(f"Unknown command '{command}'. Type 'help'.")
        except ClutchLockDeniedError as ex:
            print(f"Lock denied ({ex.result}): {ex}")
        except ClutchError as ex:
            print(f"Error: {ex}")
        except Exception as ex:  # noqa: BLE001
            print(f"Unexpected: {type(ex).__name__}: {ex}")

    if state["locks"] is not None:
        state["locks"].close()
    admin.close()
    print("Goodbye.")


if __name__ == "__main__":
    main()

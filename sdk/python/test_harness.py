"""Clutch Python SDK test harness.

Non-interactive: exercises the admin (REST) and lock (WebSocket) clients, asserts expected
behavior, and exits with code 0 on success or 1 on failure.

Usage: python test_harness.py <endpoint> <accessKey>
"""

import sys
import threading
import time
import uuid

from clutch_admin_sdk import ClutchAdminClient, LockBehavior, LockMode
from clutch_lock_sdk import AcquireResult, ClutchLockClient, ClutchLockDeniedError

_passed = 0
_failed = 0


def check(name, fn):
    global _passed, _failed
    try:
        fn()
        _passed += 1
        print(f"[PASS] {name}")
    except Exception as ex:  # noqa: BLE001 - report any failure
        _failed += 1
        print(f"[FAIL] {name}")
        print(f"       {type(ex).__name__}: {ex}")


def assert_true(condition, message):
    if not condition:
        raise AssertionError(message)


def assert_raises(error_type, fn):
    try:
        fn()
    except error_type as ex:
        return ex
    except Exception as ex:  # noqa: BLE001
        raise AssertionError(f"Expected {error_type.__name__} but caught {type(ex).__name__}: {ex}")
    raise AssertionError(f"Expected {error_type.__name__} but no error was raised.")


def unique_key(prefix):
    return f"{prefix}/{uuid.uuid4().hex}"


def main():
    endpoint = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8090"
    access_key = sys.argv[2] if len(sys.argv) > 2 else "clutch-default-access-key"

    print("========================================")
    print("  Clutch Python SDK Test Harness")
    print("========================================")
    print(f"Endpoint : {endpoint}")
    print(f"AccessKey: {access_key}")
    print("")

    admin = ClutchAdminClient(endpoint)
    state = {"tenant_id": None, "first_token": 0}

    def health():
        h = admin.get_health()
        assert_true(bool(h.get("status")), "status should be present")

    def authenticate():
        token = admin.authenticate_with_key(access_key)
        assert_true(bool(token.get("token")), "token should be returned")
        assert_true(bool(token.get("tenantId")), "tenantId should be returned")
        state["tenant_id"] = token.get("tenantId")

    def token_details():
        d = admin.get_token_details()
        assert_true(d.get("authenticated") is True, "principal should be authenticated")
        assert_true(bool(d.get("tenantId")), "tenantId should be present")

    def server_info():
        info = admin.get_server_info()
        assert_true((info.get("product") or "").lower() == "clutch", f"product should be Clutch, was '{info.get('product')}'")

    def list_tenants():
        tenants = admin.list_tenants()
        assert_true(isinstance(tenants, list) and len(tenants) >= 1, "expected at least one tenant")

    def tenant_crud():
        name = "sdk-test-" + uuid.uuid4().hex[:12]
        created = admin.create_tenant(name, lock_history_retention_days=7, default_lease_ms=30000, max_lease_ms=300000)
        assert_true(bool(created.get("id")), "created tenant should have an id")
        fetched = admin.get_tenant(created["id"])
        assert_true(fetched.get("name") == name, "fetched tenant name should match")
        admin.delete_tenant(created["id"])

    def credential_crud():
        created = admin.create_credential(state["tenant_id"], "sdk-test-cred-" + uuid.uuid4().hex[:8])
        assert_true(bool(created.get("id")), "created credential should have an id")
        assert_true(bool(created.get("accessKey")), "created credential should return an access key")
        credentials = admin.list_credentials(state["tenant_id"])
        assert_true(any(c.get("id") == created["id"] for c in credentials), "created credential should appear in the list")
        admin.delete_credential(state["tenant_id"], created["id"])

    def list_locks():
        holders = admin.list_locks(state["tenant_id"])
        assert_true(isinstance(holders, list), "locks listing should be a list")

    def request_history_summary():
        summary = admin.get_request_history_summary(bucketMinutes=60)
        assert_true(summary.get("totalCount", 0) >= 0, "total count should be non-negative")

    check("Health check is healthy", health)
    check("Authenticate with application key", authenticate)
    check("Get token details", token_details)
    check("Get server info", server_info)
    check("List tenants includes at least one tenant", list_tenants)
    check("Create, read, and delete a tenant", tenant_crud)
    check("Create, list, and delete a credential", credential_crud)
    check("List active locks (REST observability)", list_locks)
    check("Read request-history summary", request_history_summary)

    # ---- Lock (WebSocket) ----

    locks = ClutchLockClient(endpoint, access_key)
    write_key = unique_key("sdk-test")

    def connect():
        welcome = locks.connect()
        assert_true(bool(welcome.get("sessionId")), "welcome should include a session id")
        assert_true(welcome.get("heartbeatIntervalMs", 0) > 0, "welcome should include a heartbeat interval")

    def acquire_release():
        acquired = locks.acquire(write_key, LockMode.Write)
        assert_true(bool(acquired.get("holderId")), "acquired lock should have a holder id")
        assert_true(acquired.get("fencingToken", 0) >= 1, "fencing token should be at least 1")
        assert_true(acquired.get("mode") == LockMode.Write, "mode should be Write")
        state["first_token"] = acquired["fencingToken"]
        released = locks.release(acquired["holderId"])
        assert_true(released is True, "release should report true")

    def fencing_increases():
        acquired = locks.acquire(write_key, LockMode.Write)
        assert_true(acquired["fencingToken"] > state["first_token"],
                    f"fencing token should increase (was {state['first_token']}, got {acquired['fencingToken']})")
        locks.release(acquired["holderId"])

    def multiple_readers():
        read_key = unique_key("sdk-test-read")
        r1 = locks.acquire(read_key, LockMode.Read)
        r2 = locks.acquire(read_key, LockMode.Read)
        assert_true(bool(r1.get("holderId")) and bool(r2.get("holderId")), "both read holders should be granted")
        assert_true(r1["holderId"] != r2["holderId"], "two read holders should be distinct")
        locks.release(r1["holderId"])
        locks.release(r2["holderId"])

    def write_denies_read():
        key = unique_key("sdk-test-conflict")
        w = locks.acquire(key, LockMode.Write)
        try:
            denied = assert_raises(ClutchLockDeniedError, lambda: locks.acquire(key, LockMode.Read, behavior=LockBehavior.FailFast))
            assert_true(denied.result == AcquireResult.Denied, f"result should be Denied, was {denied.result}")
        finally:
            locks.release(w["holderId"])

    def wait_times_out():
        key = unique_key("sdk-test-timeout")
        w = locks.acquire(key, LockMode.Write)
        try:
            denied = assert_raises(ClutchLockDeniedError, lambda: locks.acquire(key, LockMode.Read, behavior=LockBehavior.Wait, timeout_ms=500))
            assert_true(denied.result == AcquireResult.Timeout, f"result should be Timeout, was {denied.result}")
        finally:
            locks.release(w["holderId"])

    def heartbeat_renews():
        key = unique_key("sdk-test-hb")
        renewed_event = threading.Event()
        locks.on_heartbeat = lambda renewed: renewed_event.set()
        acquired = locks.acquire(key, LockMode.Write)
        try:
            locks.heartbeat([acquired["holderId"]])
            assert_true(renewed_event.wait(timeout=3.0), "expected a heartbeat renewal frame within 3 seconds")
        finally:
            locks.on_heartbeat = None
            locks.release(acquired["holderId"])

    def close():
        locks.close()

    def logout():
        admin.logout()

    check("Connect lock client and receive welcome", connect)
    check("Acquire and release a write lock; obtain a fencing token", acquire_release)
    check("Fencing token increases on re-acquire of the same key", fencing_increases)
    check("Multiple readers may share a key", multiple_readers)
    check("A write lock denies a fail-fast read on the same key", write_denies_read)
    check("A waiting read times out while a write is held", wait_times_out)
    check("Heartbeat renews a held lease", heartbeat_renews)
    check("Close the lock connection", close)
    check("Logout", logout)

    admin.close()

    print("")
    print("========================================")
    print(f"  Passed: {_passed}   Failed: {_failed}")
    print("========================================")
    sys.exit(0 if _failed == 0 else 1)


if __name__ == "__main__":
    main()

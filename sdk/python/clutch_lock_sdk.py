"""Clutch Lock SDK (Python).

WebSocket client for the Clutch lock protocol. Manages one connection per tenant, acquires and
releases locks, correlates responses by request identifier, and automatically sends heartbeats
to keep held leases alive.

Depends on the ``websocket-client`` package (imported as ``websocket``).
"""

import itertools
import json
import threading
from typing import Any, Callable, Dict, List, Optional

import websocket

from clutch_admin_sdk import AcquireResult, ClutchError, LockBehavior, LockMode  # noqa: F401  (re-exported)


class ClutchLockDeniedError(ClutchError):
    """Raised when a lock acquisition is denied, times out, or conflicts with an existing policy."""

    def __init__(self, result: str, key: str, message: str):
        """Create a ClutchLockDeniedError.

        :param result: One of AcquireResult (Denied, Timeout, PolicyConflict).
        :param key: The requested key.
        :param message: A human-readable explanation.
        """
        super().__init__(message)
        self.result = result
        self.key = key


def _build_ws_url(endpoint: str) -> str:
    """Convert an HTTP(S) endpoint to the lock-connect WebSocket URL."""
    trimmed = endpoint.rstrip("/")
    if trimmed.startswith("https://"):
        trimmed = "wss://" + trimmed[len("https://"):]
    elif trimmed.startswith("http://"):
        trimmed = "ws://" + trimmed[len("http://"):]
    return f"{trimmed}/v1.0/lock/connect"


class _Pending:
    """Correlation state for a single outstanding request."""

    def __init__(self):
        self.event = threading.Event()
        self.result: Optional[Dict[str, Any]] = None


class ClutchLockClient:
    """WebSocket lock client.

    Optional callbacks may be assigned as attributes:
      - ``on_heartbeat(renewed: list)`` invoked when the server renews held leases.
      - ``on_error(message: str)`` invoked on an unsolicited error frame.
      - ``on_close(reason: str)`` invoked when the connection closes.
    """

    def __init__(self, endpoint: str, access_key: str, secret_key: Optional[str] = None, request_timeout: float = 30.0):
        """Create a ClutchLockClient.

        :param endpoint: Base URL, e.g. "http://127.0.0.1:8090".
        :param access_key: The application access key.
        :param secret_key: The optional secret key.
        :param request_timeout: Default seconds to wait for a fail-fast response. Default is 30.
        """
        if not endpoint:
            raise ClutchError("endpoint is required")
        if not access_key:
            raise ClutchError("access_key is required")
        self.url = _build_ws_url(endpoint)
        self.access_key = access_key
        self.secret_key = secret_key
        self.request_timeout = request_timeout
        self.welcome: Optional[Dict[str, Any]] = None

        self.on_heartbeat: Optional[Callable[[List[Dict[str, Any]]], None]] = None
        self.on_error: Optional[Callable[[str], None]] = None
        self.on_close: Optional[Callable[[str], None]] = None

        self._ws: Optional[websocket.WebSocket] = None
        self._pending: Dict[str, _Pending] = {}
        self._held: Dict[str, Dict[str, Any]] = {}
        self._send_lock = threading.Lock()
        self._state_lock = threading.Lock()
        self._welcome_event = threading.Event()
        self._running = False
        self._recv_thread: Optional[threading.Thread] = None
        self._heartbeat_thread: Optional[threading.Thread] = None
        self._request_ids = itertools.count(1)

    # ---- public API ----

    def connect(self) -> Dict[str, Any]:
        """Open the connection, wait for the welcome frame, and start the heartbeat loop.

        :returns: The welcome info {sessionId, tenantId, defaultLeaseMs, heartbeatIntervalMs}.
        :raises ClutchError: When the connection cannot be established.
        """
        header = [f"x-clutch-access-key: {self.access_key}"]
        if self.secret_key:
            header.append(f"x-clutch-secret-key: {self.secret_key}")

        try:
            self._ws = websocket.create_connection(self.url, header=header, enable_multithread=True)
        except Exception as ex:  # websocket raises a variety of exception types on failure
            raise ClutchError(f"Failed to connect to {self.url}: {ex}") from ex

        self._running = True
        self._recv_thread = threading.Thread(target=self._recv_loop, name="clutch-recv", daemon=True)
        self._recv_thread.start()

        if not self._welcome_event.wait(timeout=self.request_timeout):
            self.close()
            raise ClutchError("Timed out waiting for the welcome frame.")

        self._heartbeat_thread = threading.Thread(target=self._heartbeat_loop, name="clutch-heartbeat", daemon=True)
        self._heartbeat_thread.start()
        return self.welcome

    def acquire(self, key: str, mode: str, behavior: str = LockBehavior.FailFast,
                timeout_ms: Optional[int] = None, lease_ms: Optional[int] = None,
                policy: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
        """Acquire a lock on a key.

        :param key: The key to lock.
        :param mode: One of LockMode (Read, Write, Delete).
        :param behavior: One of LockBehavior (FailFast, Wait).
        :param timeout_ms: Maximum wait time in milliseconds, honored when behavior is Wait.
        :param lease_ms: Requested lease duration in milliseconds.
        :param policy: Policy applied only when this caller creates the key.
        :returns: The acquired lock {key, mode, holderId, fencingToken, leaseExpiresUtc}.
        :raises ClutchLockDeniedError: When the acquisition is denied, times out, or conflicts with a policy.
        """
        self._ensure_connected()
        request_id = self._next_request_id()
        frame: Dict[str, Any] = {"type": "acquire", "requestId": request_id, "key": key, "mode": mode, "behavior": behavior}
        if timeout_ms is not None:
            frame["timeoutMs"] = timeout_ms
        if lease_ms is not None:
            frame["leaseMs"] = lease_ms
        if policy is not None:
            frame["policy"] = policy

        wait_seconds = self.request_timeout
        if behavior == LockBehavior.Wait and timeout_ms is not None:
            wait_seconds = (timeout_ms / 1000.0) + 10.0

        response = self._send_and_await(request_id, frame, wait_seconds)
        msg_type = response.get("type")
        if msg_type == "acquired":
            held = {
                "key": response.get("key"),
                "mode": response.get("mode"),
                "holderId": response.get("holderId"),
                "fencingToken": response.get("fencingToken"),
                "leaseExpiresUtc": response.get("leaseExpiresUtc"),
            }
            with self._state_lock:
                self._held[held["holderId"]] = held
            return held
        if msg_type == "denied":
            raise ClutchLockDeniedError(response.get("result", AcquireResult.Denied), key,
                                        response.get("reason", "The lock request was denied."))
        if msg_type == "error":
            raise ClutchError(f"The server rejected the acquire request for '{key}': {response.get('message')}")
        raise ClutchError(f"Unexpected response type '{msg_type}' to the acquire request for '{key}'.")

    def release(self, holder_id: str) -> bool:
        """Release a held lock. Releasing is idempotent.

        :param holder_id: The holder identifier returned when the lock was acquired.
        :returns: True when a holder was released; False when it was already gone.
        """
        self._ensure_connected()
        with self._state_lock:
            held = self._held.get(holder_id)
        key = held["key"] if held else ""
        request_id = self._next_request_id()
        response = self._send_and_await(request_id, {"type": "release", "requestId": request_id, "key": key, "holderId": holder_id}, self.request_timeout)
        with self._state_lock:
            self._held.pop(holder_id, None)
        if response.get("type") == "error":
            raise ClutchError(f"The server rejected the release request for holder '{holder_id}': {response.get('message')}")
        return response.get("released") is True

    def heartbeat(self, holder_ids: Optional[List[str]] = None) -> None:
        """Send a heartbeat for the supplied holders, or for all held locks when none are given."""
        self._ensure_connected()
        if holder_ids is None:
            with self._state_lock:
                holder_ids = list(self._held.keys())
        if not holder_ids:
            return
        self._send({"type": "heartbeat", "holderIds": holder_ids})

    def held_locks(self) -> List[Dict[str, Any]]:
        """Return the locks currently held on this connection."""
        with self._state_lock:
            return list(self._held.values())

    def close(self) -> None:
        """Close the connection, releasing every lock held by this connection on the server side."""
        self._running = False
        if self._ws is not None:
            try:
                self._ws.close()
            except Exception:
                pass
        self._fail_all_pending(ClutchError("The lock client was closed."))

    # ---- internals ----

    def _next_request_id(self) -> str:
        return f"r{next(self._request_ids)}"

    def _ensure_connected(self) -> None:
        if self._ws is None or not self._running:
            raise ClutchError("The lock client is not connected. Call connect() first.")

    def _send(self, frame: Dict[str, Any]) -> None:
        payload = json.dumps(frame)
        with self._send_lock:
            if self._ws is None:
                raise ClutchError("The lock client is not connected.")
            try:
                self._ws.send(payload)
            except Exception as ex:
                raise ClutchError(f"Failed to send a frame: {ex}") from ex

    def _send_and_await(self, request_id: str, frame: Dict[str, Any], wait_seconds: float) -> Dict[str, Any]:
        pending = _Pending()
        self._pending[request_id] = pending
        try:
            self._send(frame)
            if not pending.event.wait(timeout=wait_seconds):
                raise ClutchError(f"Timed out waiting for a response to request '{request_id}'.")
            if pending.result is None:
                raise ClutchError(f"The connection closed before request '{request_id}' completed.")
            return pending.result
        finally:
            self._pending.pop(request_id, None)

    def _recv_loop(self) -> None:
        reason = "closed"
        try:
            while self._running and self._ws is not None:
                try:
                    text = self._ws.recv()
                except (websocket.WebSocketConnectionClosedException, OSError):
                    reason = "connection closed"
                    break
                except Exception as ex:  # noqa: BLE001 - surface any receive failure as a close
                    reason = str(ex)
                    break
                if text is None or text == "":
                    continue
                if isinstance(text, bytes):
                    text = text.decode("utf-8", errors="replace")
                self._dispatch(text)
        finally:
            self._running = False
            if not self._welcome_event.is_set():
                self._welcome_event.set()
            self._fail_all_pending(ClutchError(f"The connection closed: {reason}"))
            if self.on_close is not None:
                try:
                    self.on_close(reason)
                except Exception:
                    pass

    def _dispatch(self, text: str) -> None:
        try:
            msg = json.loads(text)
        except ValueError:
            return

        msg_type = msg.get("type")
        if msg_type == "welcome":
            self.welcome = {
                "sessionId": msg.get("sessionId"),
                "tenantId": msg.get("tenantId"),
                "defaultLeaseMs": msg.get("defaultLeaseMs"),
                "heartbeatIntervalMs": msg.get("heartbeatIntervalMs"),
            }
            self._welcome_event.set()
        elif msg_type in ("acquired", "denied", "released"):
            self._complete(msg)
        elif msg_type == "heartbeat":
            self._handle_heartbeat(msg)
        elif msg_type == "error":
            request_id = msg.get("requestId")
            if request_id and request_id in self._pending:
                self._complete(msg)
            elif self.on_error is not None:
                try:
                    self.on_error(msg.get("message", "unknown error"))
                except Exception:
                    pass
        # 'pong' and unknown types are ignored

    def _complete(self, msg: Dict[str, Any]) -> None:
        request_id = msg.get("requestId")
        if not request_id:
            return
        pending = self._pending.get(request_id)
        if pending is not None:
            pending.result = msg
            pending.event.set()

    def _handle_heartbeat(self, msg: Dict[str, Any]) -> None:
        renewed: List[Dict[str, Any]] = []
        for item in msg.get("renewed", []) or []:
            holder_id = item.get("holderId")
            with self._state_lock:
                held = self._held.get(holder_id)
                if held is not None:
                    held["leaseExpiresUtc"] = item.get("leaseExpiresUtc")
                    renewed.append(held)
        if renewed and self.on_heartbeat is not None:
            try:
                self.on_heartbeat(renewed)
            except Exception:
                pass

    def _heartbeat_loop(self) -> None:
        interval = 10.0
        if self.welcome and self.welcome.get("heartbeatIntervalMs"):
            interval = max(1.0, self.welcome["heartbeatIntervalMs"] / 1000.0)
        while self._running:
            waited = 0.0
            step = 0.5
            while self._running and waited < interval:
                threading.Event().wait(step)
                waited += step
            if not self._running:
                break
            with self._state_lock:
                has_held = len(self._held) > 0
            if has_held:
                try:
                    self.heartbeat()
                except ClutchError:
                    pass

    def _fail_all_pending(self, error: ClutchError) -> None:
        for pending in list(self._pending.values()):
            if pending.result is None:
                pending.event.set()
        self._pending.clear()

    def __enter__(self) -> "ClutchLockClient":
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()

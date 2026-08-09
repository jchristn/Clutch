"""Clutch Admin SDK (Python).

REST client for Clutch administration and observability: tokens, tenants, users, application keys,
lock inspection, audit, request history, health, and server info. Lock acquisition itself is
performed over the WebSocket API via clutch_lock_sdk.py.

Depends on the ``requests`` package.
"""

from typing import Any, Dict, List, Optional

import requests


class LockMode:
    """Lock modes. Determines compatibility with other holders on the same key."""

    Read = "Read"
    Write = "Write"
    Delete = "Delete"


class LockBehavior:
    """Behavior applied when a lock cannot be granted immediately."""

    FailFast = "FailFast"
    Wait = "Wait"


class AcquireResult:
    """Outcome of a lock acquisition request."""

    Acquired = "Acquired"
    Denied = "Denied"
    Timeout = "Timeout"
    PolicyConflict = "PolicyConflict"


class ClutchError(Exception):
    """Raised when a Clutch REST or WebSocket operation fails."""

    def __init__(self, message: str, status_code: int = 0, response_body: Optional[str] = None):
        """Create a ClutchError.

        :param message: Human-readable error message.
        :param status_code: HTTP status code, or 0 when not applicable.
        :param response_body: Raw response body, when available.
        """
        super().__init__(message)
        self.status_code = status_code
        self.response_body = response_body


class ClutchAdminClient:
    """REST client for the Clutch API."""

    def __init__(self, endpoint: str, token: Optional[str] = None, timeout: float = 30.0):
        """Create a ClutchAdminClient.

        :param endpoint: Base URL, e.g. "http://127.0.0.1:8090".
        :param token: Optional pre-existing bearer token.
        :param timeout: Per-request timeout in seconds. Default is 30.
        """
        if not endpoint:
            raise ClutchError("endpoint is required")
        self.endpoint = endpoint.rstrip("/")
        self.token = token
        self.timeout = timeout
        self._session = requests.Session()

    def close(self) -> None:
        """Close the underlying HTTP session."""
        self._session.close()

    def __enter__(self) -> "ClutchAdminClient":
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()

    # ---- internals ----

    @staticmethod
    def _clean(params: Dict[str, Any]) -> Dict[str, Any]:
        """Drop entries whose value is None or an empty string."""
        return {k: v for k, v in params.items() if v is not None and v != ""}

    def _request(self, method: str, path: str, body: Optional[Any] = None, auth: bool = True) -> Any:
        """Send an HTTP request and return the parsed response.

        :param method: HTTP method.
        :param path: Request path beginning with '/'.
        :param body: Optional JSON body.
        :param auth: Whether to send the bearer token.
        :returns: Parsed response body, or None for empty responses.
        :raises ClutchError: When the request fails or returns a non-2xx status.
        """
        headers = {"Accept": "application/json"}
        if auth:
            if not self.token:
                raise ClutchError(f"A bearer token is required for {method} {path}. Authenticate first.")
            headers["Authorization"] = f"Bearer {self.token}"

        try:
            response = self._session.request(
                method, f"{self.endpoint}{path}", json=body, headers=headers, timeout=self.timeout
            )
        except requests.RequestException as ex:
            raise ClutchError(f"The request to {method} {path} failed: {ex}") from ex

        text = response.text
        if not response.ok:
            raise ClutchError(f"{method} {path} returned {response.status_code}: {text}", response.status_code, text)
        if not text:
            return None
        try:
            return response.json()
        except ValueError as ex:
            raise ClutchError(f"Failed to parse the response for {method} {path}: {ex}", response.status_code, text) from ex

    # ---- Tokens ----

    def authenticate_with_key(self, access_key: str) -> Dict[str, Any]:
        """Authenticate with an application key. Stores the returned token on this client.

        :returns: The token response {token, principalType, tenantId}.
        """
        result = self._request("POST", "/v1.0/token", {"accessKey": access_key}, auth=False)
        self.token = result.get("token")
        return result

    def authenticate_with_password(self, tenant_id: str, email: str, password: str) -> Dict[str, Any]:
        """Authenticate with user credentials. Stores the returned token on this client.

        :returns: The token response {token, principalType, tenantId}.
        """
        result = self._request("POST", "/v1.0/token", {"tenantId": tenant_id, "email": email, "password": password}, auth=False)
        self.token = result.get("token")
        return result

    def get_token_details(self) -> Dict[str, Any]:
        """Validate the current token and return the resolved principal context."""
        return self._request("GET", "/v1.0/token")

    def logout(self) -> None:
        """Revoke the current session (logout) and clear the stored token."""
        self._request("DELETE", "/v1.0/token")
        self.token = None

    # ---- Health and server info ----

    def get_health(self) -> Dict[str, Any]:
        """Retrieve server health. Anonymous."""
        return self._request("GET", "/v1.0/api/health", auth=False)

    def get_server_info(self) -> Dict[str, Any]:
        """Retrieve information about the running server."""
        return self._request("GET", "/v1.0/api/server-info")

    # ---- Tenants ----

    def list_tenants(self, **filters: Any) -> List[Dict[str, Any]]:
        """List tenants (paginated). Returns the page's objects.

        :param filters: maxResults, skip, ordering.
        """
        result = self._request_with_params("GET", "/v1.0/api/tenants", self._clean(filters))
        return (result or {}).get("objects", [])

    def get_tenant(self, tenant_id: str) -> Dict[str, Any]:
        """Retrieve a single tenant."""
        return self._request("GET", f"/v1.0/api/tenants/{tenant_id}")

    def create_tenant(self, name: str, lock_history_retention_days: Optional[int] = None,
                       default_lease_ms: Optional[int] = None, max_lease_ms: Optional[int] = None) -> Dict[str, Any]:
        """Create a tenant. Requires a system administrator.

        :returns: The created tenant.
        """
        body = self._clean({
            "name": name,
            "lockHistoryRetentionDays": lock_history_retention_days,
            "defaultLeaseMs": default_lease_ms,
            "maxLeaseMs": max_lease_ms,
        })
        return self._request("POST", "/v1.0/api/tenants", body)

    def update_tenant(self, tenant_id: str, fields: Dict[str, Any]) -> Dict[str, Any]:
        """Update a tenant with the supplied fields."""
        return self._request("PUT", f"/v1.0/api/tenants/{tenant_id}", fields)

    def delete_tenant(self, tenant_id: str) -> None:
        """Delete a tenant. Cascades to users, credentials, locks, and audit."""
        self._request("DELETE", f"/v1.0/api/tenants/{tenant_id}")

    # ---- Users ----

    def list_users(self, tenant_id: str, **filters: Any) -> List[Dict[str, Any]]:
        """List users within a tenant (paginated). Returns the page's objects.

        :param filters: maxResults, skip, ordering.
        """
        result = self._request_with_params("GET", f"/v1.0/api/tenants/{tenant_id}/users", self._clean(filters))
        return (result or {}).get("objects", [])

    def get_user(self, tenant_id: str, user_id: str) -> Dict[str, Any]:
        """Retrieve a single user."""
        return self._request("GET", f"/v1.0/api/tenants/{tenant_id}/users/{user_id}")

    def create_user(self, tenant_id: str, user: Dict[str, Any]) -> Dict[str, Any]:
        """Create a user. ``user`` is {email, password, firstName?, lastName?, isSystemAdmin?, isTenantAdmin?, active?}."""
        return self._request("POST", f"/v1.0/api/tenants/{tenant_id}/users", user)

    def update_user(self, tenant_id: str, user_id: str, fields: Dict[str, Any]) -> Dict[str, Any]:
        """Update a user with the supplied fields."""
        return self._request("PUT", f"/v1.0/api/tenants/{tenant_id}/users/{user_id}", fields)

    def delete_user(self, tenant_id: str, user_id: str) -> None:
        """Delete a user."""
        self._request("DELETE", f"/v1.0/api/tenants/{tenant_id}/users/{user_id}")

    # ---- Credentials ----

    def list_credentials(self, tenant_id: str, **filters: Any) -> List[Dict[str, Any]]:
        """List application keys within a tenant (paginated). Returns the page's objects.

        :param filters: maxResults, skip, ordering.
        """
        result = self._request_with_params("GET", f"/v1.0/api/tenants/{tenant_id}/credentials", self._clean(filters))
        return (result or {}).get("objects", [])

    def get_credential(self, tenant_id: str, credential_id: str) -> Dict[str, Any]:
        """Retrieve a single application key."""
        return self._request("GET", f"/v1.0/api/tenants/{tenant_id}/credentials/{credential_id}")

    def create_credential(self, tenant_id: str, name: str, user_id: Optional[str] = None,
                          expires_utc: Optional[str] = None) -> Dict[str, Any]:
        """Create an application key.

        :returns: The created credential, including its access key.
        """
        body = self._clean({"name": name, "userId": user_id, "expiresUtc": expires_utc})
        return self._request("POST", f"/v1.0/api/tenants/{tenant_id}/credentials", body)

    def delete_credential(self, tenant_id: str, credential_id: str) -> None:
        """Delete an application key."""
        self._request("DELETE", f"/v1.0/api/tenants/{tenant_id}/credentials/{credential_id}")

    # ---- Locks ----

    def list_locks(self, tenant_id: str, name: Optional[str] = None, mode: Optional[str] = None, **filters: Any) -> List[Dict[str, Any]]:
        """List active lock holders within a tenant (paginated), optionally filtered by key substring and mode.

        :param filters: maxResults, skip, ordering.
        """
        params = self._clean({"name": name, "mode": mode, **filters})
        result = self._request_with_params("GET", f"/v1.0/api/tenants/{tenant_id}/locks", params)
        return (result or {}).get("objects", [])

    def get_lock(self, tenant_id: str, key: str) -> Dict[str, Any]:
        """Retrieve the definition and holders for a single lock key."""
        return self._request("GET", f"/v1.0/api/tenants/{tenant_id}/locks/{key}")

    def release_lock(self, tenant_id: str, key: str) -> Dict[str, Any]:
        """Force-release every holder on a key. Requires a tenant administrator.

        :returns: The release result {key, released}.
        """
        return self._request("POST", f"/v1.0/api/tenants/{tenant_id}/locks/{key}/release")

    # ---- Lock audit ----

    def get_lock_audit(self, tenant_id: str, **filters: Any) -> Dict[str, Any]:
        """Retrieve a page of lock audit entries.

        :param filters: name, mode, fromUtc, toUtc, maxResults, skip, ordering.
        :returns: An EnumerationResult {objects, totalRecords, recordsRemaining, endOfResults, maxResults, skip}.
        """
        return self._request_with_params("GET", f"/v1.0/api/tenants/{tenant_id}/lock-audit", self._clean(filters))

    def get_lock_audit_summary(self, tenant_id: str, **filters: Any) -> Dict[str, Any]:
        """Retrieve a time-bucketed summary of lock acquire activity.

        :param filters: name, mode, fromUtc, toUtc, bucketCount.
        """
        return self._request_with_params("GET", f"/v1.0/api/tenants/{tenant_id}/lock-audit/summary", self._clean(filters))

    # ---- Request history ----

    def get_request_history(self, **filters: Any) -> Dict[str, Any]:
        """Retrieve a page of request history.

        :param filters: method, statusCode, pathContains, fromUtc, toUtc, maxResults, skip, ordering, tenantId.
        :returns: An EnumerationResult {objects, totalRecords, recordsRemaining, endOfResults, maxResults, skip}.
        """
        return self._request_with_params("GET", "/v1.0/api/request-history", self._clean(filters))

    def get_request_history_summary(self, **filters: Any) -> Dict[str, Any]:
        """Retrieve a bucketed summary of request history.

        :param filters: method, statusCode, pathContains, fromUtc, toUtc, bucketMinutes, tenantId.
        """
        return self._request_with_params("GET", "/v1.0/api/request-history/summary", self._clean(filters))

    def get_request_history_entry(self, entry_id: str) -> Dict[str, Any]:
        """Retrieve a single request history entry."""
        return self._request("GET", f"/v1.0/api/request-history/{entry_id}")

    def delete_request_history_entry(self, entry_id: str) -> None:
        """Delete a single request history entry."""
        self._request("DELETE", f"/v1.0/api/request-history/{entry_id}")

    def delete_request_history(self, **filters: Any) -> Dict[str, Any]:
        """Bulk-delete request history entries matching the supplied filter.

        :returns: The delete result {deletedCount}.
        """
        return self._request_with_params("DELETE", "/v1.0/api/request-history", self._clean(filters))

    def _request_with_params(self, method: str, path: str, params: Dict[str, Any]) -> Any:
        """Send a request whose query parameters are supplied separately from the path."""
        query = ""
        if params:
            query = "?" + "&".join(f"{requests.utils.quote(str(k))}={requests.utils.quote(str(v))}" for k, v in params.items())
        return self._request(method, f"{path}{query}")

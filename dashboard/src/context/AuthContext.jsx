import { createContext, useContext, useState, useEffect, useCallback } from 'react';
import ApiClient from '../utils/api';
import { STORAGE_KEYS } from '../utils/constants';

const AuthContext = createContext(null);

/**
 * Authentication + session provider. Owns the ApiClient, server URL, token, the
 * resolved principal, and the theme. Restores a saved session on load and
 * validates it via GET /v1.0/token.
 */
export function AuthProvider({ children }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [apiClient, setApiClient] = useState(null);
  const [serverUrl, setServerUrl] = useState('');
  const [token, setToken] = useState('');
  const [principal, setPrincipal] = useState(null);
  const [theme, setTheme] = useState('light');

  // Restore from localStorage on mount.
  useEffect(() => {
    const savedUrl = localStorage.getItem(STORAGE_KEYS.serverUrl);
    const savedToken = localStorage.getItem(STORAGE_KEYS.token);
    const savedTheme = localStorage.getItem(STORAGE_KEYS.theme) || 'light';

    setTheme(savedTheme);
    document.body.setAttribute('data-theme', savedTheme);

    if (savedUrl && savedToken) {
      const client = new ApiClient(savedUrl, savedToken);
      client
        .validateToken()
        .then((p) => {
          if (!p || p.authenticated === false) throw new Error('invalid');
          setServerUrl(savedUrl);
          setToken(savedToken);
          setApiClient(client);
          setPrincipal(p);
          setIsAuthenticated(true);
        })
        .catch(() => {
          localStorage.removeItem(STORAGE_KEYS.serverUrl);
          localStorage.removeItem(STORAGE_KEYS.token);
        })
        .finally(() => setIsLoading(false));
    } else {
      setIsLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    // Best-effort server-side revoke; do not block the UI on it.
    if (apiClient) apiClient.revokeToken().catch(() => {});
    localStorage.removeItem(STORAGE_KEYS.serverUrl);
    localStorage.removeItem(STORAGE_KEYS.token);
    setServerUrl('');
    setToken('');
    setApiClient(null);
    setPrincipal(null);
    setIsAuthenticated(false);
  }, [apiClient]);

  // Global 401 handler: any request that 401s forces a logout.
  useEffect(() => {
    const handler = () => logout();
    window.addEventListener('auth:unauthorized', handler);
    return () => window.removeEventListener('auth:unauthorized', handler);
  }, [logout]);

  /**
   * Log in. `mode` is 'key' ({ accessKey }) or
   * 'password' ({ tenantId, email, password }).
   */
  const login = useCallback(async (url, mode, creds) => {
    const tempClient = new ApiClient(url, null);
    const result =
      mode === 'password'
        ? await tempClient.loginWithPassword(creds.tenantId, creds.email, creds.password)
        : await tempClient.loginWithKey(creds.accessKey);

    const newToken = result?.token;
    if (!newToken) throw new Error('No token returned');

    const client = new ApiClient(url, newToken);
    const p = await client.validateToken();

    localStorage.setItem(STORAGE_KEYS.serverUrl, url);
    localStorage.setItem(STORAGE_KEYS.token, newToken);

    setServerUrl(url);
    setToken(newToken);
    setApiClient(client);
    setPrincipal(p);
    setIsAuthenticated(true);
  }, []);

  const toggleTheme = useCallback(() => {
    setTheme((prev) => {
      const next = prev === 'light' ? 'dark' : 'light';
      localStorage.setItem(STORAGE_KEYS.theme, next);
      document.body.setAttribute('data-theme', next);
      return next;
    });
  }, []);

  const value = {
    isAuthenticated,
    isLoading,
    apiClient,
    serverUrl,
    token,
    principal,
    theme,
    login,
    logout,
    toggleTheme
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
}

export default AuthContext;

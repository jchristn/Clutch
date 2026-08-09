import { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import { principalCaps } from '../utils/principal';

/**
 * Resolve the effective tenant for tenant-scoped views. System admins get a
 * fetched tenant list and a selectable tenantId; everyone else is pinned to
 * their own tenantId (from the resolved principal).
 */
export default function useTenantScope() {
  const { apiClient, principal } = useAuth();
  const caps = principalCaps(principal);
  const [tenants, setTenants] = useState([]);
  const [tenantId, setTenantId] = useState(caps.tenantId || '');
  const [loading, setLoading] = useState(caps.isSystemAdmin);

  useEffect(() => {
    if (!caps.isSystemAdmin || !apiClient) return;
    let active = true;
    setLoading(true);
    apiClient
      .listTenants({ pageSize: 1000 })
      .then((res) => {
        if (!active) return;
        const arr = res?.items || [];
        setTenants(arr);
        setTenantId((prev) => prev || arr[0]?.id || '');
      })
      .catch(() => active && setTenants([]))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [apiClient, caps.isSystemAdmin]);

  return {
    tenants,
    tenantId,
    setTenantId,
    isSystemAdmin: caps.isSystemAdmin,
    caps,
    loading
  };
}

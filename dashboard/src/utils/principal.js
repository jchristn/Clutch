// Derive client-side UX capabilities from the resolved principal.
// NOTE: client-side gating is UX only; the backend enforces authorization.

export function principalCaps(principal) {
  const isSystemAdmin = !!principal?.isAdmin;
  const isTenantAdmin = !!principal?.isTenantAdmin;
  return {
    isSystemAdmin,
    isTenantAdmin,
    isAdminOfSomething: isSystemAdmin || isTenantAdmin,
    tenantId: principal?.tenantId || null,
    principalName: principal?.principalName || '',
    principalType: principal?.principalType || ''
  };
}

/** Human role label key for i18n. */
export function roleKey(principal) {
  if (principal?.isAdmin) return 'roles.systemAdmin';
  if (principal?.isTenantAdmin) return 'roles.tenantAdmin';
  if (principal?.credentialId) return 'roles.credential';
  return 'roles.user';
}

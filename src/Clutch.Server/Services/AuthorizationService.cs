namespace Clutch.Server.Services
{
    using Clutch.Core.Security;

    /// <summary>
    /// Authorization decisions for the three-tier model: system administrator, tenant administrator, and
    /// regular user. RBAC is intentionally not implemented. Client-side checks are UI-only; the server
    /// re-enforces every decision here.
    /// </summary>
    public class AuthorizationService
    {
        #region Public-Methods

        /// <summary>
        /// Whether the principal may manage and view all tenants.
        /// </summary>
        /// <param name="context">Request context.</param>
        /// <returns>True if permitted.</returns>
        public bool CanManageTenants(RequestContext context)
        {
            return context != null && context.IsAuthenticated && context.IsAdmin;
        }

        /// <summary>
        /// Whether the principal may read within a tenant.
        /// </summary>
        /// <param name="context">Request context.</param>
        /// <param name="tenantId">Target tenant identifier.</param>
        /// <returns>True if permitted.</returns>
        public bool CanReadTenant(RequestContext context, string tenantId)
        {
            if (context == null || !context.IsAuthenticated) return false;
            if (context.IsAdmin) return true;
            return context.TenantId == tenantId;
        }

        /// <summary>
        /// Whether the principal may administer (mutate) within a tenant. Requires system admin, or tenant
        /// admin within the same tenant.
        /// </summary>
        /// <param name="context">Request context.</param>
        /// <param name="tenantId">Target tenant identifier.</param>
        /// <returns>True if permitted.</returns>
        public bool CanAdministerTenant(RequestContext context, string tenantId)
        {
            if (context == null || !context.IsAuthenticated) return false;
            if (context.IsAdmin) return true;
            return context.IsTenantAdmin && context.TenantId == tenantId;
        }

        /// <summary>
        /// Resolve the effective tenant scope for a request. System administrators may target any tenant
        /// (or none); all other principals are pinned to their own tenant regardless of what they request.
        /// </summary>
        /// <param name="context">Request context.</param>
        /// <param name="requestedTenantId">Tenant identifier requested by the caller, if any.</param>
        /// <returns>The effective tenant identifier, or null for a cross-tenant admin scope.</returns>
        public string? ResolveTenantScope(RequestContext context, string? requestedTenantId)
        {
            if (context == null || !context.IsAuthenticated) return null;
            if (context.IsAdmin) return requestedTenantId;
            return context.TenantId;
        }

        #endregion
    }
}

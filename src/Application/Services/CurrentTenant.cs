using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;

public class CurrentTenant(IHttpContextAccessor _httpContextAccessor) : ICurrentTenant
{
    public Guid Id
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return Guid.Empty;

            // The X-Tenant-Subdomain header is attacker-controlled. TenantResolverMiddleware parks the
            // resolved id in HttpContext.Items["TenantId"] for unauthenticated flows (login, onboarding,
            // password reset) where there is no JWT yet.
            Guid? headerTenant = null;
            if (httpContext.Items.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is Guid h)
                headerTenant = h;

            // For an authenticated request the JWT is the single source of truth. Trusting the header
            // here would let any logged-in user pivot into another school's data by sending its subdomain.
            if (httpContext.User?.Identity?.IsAuthenticated == true)
            {
                var claim = httpContext.User.FindFirst("tenant_id")?.Value;
                if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var tokenTenant))
                    return Guid.Empty;

                // Defense in depth: a header that disagrees with the token is an explicit cross-tenant
                // attempt — reject it rather than silently picking one.
                if (headerTenant.HasValue && headerTenant.Value != tokenTenant)
                    throw new TenantMismatchException(
                        "The tenant in the request header does not match the authenticated user's tenant.");

                return tokenTenant;
            }

            // Unauthenticated: fall back to the header-resolved tenant (or Guid.Empty if absent).
            return headerTenant ?? Guid.Empty;
        }
    }
}

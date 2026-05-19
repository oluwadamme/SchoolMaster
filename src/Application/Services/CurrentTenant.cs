using SchoolMaster.Application.Services.Interfaces;

public class CurrentTenant(IHttpContextAccessor _httpContextAccessor) : ICurrentTenant
{

    public Guid Id
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return Guid.Empty;

            if (httpContext.Items.TryGetValue("TenantId", out var tenantIdObj) && tenantIdObj is Guid tenantId)
            {
                return tenantId;
            }

            var claim = httpContext.User.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrEmpty(claim))
                return Guid.Empty;

            return Guid.Parse(claim);
        }
    }
}
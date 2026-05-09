using SchoolMaster.Application.Services.Interfaces;

public class CurrentTenant(IHttpContextAccessor _httpContextAccessor) : ICurrentTenant
{

    public Guid Id
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrEmpty(claim))
                return Guid.Empty;

            return Guid.Parse(claim);
        }
    }
}
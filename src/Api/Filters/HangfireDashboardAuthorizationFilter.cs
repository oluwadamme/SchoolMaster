using Hangfire.Dashboard;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Filters;
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated is true
            && httpContext.User.IsInRole(nameof(UserRole.Admin));
    }
}

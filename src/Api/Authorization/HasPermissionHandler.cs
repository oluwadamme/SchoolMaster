using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Authorization;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionHandler : AuthorizationHandler<HasPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasPermissionRequirement requirement)
    {
        var permissionClaims = context.User.Claims
            .Where(c => c.Type ==PermissionClaimType.Type)
            .Select(c => c.Value)
            .ToHashSet();

        if (permissionClaims.Contains(requirement.Permission.ToString()))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Authorization;

namespace SchoolMaster.Api.Authorization;
// actually logic to compare the permissions in the claim with the permission in the attribute

// whenever the system sees a HasPermissionRequirement created to store the attribute data, it calls this class
public class HasPermissionHandler : AuthorizationHandler<HasPermissionRequirement>
{
    // context is the user request
    
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

using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Authorization;

namespace SchoolMaster.Api.Authorization;
// actually logic to compare the permissions in the claim with the permission requirement in the attribute

// AuthorizationHandlerContext is a smaller "box" c# creates to store
// the user claims and the requirement
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
            // when called, goes to HasSucceeded property in context and changes it from
            // false to true
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

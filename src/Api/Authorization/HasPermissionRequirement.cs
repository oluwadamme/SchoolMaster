using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionRequirement : IAuthorizationRequirement
{
    // this instance is created for every permission enum value 
    // When the system sees your attribute in [HasPermission(attribute)], it looks for the instance of the HasPermissionRequirement class that has the same permission value as the one in the attribute, and then it calls the handler to compare the claim with the requirement
    public Permission Permission { get; }

    public HasPermissionRequirement(Permission permission)
    {
        Permission = permission;
    }
}

using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionRequirement : IAuthorizationRequirement
{
    // when the code runs, this instance is created for each permission in every UserRole enum value 
    public Permission Permission { get; }

    public HasPermissionRequirement(Permission permission)
    {
        Permission = permission;
    }
}

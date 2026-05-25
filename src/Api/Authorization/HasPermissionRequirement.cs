using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionRequirement : IAuthorizationRequirement
{
    public Permission Permission { get; }

    public HasPermissionRequirement(Permission permission)
    {
        Permission = permission;
    }
}

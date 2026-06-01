using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(Permission permission)
        : base(permission.ToString())
    {
    }
}

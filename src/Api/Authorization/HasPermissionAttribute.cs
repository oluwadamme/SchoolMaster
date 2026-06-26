using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    // passes the permission to the base authorizeattribute constructor so it knows what policy to look for
    public HasPermissionAttribute(Permission permission)
        : base(permission.ToString())
    {
    }
}

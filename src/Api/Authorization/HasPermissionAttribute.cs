using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    // when your code runs, It scans all your controllers, reads every single [HasPermission], 
    // and call this class. this class takes permission value i.e. Permission.StudentsCreate 
    // in [HasPermission(Permission.StudentsCreate)] and passes it as a text string on to the base 
    // thus every single [HasPermission] you typed in your code, there is exactly one HasPermissionAttribute 
    // Object sitting permanently in the computer's memory, holding its own specific text string.
    // NOTE: THIS IS BEFORE A REQUEST IS MADE TO ANY CONTROLLER
    public HasPermissionAttribute(Permission permission)
        : base(permission.ToString()) 
    {
    }
}

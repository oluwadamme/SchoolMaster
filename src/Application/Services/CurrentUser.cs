using System.Security.Claims;
using SchoolMaster.Application.Services.Interfaces;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id
    {
        get
        {
            var claim = accessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return claim is null
                ? throw new UnauthorizedAccessException("User identity claim is missing from the token.")
                : Guid.Parse(claim);
        }
    }
}

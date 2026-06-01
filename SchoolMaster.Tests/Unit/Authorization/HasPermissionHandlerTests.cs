using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Api.Authorization;
using SchoolMaster.Domain.Authorization;
using SchoolMaster.Domain.Enums;
using Xunit;

namespace SchoolMaster.Tests.Unit.Authorization;

public class HasPermissionHandlerTests
{
    private static AuthorizationHandlerContext MakeContext(
        HasPermissionRequirement requirement,
        IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "Test");
        var user = new ClaimsPrincipal(identity);
        return new AuthorizationHandlerContext(new[] { requirement }, user, null);
    }

    // -------------------------------------------------------------------------
    // Success cases
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_Succeeds_WhenRequiredPermissionClaimIsPresent()
    {
        var requirement = new HasPermissionRequirement(Permission.StudentsCreate);
        var context = MakeContext(requirement, new[]
        {
            new Claim(PermissionClaimType.Type, Permission.StudentsCreate.ToString())
        });

        await new HasPermissionHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_Succeeds_WhenUserHasMultiplePermissionsIncludingRequired()
    {
        var requirement = new HasPermissionRequirement(Permission.AttendanceMark);
        var context = MakeContext(requirement, new[]
        {
            new Claim(PermissionClaimType.Type, Permission.StudentsRead.ToString()),
            new Claim(PermissionClaimType.Type, Permission.AttendanceMark.ToString()),
            new Claim(PermissionClaimType.Type, Permission.AcademicViewTimetable.ToString()),
        });

        await new HasPermissionHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    // -------------------------------------------------------------------------
    // Failure cases
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_DoesNotSucceed_WhenRequiredPermissionClaimIsMissing()
    {
        var requirement = new HasPermissionRequirement(Permission.StudentsCreate);
        var context = MakeContext(requirement, new[]
        {
            new Claim(PermissionClaimType.Type, Permission.StudentsRead.ToString())
        });

        await new HasPermissionHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_DoesNotSucceed_WhenUserHasNoPermissionClaims()
    {
        var requirement = new HasPermissionRequirement(Permission.StudentsCreate);
        var context = MakeContext(requirement, new[]
        {
            new Claim(ClaimTypes.Email, "user@test.com"),
            new Claim(ClaimTypes.Role, "Admin"),
        });

        await new HasPermissionHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_DoesNotSucceed_WhenUserHasNoClaims()
    {
        var requirement = new HasPermissionRequirement(Permission.StudentsCreate);
        var context = MakeContext(requirement, Enumerable.Empty<Claim>());

        await new HasPermissionHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_IsCaseSensitive_DoesNotSucceedOnLowercaseMatch()
    {
        // Permission enum values are PascalCase — a lowercase claim must not match
        var requirement = new HasPermissionRequirement(Permission.StudentsCreate);
        var context = MakeContext(requirement, new[]
        {
            new Claim(PermissionClaimType.Type, "studentscreate")
        });

        await new HasPermissionHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_DoesNotSucceed_WhenClaimTypeIsWrongEvenIfValueMatches()
    {
        // A claim with the right value but wrong type must not satisfy the requirement
        var requirement = new HasPermissionRequirement(Permission.StudentsCreate);
        var context = MakeContext(requirement, new[]
        {
            new Claim("role", Permission.StudentsCreate.ToString())
        });

        await new HasPermissionHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}

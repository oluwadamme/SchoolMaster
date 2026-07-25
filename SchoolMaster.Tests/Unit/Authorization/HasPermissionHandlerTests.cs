using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolMaster.Api.Authorization;
using SchoolMaster.Domain.Authorization;
using SchoolMaster.Domain.Enums;
using Xunit;

// Unit tests test pieces of code in isolation thus it's best to build the context
// manually rather than waiting on a fake web server. it is much faster this way

namespace SchoolMaster.Tests.Unit.Authorization;

public class HasPermissionHandlerTests
{
    // manually creating what .NET does automatically
    private static AuthorizationHandlerContext MakeContext(
        HasPermissionRequirement requirement,
        IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "Test");
        var user = new ClaimsPrincipal(identity);
        // a built in class that takes in the requirement and the claim
        // a container that bundles the requirement and the claim
        return new AuthorizationHandlerContext(new[] { requirement }, user, null);
    }

    // -------------------------------------------------------------------------
    // Success cases
    // -------------------------------------------------------------------------
    // Xunit scans your project looking for [Fact] attributes, 
    // and executes the method marked with it.
    [Fact]
    public async Task HandleAsync_Succeeds_WhenRequiredPermissionClaimIsPresent()
    {
        var requirement = new HasPermissionRequirement(Permission.StudentsCreate);

        var context = MakeContext(requirement, new[]
        {
            new Claim(PermissionClaimType.Type, Permission.StudentsCreate.ToString())
        });

        // .handleAsync is a built in method that does some quick setup in the background,
        //  and then it automatically calls the HandleRequirementAsync method that you wrote

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

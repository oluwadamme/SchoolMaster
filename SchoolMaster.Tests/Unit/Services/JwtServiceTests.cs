using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolMaster.Domain.Authorization;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Infrastructure.Options;
using SchoolMaster.Infrastructure.Services;
using Xunit;

namespace SchoolMaster.Tests.Unit.Services;

public class JwtServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Key = "test-secret-key-minimum-32-characters-abc!!",
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpirationInMinutes = 60
    };

    private static JwtService CreateSut() =>
        new(Microsoft.Extensions.Options.Options.Create(Options));

    private static User MakeUser(List<UserRole> roles) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "User",
        Email = "test@test.com",
        PasswordHash = "hash",
        Roles = roles
    };

    // Validates and reads a token back into a ClaimsPrincipal using the same
    // parameters the real application uses, so claims are correctly mapped.
    private static ClaimsPrincipal ReadToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Options.Issuer,
            ValidateAudience = true,
            ValidAudience = Options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Options.Key)),
            ValidateLifetime = true
        };
        return handler.ValidateToken(token, parameters, out _);
    }

    // -------------------------------------------------------------------------
    // Role claims
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateAccessToken_StampsOneRoleClaimPerRole()
    {
        var user = MakeUser(new List<UserRole> { UserRole.Teacher });

        var token = CreateSut().GenerateAccessToken(user);
        var principal = ReadToken(token);

        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Single(roleClaims);
        Assert.Contains("Teacher", roleClaims);
    }

    [Fact]
    public void GenerateAccessToken_DualRole_StampsTwoSeparateRoleClaims()
    {
        var user = MakeUser(new List<UserRole> { UserRole.Teacher, UserRole.Parent });

        var token = CreateSut().GenerateAccessToken(user);
        var principal = ReadToken(token);

        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Equal(2, roleClaims.Count);
        Assert.Contains("Teacher", roleClaims);
        Assert.Contains("Parent", roleClaims);
    }

    [Fact]
    public void GenerateAccessToken_DoesNotStampRolesAsCommaSeparatedSingleClaim()
    {
        var user = MakeUser(new List<UserRole> { UserRole.Teacher, UserRole.Parent });

        var token = CreateSut().GenerateAccessToken(user);
        var principal = ReadToken(token);

        // A comma-separated value like "Teacher,Parent" as a single claim would
        // break User.IsInRole() — verify it is never present
        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.DoesNotContain("Teacher,Parent", roleClaims);
    }

    // -------------------------------------------------------------------------
    // Permission claims
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateAccessToken_StampsPermissionClaimsForRole()
    {
        var user = MakeUser(new List<UserRole> { UserRole.Teacher });
        var expectedPermissions = RolePermissions.For(UserRole.Teacher)
            .Select(p => p.ToString())
            .ToHashSet();

        var token = CreateSut().GenerateAccessToken(user);
        var principal = ReadToken(token);

        var permissionClaims = principal.FindAll(PermissionClaimType.Type)
            .Select(c => c.Value)
            .ToHashSet();

        Assert.Equal(expectedPermissions, permissionClaims);
    }

    [Fact]
    public void GenerateAccessToken_DualRole_StampsUnionOfPermissionsWithoutDuplicates()
    {
        // Teacher and Parent both have StudentsRead — it should appear only once
        var user = MakeUser(new List<UserRole> { UserRole.Teacher, UserRole.Parent });

        var token = CreateSut().GenerateAccessToken(user);
        var principal = ReadToken(token);

        var permissionClaims = principal.FindAll(PermissionClaimType.Type)
            .Select(c => c.Value)
            .ToList();

        // No duplicates
        Assert.Equal(permissionClaims.Count, permissionClaims.Distinct().Count());

        // Contains permissions from both roles
        Assert.Contains(Permission.AttendanceMark.ToString(), permissionClaims);     // Teacher only
        Assert.Contains(Permission.StudentsRead.ToString(), permissionClaims);       // Both roles
        Assert.Contains(Permission.AttendanceViewStudent.ToString(), permissionClaims); // Both roles
    }

    [Fact]
    public void GenerateAccessToken_Admin_StampsUsersDeactivatePermission()
    {
        var user = MakeUser(new List<UserRole> { UserRole.Admin });

        var token = CreateSut().GenerateAccessToken(user);
        var principal = ReadToken(token);

        var permissions = principal.FindAll(PermissionClaimType.Type)
            .Select(c => c.Value)
            .ToHashSet();

        Assert.Contains(Permission.UsersDeactivate.ToString(), permissions);
    }

    // -------------------------------------------------------------------------
    // Standard claims
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateAccessToken_ContainsTenantIdClaim()
    {
        var user = MakeUser(new List<UserRole> { UserRole.Admin });

        var token = CreateSut().GenerateAccessToken(user);
        var principal = ReadToken(token);

        var tenantClaim = principal.FindFirst("tenant_id");
        Assert.NotNull(tenantClaim);
        Assert.Equal(user.TenantId.ToString(), tenantClaim.Value);
    }

    [Fact]
    public void GenerateAccessToken_ContainsUserIdClaim()
    {
        var user = MakeUser(new List<UserRole> { UserRole.Admin });

        var token = CreateSut().GenerateAccessToken(user);
        var principal = ReadToken(token);

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        Assert.NotNull(userIdClaim);
        Assert.Equal(user.Id.ToString(), userIdClaim.Value);
    }
}

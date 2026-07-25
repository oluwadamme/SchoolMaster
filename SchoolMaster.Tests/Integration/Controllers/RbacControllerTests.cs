using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Domain.Authorization;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Tests.Integration.Helpers;
using Xunit;

namespace SchoolMaster.Tests.Integration.Controllers;

/// <summary>
/// Verifies that the RBAC permission system is wired up end-to-end:
/// correct roles get through, wrong roles are blocked, and JWTs carry
/// the expected permission claims.
/// </summary>
[Collection("Integration")]
public class RbacControllerTests : IClassFixture<SchoolMasterWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly SchoolMasterWebApplicationFactory _factory;

    public RbacControllerTests(SchoolMasterWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<string> LoginAndGetTokenAsync(string email, string password, string subdomain)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.Add("X-Tenant-Subdomain", subdomain);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);

        return body!.Data!.Token;
    }

    private HttpRequestMessage DeactivateRequest(string emailToDeactivate, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/auth/users/deactivate-by-email")
        {
            Content = JsonContent.Create(new DeactivateUserByEmailRequest(emailToDeactivate))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    // -------------------------------------------------------------------------
    // PATCH /api/auth/users/deactivate-by-email
    // Protected by [HasPermission(Permission.UsersDeactivate)]
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeactivateByEmail_WithAdminJwt_Returns200()
    {
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Admin });

        var token = await LoginAndGetTokenAsync(email, password, subdomain);
        var response = await _client.SendAsync(DeactivateRequest(email, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateByEmail_WithTeacherJwt_Returns403()
    {
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Teacher });

        var token = await LoginAndGetTokenAsync(email, password, subdomain);
        var response = await _client.SendAsync(DeactivateRequest(email, token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateByEmail_WithParentJwt_Returns403()
    {
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Parent });

        var token = await LoginAndGetTokenAsync(email, password, subdomain);
        var response = await _client.SendAsync(DeactivateRequest(email, token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateByEmail_WithStaffJwt_Returns403()
    {
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Staff });

        var token = await LoginAndGetTokenAsync(email, password, subdomain);
        var response = await _client.SendAsync(DeactivateRequest(email, token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateByEmail_WithNoJwt_Returns401()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/v1/auth/users/deactivate-by-email",
            new DeactivateUserByEmailRequest("someone@test.com"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // JWT token structure — verify claims are stamped correctly at login
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_AdminJwt_ContainsUsersDeactivatePermissionClaim()
    {
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Admin });

        var token = await LoginAndGetTokenAsync(email, password, subdomain);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var permissions = jwt.Claims
            .Where(c => c.Type == PermissionClaimType.Type)
            .Select(c => c.Value)
            .ToHashSet();

        Assert.Contains(Permission.UsersDeactivate.ToString(), permissions);
    }

    [Fact]
    public async Task Login_TeacherJwt_ContainsAttendanceMarkPermissionClaim()
    {
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Teacher });

        var token = await LoginAndGetTokenAsync(email, password, subdomain);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var permissions = jwt.Claims
            .Where(c => c.Type == PermissionClaimType.Type)
            .Select(c => c.Value)
            .ToHashSet();

        Assert.Contains(Permission.AttendanceMark.ToString(), permissions);
        Assert.DoesNotContain(Permission.UsersDeactivate.ToString(), permissions);
    }

    [Fact]
    public async Task Login_TeacherJwt_ContainsOneRoleClaimWithValueTeacher()
    {
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Teacher });

        var token = await LoginAndGetTokenAsync(email, password, subdomain);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        // JWT stores ClaimTypes.Role as the short name "role"
        var roleClaims = jwt.Claims
            .Where(c => c.Type == "role")
            .Select(c => c.Value)
            .ToList();

        Assert.Single(roleClaims);
        Assert.Equal("Teacher", roleClaims[0]);
    }

    [Fact]
    public async Task Login_DualRoleUser_JwtContainsPermissionsFromBothRoles()
    {
        // Teacher-parent: has a child in the school
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Teacher, UserRole.Parent });

        var token = await LoginAndGetTokenAsync(email, password, subdomain);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var permissions = jwt.Claims
            .Where(c => c.Type == PermissionClaimType.Type)
            .Select(c => c.Value)
            .ToHashSet();

        // From Teacher role
        Assert.Contains(Permission.AttendanceMark.ToString(), permissions);
        // From Parent role
        Assert.Contains(Permission.StudentsPayment.ToString(), permissions);
    }

    [Fact]
    public async Task Login_DualRoleUser_JwtHasNoDuplicatePermissionClaims()
    {
        // Both Teacher and Parent have StudentsRead — it should appear only once
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Teacher, UserRole.Parent });

        var token = await LoginAndGetTokenAsync(email, password, subdomain);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var permissionClaims = jwt.Claims
            .Where(c => c.Type == PermissionClaimType.Type)
            .Select(c => c.Value)
            .ToList();

        Assert.Equal(permissionClaims.Count, permissionClaims.Distinct().Count());
    }
}

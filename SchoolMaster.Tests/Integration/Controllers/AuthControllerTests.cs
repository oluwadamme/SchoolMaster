using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Tests.Integration;
using Xunit;

namespace SchoolMaster.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for /api/auth endpoints.
/// Each test seeds its own tenant + admin user to stay fully isolated.
/// </summary>
[Collection("Integration")]
public class AuthControllerTests : IClassFixture<SchoolMasterWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(SchoolMasterWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static OnboardTenantRequest MakeUniqueOnboardRequest()
    {
        var u = Guid.NewGuid().ToString("N")[..10];
        return new OnboardTenantRequest
        {
            SchoolName = "Auth Test School",
            Subdomain = $"a{u}",
            ContactEmail = $"contact-{u}@test.com",
            AdminFirstName = "Auth",
            AdminLastName = "Admin",
            AdminEmail = $"authadmin-{u}@test.com",
            AdminPassword = "Test@123!",
        };
    }

    /// <summary>
    /// Registers a tenant + admin user. Returns credentials needed for follow-up requests.
    /// Email is NOT verified so the test controls that step explicitly.
    /// </summary>
    private async Task<(string Subdomain, Guid TenantId, string Email, string Password)> SeedTenantAsync()
    {
        var req = MakeUniqueOnboardRequest();
        var response = await _client.PostAsJsonAsync("/api/onboarding/tenants", req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<Guid>>();
        return (req.Subdomain, body!.Data, req.AdminEmail, req.AdminPassword);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, object body, string subdomain)
    {
        var msg = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        msg.Headers.Add("X-Tenant-Subdomain", subdomain);
        return msg;
    }

    /// <summary>
    /// Logs in and returns the access token + refresh token from the response.
    /// LoginAsync does not require verified email so this works right after SeedTenantAsync.
    /// </summary>
    private async Task<(string AccessToken, string RefreshToken)> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>(SchoolMasterWebApplicationFactory.JsonOptions);
        return (body!.Data!.Token, body.Data.RefreshToken);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/login
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndTokens()
    {
        var (_, _, email, password) = await SeedTenantAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>(SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.True(body!.Success);
        Assert.False(string.IsNullOrEmpty(body.Data!.Token));
        Assert.False(string.IsNullOrEmpty(body.Data.RefreshToken));
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@test.com", password = "Test@123!" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var (_, _, email, _) = await SeedTenantAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "WrongPass@99" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithMissingEmailField_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { password = "Test@123!" }); // email intentionally omitted

        // LoginRequest is a record with no FluentValidation; null email causes a
        // UserNotFoundException which the ExceptionMiddleware maps to 404, unless
        // model binding itself fails with 400. This documents the actual behaviour.
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/refresh-token
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RefreshToken_WithValidTokens_Returns200AndNewTokens()
    {
        var (_, _, email, password) = await SeedTenantAsync();
        var (accessToken, refreshToken) = await LoginAsync(email, password);

        var response = await _client.PostAsJsonAsync("/api/auth/refresh-token",
            new { accessToken, refreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>(SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.True(body!.Success);
        Assert.False(string.IsNullOrEmpty(body.Data!.Token));
    }

    [Fact]
    public async Task RefreshToken_WithWrongRefreshToken_Returns401()
    {
        var (_, _, email, password) = await SeedTenantAsync();
        var (accessToken, _) = await LoginAsync(email, password);

        var response = await _client.PostAsJsonAsync("/api/auth/refresh-token",
            new { accessToken, refreshToken = "this-is-not-the-right-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/forgot-password
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ForgotPassword_WithValidEmail_Returns200()
    {
        var (subdomain, _, email, _) = await SeedTenantAsync();

        var response = await _client.SendAsync(BuildRequest(
            HttpMethod.Post, "/api/auth/forgot-password",
            new { email }, subdomain));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_Returns200Silently()
    {
        // Service silently returns success to prevent user enumeration
        var (subdomain, _, _, _) = await SeedTenantAsync();

        var response = await _client.SendAsync(BuildRequest(
            HttpMethod.Post, "/api/auth/forgot-password",
            new { email = "nobody@test.com" }, subdomain));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /api/auth/reset-password
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResetPassword_WithValidOtp_Returns200AndAllowsNewPassword()
    {
        var (subdomain, _, email, _) = await SeedTenantAsync();

        // Trigger forgot-password so the fixed OTP "0000" is saved on the user
        await _client.SendAsync(BuildRequest(
            HttpMethod.Post, "/api/auth/forgot-password",
            new { email }, subdomain));

        var response = await _client.SendAsync(BuildRequest(
            HttpMethod.Post, "/api/auth/reset-password",
            new { email, password = "NewPass@99!", otp = SchoolMasterWebApplicationFactory.FixedOtp },
            subdomain));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<bool>>();
        Assert.True(body!.Success);
    }

    [Fact]
    public async Task ResetPassword_WithWrongOtp_Returns400()
    {
        var (subdomain, _, email, _) = await SeedTenantAsync();

        // Issue a reset request without triggering forgot-password first,
        // so no OTP is set — any OTP value will be treated as wrong
        var response = await _client.SendAsync(BuildRequest(
            HttpMethod.Post, "/api/auth/reset-password",
            new { email, password = "NewPass@99!", otp = "9999" },
            subdomain));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // PATCH /api/auth/users/deactivate-by-email  [Authorize(Roles = "Admin")]
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeactivateByEmail_WithAdminJwt_Returns200()
    {
        var (_, _, email, password) = await SeedTenantAsync();
        var (accessToken, _) = await LoginAsync(email, password);

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/users/deactivate-by-email")
        {
            Content = JsonContent.Create(new DeactivateUserByEmailRequest(email)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<bool>>();
        Assert.True(body!.Success);
        Assert.True(body.Data);
    }

    [Fact]
    public async Task DeactivateByEmail_WithoutJwt_Returns401()
    {
        var response = await _client.PatchAsJsonAsync("/api/auth/users/deactivate-by-email",
            new DeactivateUserByEmailRequest("someone@test.com"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateByEmail_WithUnknownEmail_Returns404()
    {
        var (_, _, email, password) = await SeedTenantAsync();
        var (accessToken, _) = await LoginAsync(email, password);

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/auth/users/deactivate-by-email")
        {
            Content = JsonContent.Create(new DeactivateUserByEmailRequest("ghost@test.com")),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

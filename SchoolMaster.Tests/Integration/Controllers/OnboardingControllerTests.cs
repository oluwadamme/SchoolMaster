using System.Net;
using System.Net.Http.Json;
using SchoolMaster.Application.DTOs;
using Xunit;

namespace SchoolMaster.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for /api/v1/onboarding endpoints.
/// Each test seeds its own unique tenant so tests run independently.
/// </summary>
[Collection("Integration")]
public class OnboardingControllerTests : IClassFixture<SchoolMasterWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OnboardingControllerTests(SchoolMasterWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static OnboardTenantRequest MakeUniqueRequest()
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        return new OnboardTenantRequest
        {
            SchoolName = "Test School",
            Subdomain = $"s{unique}",
            ContactEmail = $"contact-{unique}@test.com",
            AdminFirstName = "Admin",
            AdminLastName = "User",
            AdminEmail = $"admin-{unique}@test.com",
            AdminPassword = "Test@123!",
            SchoolCode = "SCH",
        };
    }

    /// <summary>
    /// Creates a tenant via HTTP and returns subdomain + tenantId + adminEmail.
    /// The fixed OTP "0000" will be stored on the user by FixedOtpService.
    /// </summary>
    private async Task<(string Subdomain, Guid TenantId, string AdminEmail)> SeedTenantAsync()
    {
        var req = MakeUniqueRequest();
        var response = await _client.PostAsJsonAsync("/api/v1/onboarding/tenants", req);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<BaseResponse<Guid>>();
        return (req.Subdomain, body!.Data, req.AdminEmail);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, object body, string subdomain)
    {
        var message = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        message.Headers.Add("X-Tenant-Subdomain", subdomain);
        return message;
    }

    // -------------------------------------------------------------------------
    // POST /api/v1/onboarding/tenants
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RegisterTenant_WithValidRequest_Returns201AndTenantId()
    {
        var req = MakeUniqueRequest();

        var response = await _client.PostAsJsonAsync("/api/v1/onboarding/tenants", req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<Guid>>();
        Assert.True(body!.Success);
        Assert.NotEqual(Guid.Empty, body.Data);
    }

    [Fact]
    public async Task RegisterTenant_WithDuplicateAdminEmail_Returns409()
    {
        var req = MakeUniqueRequest();
        await _client.PostAsJsonAsync("/api/v1/onboarding/tenants", req); // first registration

        // Same adminEmail but different subdomain
        var uniquePart = Guid.NewGuid().ToString("N")[..10];
        var req2 = new OnboardTenantRequest
        {
            SchoolName = req.SchoolName,
            Subdomain = $"s{uniquePart}",
            ContactEmail = $"contact-{uniquePart}@test.com",
            AdminFirstName = req.AdminFirstName,
            AdminLastName = req.AdminLastName,
            AdminEmail = req.AdminEmail, // same email — causes conflict
            AdminPassword = req.AdminPassword,
            SchoolCode = "SCH",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/onboarding/tenants", req2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterTenant_WithDuplicateSubdomain_Returns409()
    {
        var req = MakeUniqueRequest();
        await _client.PostAsJsonAsync("/api/v1/onboarding/tenants", req); // first registration

        var uniquePart = Guid.NewGuid().ToString("N")[..10];
        var req2 = new OnboardTenantRequest
        {
            SchoolName = req.SchoolName,
            Subdomain = req.Subdomain, // same subdomain — causes conflict
            ContactEmail = $"contact-{uniquePart}@test.com",
            AdminFirstName = req.AdminFirstName,
            AdminLastName = req.AdminLastName,
            AdminEmail = $"admin-{uniquePart}@test.com",
            AdminPassword = req.AdminPassword,
            SchoolCode = "SCH",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/onboarding/tenants", req2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterTenant_WithMissingRequiredFields_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/onboarding/tenants", new
        {
            // schoolName, subdomain, and password deliberately omitted
            adminEmail = "bad",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterTenant_WithInvalidPasswordFormat_Returns400()
    {
        var base2 = MakeUniqueRequest();
        var req = new OnboardTenantRequest
        {
            SchoolName = base2.SchoolName, Subdomain = base2.Subdomain,
            ContactEmail = base2.ContactEmail, AdminFirstName = base2.AdminFirstName,
            AdminLastName = base2.AdminLastName, AdminEmail = base2.AdminEmail,
            AdminPassword = "weak", // fails complexity rules
            SchoolCode = "SCH",
        };

        var response = await _client.PostAsJsonAsync("/api/v1/onboarding/tenants", req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /api/v1/onboarding/verify-email
    // -------------------------------------------------------------------------

    [Fact]
    public async Task VerifyEmail_WithValidOtp_Returns200AndVerifiesEmail()
    {
        var (subdomain, tenantId, adminEmail) = await SeedTenantAsync();

        var request = BuildRequest(HttpMethod.Post, "/api/v1/onboarding/verify-email",
            new VerifyUserEmailRequest { Email = adminEmail, OtpToken = SchoolMasterWebApplicationFactory.FixedOtp},
            subdomain);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<bool>>();
        Assert.True(body!.Success);
        Assert.True(body.Data);
    }

    [Fact]
    public async Task VerifyEmail_WithWrongOtp_Returns400()
    {
        var (subdomain, tenantId, adminEmail) = await SeedTenantAsync();

        var request = BuildRequest(HttpMethod.Post, "/api/v1/onboarding/verify-email",
            new VerifyUserEmailRequest { Email = adminEmail, OtpToken = "9999" },
            subdomain);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_WithoutTenantHeader_Returns400()
    {
        var (_, tenantId, adminEmail) = await SeedTenantAsync();

        // No X-Tenant-Subdomain header — ICurrentTenant.Id will be Guid.Empty
        var response = await _client.PostAsJsonAsync("/api/v1/onboarding/verify-email",
            new VerifyUserEmailRequest { Email = adminEmail, OtpToken = SchoolMasterWebApplicationFactory.FixedOtp});

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // POST /api/v1/onboarding/resend-verification-otp
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResendOtp_WithValidUnverifiedEmail_Returns200()
    {
        var (subdomain, _, adminEmail) = await SeedTenantAsync();

        var request = BuildRequest(HttpMethod.Post, "/api/v1/onboarding/resend-verification-otp",
            new ResendOtpRequest { Email = adminEmail },
            subdomain);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResendOtp_WithUnknownEmail_Returns200Silently()
    {
        var (subdomain, _, _) = await SeedTenantAsync();

        // Service silently succeeds to avoid user enumeration
        var request = BuildRequest(HttpMethod.Post, "/api/v1/onboarding/resend-verification-otp",
            new ResendOtpRequest { Email = "nobody@test.com" },
            subdomain);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResendOtp_WithInvalidEmailFormat_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/onboarding/resend-verification-otp",
            new ResendOtpRequest { Email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

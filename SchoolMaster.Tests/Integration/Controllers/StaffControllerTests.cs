using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Infrastructure.Persistence;
using Xunit;
using SchoolMaster.Tests.Integration.Helpers;

namespace SchoolMaster.Tests.Integration;

// Integration tests are tests that check how well parts of an API talk to each other. Here we are testing if the HasPermissionattribute succesfully triggers the HasPermission Handler 

// This class uses the "Laboratory" we built (the Factory)
public class StaffControllerTests : IClassFixture<SchoolMasterWebApplicationFactory>
{
    private readonly SchoolMasterWebApplicationFactory _factory;

    // This tool acts like a web browser that we can use to send real HTTP requests to our API, just like Postman or a frontend would.
    private readonly HttpClient _client;

    public StaffControllerTests(SchoolMasterWebApplicationFactory factory)
    {
        _factory = factory;
        // creates your client and wires it into your factory(API copy)
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateStaff_WhenUserIsAdmin_ReturnsCreated()
    {
        // 1. Arrange: Prepare the building (The Tenant)
        var tenantId = Guid.NewGuid();
        var subdomain = "ghana-high";
        
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SchoolMasterContext>();
            context.Tenants.Add(new Tenant 
            { 
                Id = tenantId, 
                Name = "Ghana High", 
                Subdomain = subdomain, 
                SchoolCode = "GHA",
                Status = TenantStatus.Active,
                ContactEmail = "contact@gha.edu",
                Plan = TenantPlan.Free
            });
            await context.SaveChangesAsync();
        }

        // 2. Arrange: Create Susan's ID Badge (JWT)
        // We need a real token with "StaffManage" and "IsEmailVerified" permissions
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SchoolMasterContext>();
            var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FirstName = "Susan",
                LastName = "Admin",
                Email = "susan@gha.edu",
                PasswordHash = "TestPasswordHash",
                Roles = new List<UserRole> { UserRole.Admin },
                Status = UserStatus.Active,   // must be Active — OnTokenValidated rejects non-active users
                IsEmailVerified = true // Critical for our [HasPermission] check!
            };
            // Persist the admin so the token's security stamp can be validated against the stored user
            // on every request (session-revocation check added in OnTokenValidated).
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
            token = jwtService.GenerateAccessToken(adminUser);
        }

        // 3. Arrange: Prepare the Request
        var request = new CreateStaffRequest(
            FirstName: "James",
            LastName: "Teacher",
            Email: "james@gha.edu",
            Password: "SecurePassword123!",
            Department: "Science",
            StaffRole: StaffRole.Teacher,
            EmploymentType: EmploymentType.FullTime
        );

        // Put the headers on the "Test Client" (Subdomain and ID Badge) used in authorization
        _client.DefaultRequestHeaders.Add("X-Tenant-Subdomain", subdomain);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 4. Act: Send the Request to the API
        // PostAsJsonAsync: This tells the Browser tool to send a POST message (which means "Create something new").
        //  response is what the API sends back after it's done
        var response = await _client.PostAsJsonAsync("/api/v1/staff", request, SchoolMasterWebApplicationFactory.JsonOptions);

        // 5. Assert: Check the Result
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // ReadFromJsonAsync: This is the "Translator." It reads the JSON text from the response.
        // <BaseResponse<StaffResponse>>: This tells the translator: "I want you to build a C# object that looks exactly like my BaseResponse class."
        // JsonOptions: This is the "Dictionary." It tells the translator how to handle special words (like Enums) so that the text "Teacher" becomes the C# value StaffRole.Teacher
        
        var result = await response.Content.ReadFromJsonAsync<BaseResponse<StaffResponse>>(SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("James", result.Data?.FirstName);
        
        // Check if the Staff Number was generated correctly (GHA/STF/Year/000001)
        Assert.Contains("GHA/STF/", result.Data?.StaffNumber);
    }

    [Fact]
    public async Task CreateStaff_WhenUserIsTeacher_Returns403Forbidden()
    {
        // 1. Arrange: Create a Teacher in the database
        // We use a helper called UserSeeder to quickly put a Teacher in a school. We don't create and add the staff (teacher) manually because we are testing staff creation in the staff controller so we need to send a a request to the API endpoint
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, new List<UserRole> { UserRole.Teacher });

        // 2. Arrange: Get the Teacher's ID Badge (JWT)
        // We "Login" as the teacher to get a real token.
        // c# turns this anonymous object i.e no specific class into json
        var loginRequest = new { email, password };
        var loginMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(loginRequest)
        };
        loginMsg.Headers.Add("X-Tenant-Subdomain", subdomain);
        
        var loginResponse = await _client.SendAsync(loginMsg);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>(SchoolMasterWebApplicationFactory.JsonOptions);
        var token = loginBody!.Data!.Token;

        // 3. Act: Try to enter the Staff Room (The Controller)
        // We send a request to create staff, but we use the Teacher's badge.
        var request = new CreateStaffRequest("Any", "Body", "any@test.com", "Pass123!", "Math", StaffRole.Teacher, EmploymentType.FullTime);
        
        var apiRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff") { Content = JsonContent.Create(request) };
        apiRequest.Headers.Add("X-Tenant-Subdomain", subdomain);
        apiRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(apiRequest);

        // 4. Assert: The Security Guard should block the Teacher.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
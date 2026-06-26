using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Tests.Integration;
using SchoolMaster.Tests.Integration.Helpers;
using Xunit;

namespace SchoolMaster.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for /api/v1/attendance endpoints.
/// Academic setup (year, term, class) is created through the Academic API with an Admin role,
/// students are seeded directly (no creation endpoint yet), and marking uses the Teacher role.
/// A single user holds both roles so all of this happens inside one tenant.
/// </summary>
[Collection("Integration")]
public class AttendanceControllerTests : IClassFixture<SchoolMasterWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly SchoolMasterWebApplicationFactory _factory;

    public AttendanceControllerTests(SchoolMasterWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Auth / request helpers ────────────────────────────────────────────────

    private async Task<(string Subdomain, string Token)> SeedAdminTeacherAsync()
    {
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, [UserRole.Admin, UserRole.Teacher]);
        var token = await LoginAsync(email, password, subdomain);
        return (subdomain, token);
    }

    private async Task<string> LoginAsync(string email, string password, string subdomain)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password })
        };
        msg.Headers.Add("X-Tenant-Subdomain", subdomain);
        var response = await _client.SendAsync(msg);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        return body!.Data!.Token;
    }

    private HttpRequestMessage Build(HttpMethod method, string url, object? body,
        string subdomain, string token)
    {
        var msg = new HttpRequestMessage(method, url);
        if (body != null)
            msg.Content = JsonContent.Create(body);
        msg.Headers.Add("X-Tenant-Subdomain", subdomain);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return msg;
    }

    private async Task<BaseResponse<T>> Send<T>(HttpRequestMessage msg)
    {
        var response = await _client.SendAsync(msg);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BaseResponse<T>>(
            SchoolMasterWebApplicationFactory.JsonOptions))!;
    }

    // ── Academic setup helpers (Admin role) ───────────────────────────────────

    private async Task<AcademicYearResponse> CreateYearAsync(string subdomain, string token) =>
        (await Send<AcademicYearResponse>(Build(HttpMethod.Post, "/api/v1/academic/years",
            new { name = "2025/2026", startDate = "2025-09-01", endDate = "2026-07-31", setAsCurrent = true },
            subdomain, token))).Data!;

    private async Task<TermResponse> CreateTermAsync(string subdomain, string token, Guid yearId) =>
        (await Send<TermResponse>(Build(HttpMethod.Post, "/api/v1/academic/terms",
            new { academicYearId = yearId, name = "First Term", termNumber = 1,
                  startDate = "2025-09-01", endDate = "2025-12-20", setAsCurrent = true },
            subdomain, token))).Data!;

    private async Task<ClassResponse> CreateClassAsync(string subdomain, string token, string name = "JSS 1A") =>
        (await Send<ClassResponse>(Build(HttpMethod.Post, "/api/v1/academic/classes",
            new { name, formTeacherId = (Guid?)null }, subdomain, token))).Data!;

    /// <summary>Full happy-path setup: current year + current term + class + two enrolled students.</summary>
    private async Task<(string Subdomain, string Token, Guid ClassId, Guid StudentA, Guid StudentB)>
        SetupMarkableClassAsync(string className = "JSS 1A")
    {
        var (subdomain, token) = await SeedAdminTeacherAsync();
        var year = await CreateYearAsync(subdomain, token);
        await CreateTermAsync(subdomain, token, year.Id);
        var cls = await CreateClassAsync(subdomain, token, className);
        var studentA = await StudentSeeder.SeedAsync(_factory.Services, cls.Id);
        var studentB = await StudentSeeder.SeedAsync(_factory.Services, cls.Id);
        return (subdomain, token, cls.Id, studentA, studentB);
    }

    // Dates relative to runtime so the future-date validator behaves deterministically.
    private static string Days(int offset) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offset).ToString("yyyy-MM-dd");

    // ── Auth enforcement ──────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAttendance_WithoutJwt_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/attendance",
            new { classId = Guid.NewGuid(), date = Days(-1), records = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MarkAttendance_AsUserWithoutMarkPermission_Returns403()
    {
        // Admin can manage academics but does NOT hold AttendanceMark (Teacher-only).
        var (subdomain, email, password) = await UserSeeder.SeedAsync(_factory.Services, [UserRole.Admin]);
        var token = await LoginAsync(email, password, subdomain);

        var msg = Build(HttpMethod.Post, "/api/v1/attendance",
            new { classId = Guid.NewGuid(), date = Days(-1),
                  records = new[] { new { studentId = Guid.NewGuid(), status = "Present", notes = (string?)null } } },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Mark attendance ───────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAttendance_WithValidRequest_Returns200AndStatusBreakdown()
    {
        var (subdomain, token, classId, studentA, studentB) = await SetupMarkableClassAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/attendance",
            new
            {
                classId,
                date = Days(-1),
                records = new[]
                {
                    new { studentId = studentA, status = "Present", notes = (string?)null },
                    new { studentId = studentB, status = "Absent",  notes = "No call from guardian" }
                }
            }, subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<MarkAttendanceResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal(2, body!.Data!.TotalMarked);
        Assert.Equal(1, body.Data.Present);
        Assert.Equal(1, body.Data.Absent);
        Assert.Equal(1, body.Data.NotificationsQueued);
    }

    [Fact]
    public async Task MarkAttendance_WithNoActiveAcademicYear_Returns404()
    {
        // Teacher present (passes auth) but no academic year/term was created.
        var (subdomain, token) = await SeedAdminTeacherAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/attendance",
            new { classId = Guid.NewGuid(), date = Days(-1),
                  records = new[] { new { studentId = Guid.NewGuid(), status = "Present", notes = (string?)null } } },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MarkAttendance_WithFutureDate_Returns400()
    {
        var (subdomain, token, classId, studentA, _) = await SetupMarkableClassAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/attendance",
            new { classId, date = Days(5),
                  records = new[] { new { studentId = studentA, status = "Present", notes = (string?)null } } },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MarkAttendance_WithEmptyRecords_Returns400()
    {
        var (subdomain, token, classId, _, _) = await SetupMarkableClassAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/attendance",
            new { classId, date = Days(-1), records = Array.Empty<object>() },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MarkAttendance_WithStudentNotInClass_Returns422()
    {
        var (subdomain, token, classId, _, _) = await SetupMarkableClassAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/attendance",
            new { classId, date = Days(-1),
                  records = new[] { new { studentId = Guid.NewGuid(), status = "Present", notes = (string?)null } } },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task MarkAttendance_ResubmittingSameDay_UpsertsCorrectedStatus()
    {
        var (subdomain, token, classId, studentA, _) = await SetupMarkableClassAsync();
        var date = Days(-1);

        // First mark: Absent
        await _client.SendAsync(Build(HttpMethod.Post, "/api/v1/attendance",
            new { classId, date,
                  records = new[] { new { studentId = studentA, status = "Absent", notes = (string?)null } } },
            subdomain, token));

        // Correction: Present
        await _client.SendAsync(Build(HttpMethod.Post, "/api/v1/attendance",
            new { classId, date,
                  records = new[] { new { studentId = studentA, status = "Present", notes = "Corrected" } } },
            subdomain, token));

        // Read back — exactly one record, now Present (not duplicated)
        var read = await _client.SendAsync(
            Build(HttpMethod.Get, $"/api/v1/attendance/class/{classId}?date={date}", null, subdomain, token));
        var body = await read.Content.ReadFromJsonAsync<BaseResponse<ClassAttendanceResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);

        var record = Assert.Single(body!.Data!.Records);
        Assert.Equal(AttendanceStatus.Present, record.Status);
    }

    // ── Read: class attendance ────────────────────────────────────────────────

    [Fact]
    public async Task GetClassAttendance_ReturnsMarkedRecords()
    {
        var (subdomain, token, classId, studentA, studentB) = await SetupMarkableClassAsync();
        var date = Days(-1);

        await _client.SendAsync(Build(HttpMethod.Post, "/api/v1/attendance",
            new
            {
                classId, date,
                records = new[]
                {
                    new { studentId = studentA, status = "Present", notes = (string?)null },
                    new { studentId = studentB, status = "Late",    notes = (string?)null }
                }
            }, subdomain, token));

        var response = await _client.SendAsync(
            Build(HttpMethod.Get, $"/api/v1/attendance/class/{classId}?date={date}", null, subdomain, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<ClassAttendanceResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal(2, body!.Data!.Records.Count);
    }

    [Fact]
    public async Task GetClassAttendance_ForAnotherTenantClass_Returns404()
    {
        // Tenant 1 owns the class
        var (_, _, classId, _, _) = await SetupMarkableClassAsync();

        // Tenant 2 tries to read it by id
        var (subdomain2, token2) = await SeedAdminTeacherAsync();
        var response = await _client.SendAsync(
            Build(HttpMethod.Get, $"/api/v1/attendance/class/{classId}?date={Days(-1)}", null, subdomain2, token2));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Read: student summary ─────────────────────────────────────────────────

    [Fact]
    public async Task GetStudentSummary_ReturnsCountsAndAttendancePercentage()
    {
        var (subdomain, token, classId, studentA, _) = await SetupMarkableClassAsync();

        // Two days: Present then Absent → 1 of 2 attended → 50%
        await _client.SendAsync(Build(HttpMethod.Post, "/api/v1/attendance",
            new { classId, date = Days(-1),
                  records = new[] { new { studentId = studentA, status = "Present", notes = (string?)null } } },
            subdomain, token));
        await _client.SendAsync(Build(HttpMethod.Post, "/api/v1/attendance",
            new { classId, date = Days(-2),
                  records = new[] { new { studentId = studentA, status = "Absent", notes = (string?)null } } },
            subdomain, token));

        var response = await _client.SendAsync(
            Build(HttpMethod.Get, $"/api/v1/attendance/student/{studentA}", null, subdomain, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<StudentAttendanceSummaryResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal(2, body!.Data!.TotalDays);
        Assert.Equal(1, body.Data.PresentDays);
        Assert.Equal(1, body.Data.AbsentDays);
        Assert.Equal(50, body.Data.AttendancePercentage);
    }

    [Fact]
    public async Task GetStudentSummary_UnknownStudent_Returns404()
    {
        var (subdomain, token) = await SeedAdminTeacherAsync();

        var response = await _client.SendAsync(
            Build(HttpMethod.Get, $"/api/v1/attendance/student/{Guid.NewGuid()}", null, subdomain, token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStudentSummary_WithInvertedDateRange_Returns400()
    {
        var (subdomain, token, _, studentA, _) = await SetupMarkableClassAsync();

        var response = await _client.SendAsync(Build(HttpMethod.Get,
            $"/api/v1/attendance/student/{studentA}?from={Days(-1)}&to={Days(-5)}", null, subdomain, token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

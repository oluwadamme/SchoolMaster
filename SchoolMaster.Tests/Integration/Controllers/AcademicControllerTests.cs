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
/// Integration tests for /api/v1/academic endpoints.
/// Each test class gets its own Testcontainers PostgreSQL instance via IClassFixture.
/// Data is seeded through the API (not direct DB writes) so create endpoints are
/// exercised as part of the test setup.
/// </summary>
[Collection("Integration")]
public class AcademicControllerTests : IClassFixture<SchoolMasterWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly SchoolMasterWebApplicationFactory _factory;

    public AcademicControllerTests(SchoolMasterWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    private async Task<(string Subdomain, string Token)> SeedAdminAsync()
    {
        var (subdomain, email, password) = await UserSeeder.SeedAsync(
            _factory.Services, [UserRole.Admin]);
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

    // Convenience wrappers that create entities via the API

    private async Task<AcademicYearResponse> CreateYearAsync(
        string subdomain, string token,
        string name = "2025/2026",
        string startDate = "2025-09-01",
        string endDate = "2026-07-31",
        bool setAsCurrent = true)
    {
        var body = await Send<AcademicYearResponse>(Build(
            HttpMethod.Post, "/api/v1/academic/years",
            new { name, startDate, endDate, setAsCurrent },
            subdomain, token));
        return body.Data!;
    }

    private async Task<TermResponse> CreateTermAsync(
        string subdomain, string token, Guid academicYearId,
        string name = "First Term", int termNumber = 1,
        string startDate = "2025-09-01", string endDate = "2025-12-20",
        bool setAsCurrent = true)
    {
        var body = await Send<TermResponse>(Build(
            HttpMethod.Post, "/api/v1/academic/terms",
            new { academicYearId, name, termNumber, startDate, endDate, setAsCurrent },
            subdomain, token));
        return body.Data!;
    }

    private async Task<ClassResponse> CreateClassAsync(
        string subdomain, string token, string name = "JSS 1A")
    {
        var body = await Send<ClassResponse>(Build(
            HttpMethod.Post, "/api/v1/academic/classes",
            new { name, formTeacherId = (Guid?)null },
            subdomain, token));
        return body.Data!;
    }

    private async Task<SubjectResponse> CreateSubjectAsync(
        string subdomain, string token,
        string name = "Mathematics", string? code = "MATH")
    {
        var body = await Send<SubjectResponse>(Build(
            HttpMethod.Post, "/api/v1/academic/subjects",
            new { name, code },
            subdomain, token));
        return body.Data!;
    }

    private async Task<PeriodResponse> CreatePeriodAsync(
        string subdomain, string token, Guid classId,
        Guid? subjectId = null,
        string dayOfWeek = "Monday",
        string startTime = "08:00:00",
        string endTime = "09:00:00",
        string type = "Timetabled",
        string? name = null,
        Guid? teacherId = null)
    {
        // Validator requires TeacherId to be non-null for Timetabled periods.
        // Tests that don't care about the actual teacher pass a placeholder Guid.
        var effectiveTeacherId = type == "Timetabled" ? (teacherId ?? Guid.NewGuid()) : teacherId;
        var body = await Send<PeriodResponse>(Build(
            HttpMethod.Post, $"/api/v1/academic/classes/{classId}/periods",
            new { subjectId, teacherId = effectiveTeacherId, dayOfWeek, startTime, endTime, type, name },
            subdomain, token));
        return body.Data!;
    }

    // ── Auth enforcement ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("POST",  "/api/v1/academic/years")]
    [InlineData("GET",   "/api/v1/academic/years")]
    [InlineData("POST",  "/api/v1/academic/terms")]
    [InlineData("POST",  "/api/v1/academic/classes")]
    [InlineData("GET",   "/api/v1/academic/classes")]
    [InlineData("POST",  "/api/v1/academic/subjects")]
    [InlineData("GET",   "/api/v1/academic/subjects")]
    public async Task AcademicEndpoint_WithoutJwt_Returns401(string method, string url)
    {
        var msg = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await _client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Academic Years ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAcademicYear_WithValidRequest_Returns201AndResponseBody()
    {
        var (subdomain, token) = await SeedAdminAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/academic/years",
            new { name = "2025/2026", startDate = "2025-09-01", endDate = "2026-07-31", setAsCurrent = true },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<AcademicYearResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.True(body!.Success);
        Assert.Equal("2025/2026", body.Data!.Name);
        Assert.True(body.Data.IsCurrent);
    }

    [Fact]
    public async Task CreateAcademicYear_WithDuplicateName_Returns409()
    {
        var (subdomain, token) = await SeedAdminAsync();
        await CreateYearAsync(subdomain, token, "2025/2026");

        var msg = Build(HttpMethod.Post, "/api/v1/academic/years",
            new { name = "2025/2026", startDate = "2025-09-01", endDate = "2026-07-31", setAsCurrent = false },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetAcademicYears_ReturnsPagedListWithCreatedYear()
    {
        var (subdomain, token) = await SeedAdminAsync();
        await CreateYearAsync(subdomain, token);

        var response = await _client.SendAsync(
            Build(HttpMethod.Get, "/api/v1/academic/years?page=1&pageSize=20", null, subdomain, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<PagedResponse<AcademicYearResponse>>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.True(body!.Success);
        Assert.NotEmpty(body.Data!.Items);
        Assert.Equal(1, body.Data.Page);
    }

    [Fact]
    public async Task UpdateAcademicYear_ChangingNameAndDates_Returns200WithUpdatedValues()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var year = await CreateYearAsync(subdomain, token);

        var msg = Build(HttpMethod.Patch, $"/api/v1/academic/years/{year.Id}",
            new { name = "2025/2026 Revised", endDate = "2026-08-15" },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<AcademicYearResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal("2025/2026 Revised", body!.Data!.Name);
        Assert.Equal(new DateOnly(2026, 8, 15), body.Data.EndDate);
    }

    [Fact]
    public async Task UpdateAcademicYear_OmittedFieldsArePreserved()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var year = await CreateYearAsync(subdomain, token, "2025/2026");

        // Only update setAsCurrent — name and dates should be unchanged
        var msg = Build(HttpMethod.Patch, $"/api/v1/academic/years/{year.Id}",
            new { setAsCurrent = true },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<AcademicYearResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal("2025/2026", body!.Data!.Name);
    }

    [Fact]
    public async Task UpdateAcademicYear_NotFound_Returns404()
    {
        var (subdomain, token) = await SeedAdminAsync();

        var msg = Build(HttpMethod.Patch, $"/api/v1/academic/years/{Guid.NewGuid()}",
            new { name = "Updated" }, subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Terms ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTerm_WithValidDates_Returns201()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var year = await CreateYearAsync(subdomain, token);

        var msg = Build(HttpMethod.Post, "/api/v1/academic/terms",
            new { academicYearId = year.Id, name = "First Term", termNumber = 1,
                  startDate = "2025-09-01", endDate = "2025-12-20", setAsCurrent = true },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<TermResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal("First Term", body!.Data!.Name);
        Assert.Equal(year.Id, body.Data.AcademicYearId);
    }

    [Fact]
    public async Task CreateTerm_WithDatesOutsideYearWindow_Returns422()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var year = await CreateYearAsync(subdomain, token);

        var msg = Build(HttpMethod.Post, "/api/v1/academic/terms",
            new { academicYearId = year.Id, name = "Bad Term", termNumber = 1,
                  startDate = "2025-09-01", endDate = "2027-01-01", setAsCurrent = false },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateTerm_WithOverlappingDates_Returns422()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var year = await CreateYearAsync(subdomain, token);
        await CreateTermAsync(subdomain, token, year.Id,
            startDate: "2025-09-01", endDate: "2025-12-20");

        // Second term overlaps with first
        var msg = Build(HttpMethod.Post, "/api/v1/academic/terms",
            new { academicYearId = year.Id, name = "Second Term", termNumber = 2,
                  startDate = "2025-11-01", endDate = "2026-03-31", setAsCurrent = false },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetTermsByYear_ReturnsAllTermsForYear()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var year = await CreateYearAsync(subdomain, token);
        await CreateTermAsync(subdomain, token, year.Id, "First Term", 1,
            "2025-09-01", "2025-12-20");
        await CreateTermAsync(subdomain, token, year.Id, "Second Term", 2,
            "2026-01-08", "2026-04-30", false);

        var response = await _client.SendAsync(
            Build(HttpMethod.Get, $"/api/v1/academic/years/{year.Id}/terms", null, subdomain, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<List<TermResponse>>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal(2, body!.Data!.Count);
    }

    [Fact]
    public async Task UpdateTerm_ExtendingEndDate_Returns200WithNewDate()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var year = await CreateYearAsync(subdomain, token);
        var term = await CreateTermAsync(subdomain, token, year.Id);

        var msg = Build(HttpMethod.Patch, $"/api/v1/academic/terms/{term.Id}",
            new { endDate = "2025-12-31" },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<TermResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal(new DateOnly(2025, 12, 31), body!.Data!.EndDate);
    }

    // ── Classes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateClass_WithValidRequest_Returns201()
    {
        var (subdomain, token) = await SeedAdminAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/academic/classes",
            new { name = "JSS 1A", formTeacherId = (Guid?)null },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<ClassResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal("JSS 1A", body!.Data!.Name);
        Assert.Null(body.Data.FormTeacherId);
    }

    [Fact]
    public async Task CreateClass_WithDuplicateName_Returns409()
    {
        var (subdomain, token) = await SeedAdminAsync();
        await CreateClassAsync(subdomain, token, "JSS 1A");

        var msg = Build(HttpMethod.Post, "/api/v1/academic/classes",
            new { name = "JSS 1A" }, subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetClasses_ReturnsPaginatedList()
    {
        var (subdomain, token) = await SeedAdminAsync();
        await CreateClassAsync(subdomain, token, "JSS 2A");

        var response = await _client.SendAsync(
            Build(HttpMethod.Get, "/api/v1/academic/classes?page=1&pageSize=20", null, subdomain, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<PagedResponse<ClassResponse>>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.NotEmpty(body!.Data!.Items);
    }

    [Fact]
    public async Task UpdateClass_RenamingClass_Returns200WithNewName()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token, "JSS 1A");

        var msg = Build(HttpMethod.Patch, $"/api/v1/academic/classes/{cls.Id}",
            new { name = "JSS 1B" }, subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<ClassResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal("JSS 1B", body!.Data!.Name);
    }

    [Fact]
    public async Task UpdateClass_OmittingFormTeacherId_KeepsExistingValue()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token, "JSS 3A");

        // Only update name — formTeacherId omitted
        var msg = Build(HttpMethod.Patch, $"/api/v1/academic/classes/{cls.Id}",
            new { name = "JSS 3B" }, subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<ClassResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        // FormTeacherId was null and was not changed
        Assert.Null(body!.Data!.FormTeacherId);
        Assert.Equal("JSS 3B", body.Data.Name);
    }

    // ── Subjects ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSubject_WithCodeAndName_Returns201()
    {
        var (subdomain, token) = await SeedAdminAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/academic/subjects",
            new { name = "Mathematics", code = "MATH" }, subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<SubjectResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal("Mathematics", body!.Data!.Name);
        Assert.Equal("MATH", body.Data.Code);
    }

    [Fact]
    public async Task CreateSubject_WithoutCode_Returns201WithNullCode()
    {
        var (subdomain, token) = await SeedAdminAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/academic/subjects",
            new { name = "English Language", code = (string?)null }, subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<SubjectResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Null(body!.Data!.Code);
    }

    [Fact]
    public async Task GetSubjects_ReturnsPaginatedList()
    {
        var (subdomain, token) = await SeedAdminAsync();
        await CreateSubjectAsync(subdomain, token, "Physics", "PHY");

        var response = await _client.SendAsync(
            Build(HttpMethod.Get, "/api/v1/academic/subjects?page=1&pageSize=20", null, subdomain, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<PagedResponse<SubjectResponse>>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.NotEmpty(body!.Data!.Items);
    }

    [Fact]
    public async Task UpdateSubject_ClearingCodeWithExplicitNull_Returns200WithNullCode()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var subject = await CreateSubjectAsync(subdomain, token, "Chemistry", "CHEM");

        // code: null explicitly present in JSON → should clear the code
        var msg = Build(HttpMethod.Patch, $"/api/v1/academic/subjects/{subject.Id}",
            new { code = (string?)null }, subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<SubjectResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Null(body!.Data!.Code);
    }

    // ── Periods / Timetable ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateTimetabledPeriod_WithNoNameProvided_DefaultsToSubjectName()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token);
        var subject = await CreateSubjectAsync(subdomain, token, "Mathematics", "MATH");

        var period = await CreatePeriodAsync(subdomain, token, cls.Id,
            subjectId: subject.Id, type: "Timetabled", name: null);

        Assert.Equal("Mathematics", period.Name);
        Assert.Equal(PeriodType.Timetabled, period.Type);
    }

    [Fact]
    public async Task CreateTimetabledPeriod_WithCustomName_UsesProvidedName()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token, "JSS 2B");
        var subject = await CreateSubjectAsync(subdomain, token, "Biology", "BIO");

        var period = await CreatePeriodAsync(subdomain, token, cls.Id,
            subjectId: subject.Id, type: "Timetabled", name: "Advanced Biology");

        Assert.Equal("Advanced Biology", period.Name);
    }

    [Fact]
    public async Task CreateDailyRegisterPeriod_DefaultsToMorningRegister()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token, "SSS 1A");

        var period = await CreatePeriodAsync(subdomain, token, cls.Id,
            type: "DailyRegister", startTime: "07:45:00", endTime: "08:00:00");

        Assert.Equal("Morning Register", period.Name);
        Assert.Equal(PeriodType.DailyRegister, period.Type);
    }

    [Fact]
    public async Task CreateNonAcademicPeriod_WithRequiredName_Returns201()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token, "SSS 2A");

        var period = await CreatePeriodAsync(subdomain, token, cls.Id,
            type: "NonAcademic", startTime: "10:30:00", endTime: "10:45:00", name: "Break");

        Assert.Equal("Break", period.Name);
        Assert.Equal(PeriodType.NonAcademic, period.Type);
        Assert.Null(period.SubjectId);
    }

    [Fact]
    public async Task CreatePeriod_WithConflictingTimeSlot_Returns409()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token, "JSS 3B");
        var subject = await CreateSubjectAsync(subdomain, token, "History", "HIS");

        // First period: 08:00-09:00
        await CreatePeriodAsync(subdomain, token, cls.Id,
            subjectId: subject.Id, startTime: "08:00:00", endTime: "09:00:00");

        // Second period: 08:30-09:30 — overlaps with first
        var msg = Build(HttpMethod.Post, $"/api/v1/academic/classes/{cls.Id}/periods",
            new { subjectId = subject.Id, teacherId = Guid.NewGuid(),
                  dayOfWeek = "Monday", startTime = "08:30:00", endTime = "09:30:00",
                  type = "Timetabled", name = (string?)null },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetTimetable_ReturnsSortedPeriodsForClass()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token, "SSS 3A");
        var subject = await CreateSubjectAsync(subdomain, token, "Geography", "GEO");

        await CreatePeriodAsync(subdomain, token, cls.Id,
            subjectId: subject.Id, startTime: "09:00:00", endTime: "10:00:00");
        await CreatePeriodAsync(subdomain, token, cls.Id,
            type: "DailyRegister", startTime: "07:45:00", endTime: "08:00:00");

        var response = await _client.SendAsync(
            Build(HttpMethod.Get, $"/api/v1/academic/classes/{cls.Id}/timetable", null, subdomain, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<TimetableResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal(cls.Id, body!.Data!.ClassId);
        Assert.Equal(2, body.Data.Periods.Count);
        // Periods should be ordered by time
        Assert.True(body.Data.Periods[0].StartTime < body.Data.Periods[1].StartTime);
    }

    [Fact]
    public async Task UpdatePeriod_ShiftingTimeSlot_Returns200WithNewTimes()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token, "JSS 1C");
        var subject = await CreateSubjectAsync(subdomain, token, "Art", "ART");

        var period = await CreatePeriodAsync(subdomain, token, cls.Id,
            subjectId: subject.Id, startTime: "08:00:00", endTime: "09:00:00");

        var msg = Build(HttpMethod.Patch, $"/api/v1/academic/classes/{cls.Id}/periods/{period.Id}",
            new { startTime = "09:00:00", endTime = "10:00:00" },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BaseResponse<PeriodResponse>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.Equal(new TimeOnly(9, 0), body!.Data!.StartTime);
        Assert.Equal(new TimeOnly(10, 0), body.Data.EndTime);
    }

    // ── Tenant isolation ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAcademicYears_DoesNotReturnAnotherTenantData()
    {
        var (subdomain1, token1) = await SeedAdminAsync();
        var (subdomain2, token2) = await SeedAdminAsync();

        // Tenant 1 creates a year
        await CreateYearAsync(subdomain1, token1, "2025/2026 Tenant1");

        // Tenant 2 queries — should not see Tenant 1's year
        var response = await _client.SendAsync(
            Build(HttpMethod.Get, "/api/v1/academic/years?page=1&pageSize=20", null, subdomain2, token2));

        var body = await response.Content.ReadFromJsonAsync<BaseResponse<PagedResponse<AcademicYearResponse>>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.DoesNotContain(body!.Data!.Items, y => y.Name == "2025/2026 Tenant1");
    }

    [Fact]
    public async Task GetClasses_DoesNotReturnAnotherTenantData()
    {
        var (subdomain1, token1) = await SeedAdminAsync();
        var (subdomain2, token2) = await SeedAdminAsync();

        await CreateClassAsync(subdomain1, token1, "Private Class Tenant1");

        var response = await _client.SendAsync(
            Build(HttpMethod.Get, "/api/v1/academic/classes?page=1&pageSize=20", null, subdomain2, token2));

        var body = await response.Content.ReadFromJsonAsync<BaseResponse<PagedResponse<ClassResponse>>>(
            SchoolMasterWebApplicationFactory.JsonOptions);
        Assert.DoesNotContain(body!.Data!.Items, c => c.Name == "Private Class Tenant1");
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAcademicYear_WithEndDateBeforeStartDate_Returns400()
    {
        var (subdomain, token) = await SeedAdminAsync();

        var msg = Build(HttpMethod.Post, "/api/v1/academic/years",
            new { name = "Bad Year", startDate = "2026-07-31", endDate = "2025-09-01", setAsCurrent = false },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNonAcademicPeriod_WithoutName_Returns400()
    {
        var (subdomain, token) = await SeedAdminAsync();
        var cls = await CreateClassAsync(subdomain, token, "JSS 2C");

        var msg = Build(HttpMethod.Post, $"/api/v1/academic/classes/{cls.Id}/periods",
            new { subjectId = (Guid?)null, teacherId = (Guid?)null,
                  dayOfWeek = "Monday", startTime = "10:30:00", endTime = "10:45:00",
                  type = "NonAcademic", name = (string?)null },
            subdomain, token);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

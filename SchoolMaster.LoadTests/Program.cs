using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NBomber.CSharp;

namespace SchoolMaster.LoadTests;

class Program
{
    private const string BaseUrl = "http://localhost:7001";
    private static readonly List<SchoolTestData> TestSchools = new();

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Setting up SchoolMaster NBomber Load Tests (using Native HttpClient)...");

        using var httpClient = new HttpClient();

        // We define a single scenario representing a realistic, complete user flow.
        var scenario = Scenario.Create("complete_school_lifecycle_flow", async context =>
        {
            if (TestSchools.Count == 0)
            {
                return Response.Fail(message: "No seeded schools available", statusCode: "500");
            }

            // 1. Pick a random school from the pre-seeded pool
            var school = TestSchools[Random.Shared.Next(TestSchools.Count)];

            // 2. Admin Login Flow
            var adminLoginPayload = new
            {
                email = school.AdminEmail,
                password = school.AdminPassword
            };
            var adminLoginReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/auth/login")
            {
                Content = JsonContent.Create(adminLoginPayload)
            };
            adminLoginReq.Headers.Add("X-Tenant-Subdomain", school.Subdomain);

            var adminLoginRes = await httpClient.SendAsync(adminLoginReq);
            if (!adminLoginRes.IsSuccessStatusCode)
            {
                return Response.Fail(message: "Admin login failed", statusCode: ((int)adminLoginRes.StatusCode).ToString());
            }

            // 3. Teacher Login Flow
            var teacherLoginPayload = new
            {
                email = school.TeacherEmail,
                password = school.TeacherPassword
            };
            var teacherLoginReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/auth/login")
            {
                Content = JsonContent.Create(teacherLoginPayload)
            };
            teacherLoginReq.Headers.Add("X-Tenant-Subdomain", school.Subdomain);

            var teacherLoginRes = await httpClient.SendAsync(teacherLoginReq);
            if (!teacherLoginRes.IsSuccessStatusCode)
            {
                return Response.Fail(message: "Teacher login failed", statusCode: ((int)teacherLoginRes.StatusCode).ToString());
            }

            var teacherLoginResult = await teacherLoginRes.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>();
            var teacherToken = teacherLoginResult!.Data.Token;

            // 4. Mark Attendance with dynamic data
            var records = new List<object>();
            foreach (var studentId in school.StudentIds)
            {
                var roll = Random.Shared.Next(100);
                string status = roll switch
                {
                    < 70 => "Present",  // 70% chance
                    < 85 => "Absent",   // 15% chance
                    < 95 => "Late",     // 10% chance
                    _ => "Excused"      // 5% chance
                };

                records.Add(new
                {
                    studentId = studentId,
                    status = status,
                    notes = $"Marked {status} by simulator load test"
                });
            }

            var attendancePayload = new
            {
                classId = school.ClassId,
                date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                records = records
            };

            var attendanceReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/attendance")
            {
                Content = JsonContent.Create(attendancePayload)
            };
            attendanceReq.Headers.Add("X-Tenant-Subdomain", school.Subdomain);
            attendanceReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);

            var attendanceRes = await httpClient.SendAsync(attendanceReq);
            if (!attendanceRes.IsSuccessStatusCode)
            {
                return Response.Fail(message: "Mark Attendance failed", statusCode: ((int)attendanceRes.StatusCode).ToString());
            }

            return Response.Ok();
        })
        .WithInit(async context =>
        {
            context.Logger.Information("Initializing load test: Pre-seeding 5 schools...");
            using var initClient = new HttpClient();
            
            for (int i = 1; i <= 5; i++)
            {
                context.Logger.Information($"Seeding school {i}/5...");
                try
                {
                    var school = await OnboardAndSeedSchool(initClient, i);
                    TestSchools.Add(school);
                }
                catch (Exception ex)
                {
                    context.Logger.Error($"Failed to seed school {i}: {ex.Message}");
                    throw;
                }
            }
            context.Logger.Information("Finished seeding test schools successfully.");
        })
        .WithLoadSimulations(
            // Simulate 5 parallel users executing login and attendance flows
            Simulation.KeepConstant(copies: 5, during: TimeSpan.FromSeconds(30))
        );

        // Run NBomber
        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        return 0;
    }

    #region Seeding Helper

    private static async Task<SchoolTestData> OnboardAndSeedSchool(HttpClient httpClient, int index)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var subdomain = $"school-load-{index}-{uniqueId}";
        var adminEmail = $"admin-{uniqueId}@school.edu";
        var teacherEmail = $"teacher-{uniqueId}@school.edu";
        var adminPassword = "SecurePassword123!";
        var defaultPassword = "SecurePassword123!";
        var schoolCode = GenerateRandomSchoolCode();
        
        // 1. Onboard Tenant (Admin account created)
        var onboardPayload = new
        {
            schoolName = $"Academy of {uniqueId}",
            subdomain = subdomain,
            contactEmail = $"hello-{uniqueId}@school.edu",
            adminEmail = adminEmail,
            adminFirstName = "Susan",
            adminLastName = "Ojone",
            adminPassword = adminPassword,
            schoolCode = schoolCode
        };

        var onboardRes = await httpClient.PostAsJsonAsync($"{BaseUrl}/api/v1/onboarding/tenants", onboardPayload);
        if (!onboardRes.IsSuccessStatusCode)
        {
            throw new Exception($"Onboarding failed: {onboardRes.StatusCode}");
        }

        var onboardResult = await onboardRes.Content.ReadFromJsonAsync<BaseResponse<Guid>>();
        var tenantId = onboardResult!.Data;

        // 2. Verify Admin Email
        var verifyAdminPayload = new
        {
            email = adminEmail,
            otpToken = "000000"
        };
        var verifyAdminReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/onboarding/verify-email")
        {
            Content = JsonContent.Create(verifyAdminPayload)
        };
        verifyAdminReq.Headers.Add("X-Tenant-Subdomain", subdomain);

        var verifyAdminRes = await httpClient.SendAsync(verifyAdminReq);
        if (!verifyAdminRes.IsSuccessStatusCode)
        {
            throw new Exception($"Admin verification failed: {verifyAdminRes.StatusCode}");
        }

        // 3. Admin Login (Generate JWT token)
        var loginPayload = new
        {
            email = adminEmail,
            password = adminPassword
        };
        var loginReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/auth/login")
        {
            Content = JsonContent.Create(loginPayload)
        };
        loginReq.Headers.Add("X-Tenant-Subdomain", subdomain);

        var loginRes = await httpClient.SendAsync(loginReq);
        if (!loginRes.IsSuccessStatusCode)
        {
            throw new Exception($"Admin login failed: {loginRes.StatusCode}");
        }

        var loginResult = await loginRes.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>();
        var adminToken = loginResult!.Data.Token;

        // 4. Create Academic Year (Admin)
        var yearPayload = new
        {
            name = $"2025/2026-{uniqueId}",
            startDate = "2025-09-01",
            endDate = "2026-07-31",
            setAsCurrent = true
        };
        var yearReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/academic/years")
        {
            Content = JsonContent.Create(yearPayload)
        };
        yearReq.Headers.Add("X-Tenant-Subdomain", subdomain);
        yearReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var yearRes = await httpClient.SendAsync(yearReq);
        if (!yearRes.IsSuccessStatusCode)
        {
            throw new Exception($"Create Academic Year failed: {yearRes.StatusCode}");
        }

        var yearResult = await yearRes.Content.ReadFromJsonAsync<BaseResponse<AcademicYearResponse>>();
        var yearId = yearResult!.Data.Id;

        // 5. Create Term (Admin)
        var termPayload = new
        {
            academicYearId = yearId,
            name = "First Term",
            termNumber = 1,
            startDate = "2025-09-01",
            endDate = "2025-12-20",
            setAsCurrent = true
        };
        var termReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/academic/terms")
        {
            Content = JsonContent.Create(termPayload)
        };
        termReq.Headers.Add("X-Tenant-Subdomain", subdomain);
        termReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var termRes = await httpClient.SendAsync(termReq);
        if (!termRes.IsSuccessStatusCode)
        {
            throw new Exception($"Create Term failed: {termRes.StatusCode}");
        }

        // 6. Create Class (Admin)
        var classPayload = new
        {
            name = $"Class-JSS1A-{uniqueId}",
            formTeacherId = (Guid?)null
        };
        var classReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/academic/classes")
        {
            Content = JsonContent.Create(classPayload)
        };
        classReq.Headers.Add("X-Tenant-Subdomain", subdomain);
        classReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var classRes = await httpClient.SendAsync(classReq);
        if (!classRes.IsSuccessStatusCode)
        {
            throw new Exception($"Create Class failed: {classRes.StatusCode}");
        }

        var classResult = await classRes.Content.ReadFromJsonAsync<BaseResponse<ClassResponse>>();
        var classId = classResult!.Data.Id;

        // 7. Create Subject (Admin)
        var subjectPayload = new
        {
            name = $"Mathematics-{uniqueId}",
            code = $"MTH-{uniqueId[..3].ToUpper()}"
        };
        var subjectReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/academic/subjects")
        {
            Content = JsonContent.Create(subjectPayload)
        };
        subjectReq.Headers.Add("X-Tenant-Subdomain", subdomain);
        subjectReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var subjectRes = await httpClient.SendAsync(subjectReq);
        if (!subjectRes.IsSuccessStatusCode)
        {
            throw new Exception($"Create Subject failed: {subjectRes.StatusCode}");
        }

        // 8. Create Staff Member (Teacher role)
        var teacherPayload = new
        {
            firstName = "Jane",
            lastName = "Doe",
            email = teacherEmail,
            password = defaultPassword,
            department = "Mathematics",
            staffRole = "Teacher",
            employmentType = "FullTime"
        };
        var teacherReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/staff")
        {
            Content = JsonContent.Create(teacherPayload)
        };
        teacherReq.Headers.Add("X-Tenant-Subdomain", subdomain);
        teacherReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var teacherRes = await httpClient.SendAsync(teacherReq);
        if (!teacherRes.IsSuccessStatusCode)
        {
            throw new Exception($"Create Teacher failed: {teacherRes.StatusCode}");
        }

        // 9. Verify Teacher Email
        var verifyTeacherPayload = new
        {
            email = teacherEmail,
            otpToken = "000000"
        };
        var verifyTeacherReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/onboarding/verify-email")
        {
            Content = JsonContent.Create(verifyTeacherPayload)
        };
        verifyTeacherReq.Headers.Add("X-Tenant-Subdomain", subdomain);

        var verifyTeacherRes = await httpClient.SendAsync(verifyTeacherReq);
        if (!verifyTeacherRes.IsSuccessStatusCode)
        {
            throw new Exception($"Teacher verification failed: {verifyTeacherRes.StatusCode}");
        }

        // 10. Create 5 Students and assign directly to Class (Admin)
        var studentIds = new List<Guid>();
        for (int s = 1; s <= 5; s++)
        {
            var studentEmail = $"student-{s}-{uniqueId}@school.edu";
            var studentPayload = new
            {
                firstName = $"John-{s}",
                lastName = $"Smith-{s}",
                email = studentEmail,
                password = defaultPassword,
                dateOfBirth = "2012-05-15",
                gender = "Male",
                guardianName = "Mr. Smith",
                guardianPhone = "08012345678",
                guardianEmail = $"guardian-{s}-{uniqueId}@test.com",
                medicalNotes = "None",
                photoUrl = (string?)null,
                classId = classId
            };
            var studentReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/students")
            {
                Content = JsonContent.Create(studentPayload)
            };
            studentReq.Headers.Add("X-Tenant-Subdomain", subdomain);
            studentReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var studentRes = await httpClient.SendAsync(studentReq);
            if (!studentRes.IsSuccessStatusCode)
            {
                throw new Exception($"Create Student {s} failed: {studentRes.StatusCode}");
            }

            var studentResult = await studentRes.Content.ReadFromJsonAsync<BaseResponse<StudentResponse>>();
            studentIds.Add(studentResult!.Data.Id);
        }

        return new SchoolTestData
        {
            Subdomain = subdomain,
            AdminEmail = adminEmail,
            AdminPassword = adminPassword,
            TeacherEmail = teacherEmail,
            TeacherPassword = defaultPassword,
            ClassId = classId,
            StudentIds = studentIds
        };
    }

    private static string GenerateRandomSchoolCode()
    {
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return new string(new[] { chars[Random.Shared.Next(26)], chars[Random.Shared.Next(26)], chars[Random.Shared.Next(26)] });
    }

    #endregion
}

#region Helper classes

public class SchoolTestData
{
    public string Subdomain { get; set; } = "";
    public string AdminEmail { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public string TeacherEmail { get; set; } = "";
    public string TeacherPassword { get; set; } = "";
    public Guid ClassId { get; set; }
    public List<Guid> StudentIds { get; set; } = new();
}

#endregion

#region Domain models (redefined for JSON deserialization)

public class BaseResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T Data { get; set; }
}

public class AuthResponse
{
    public string Token { get; set; }
    public string RefreshToken { get; set; }
}

public class AcademicYearResponse
{
    public Guid Id { get; set; }
}

public class ClassResponse
{
    public Guid Id { get; set; }
}

public class StudentResponse
{
    public Guid Id { get; set; }
}

#endregion

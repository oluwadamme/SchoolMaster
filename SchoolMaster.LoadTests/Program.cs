using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NBomber.CSharp;
using Npgsql;

namespace SchoolMaster.LoadTests;

class Program
{
    private const string BaseUrl = "http://localhost:7001";
    private const string ConnectionString = "Server=localhost;Port=5432;Database=schoolmaster;Username=postgres;Password=postgres";
    private static readonly Random Rng = new();

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Setting up SchoolMaster NBomber Load Tests (using Native HttpClient)...");

        using var httpClient = new HttpClient();

        // We define a single scenario representing a realistic, complete user flow.
        var scenario = Scenario.Create("complete_school_lifecycle_flow", async context =>
        {
            var uniqueId = Guid.NewGuid().ToString("N")[..8];
            var subdomain = $"school-{uniqueId}";
            var adminEmail = $"admin-{uniqueId}@school.edu";
            var teacherEmail = $"teacher-{uniqueId}@school.edu";
            var studentEmail = $"student-{uniqueId}@school.edu";
            var adminPassword = "SecurePassword123!";
            var defaultPassword = "SecurePassword123!";
            var schoolCode = GenerateRandomSchoolCode();
            
            // 1. Onboard Tenant (Admin account created)
            context.Logger.Verbose($"Step 1: Onboarding tenant {subdomain} with code {schoolCode}");
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

            var onboardReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/onboarding/tenants")
            {
                Content = JsonContent.Create(onboardPayload)
            };

            var onboardRes = await httpClient.SendAsync(onboardReq);
            if (!onboardRes.IsSuccessStatusCode)
            {
                return Response.Fail(message: "Onboarding failed", statusCode: ((int)onboardRes.StatusCode).ToString());
            }

            // Extract Tenant ID from response
            var onboardResult = await onboardRes.Content.ReadFromJsonAsync<BaseResponse<Guid>>();
            var tenantId = onboardResult!.Data;

            // 2. Fetch Admin OTP from DB
            var adminOtp = await GetOtpFromDb(adminEmail);
            if (string.IsNullOrEmpty(adminOtp))
            {
                return Response.Fail(message: "Admin OTP not found in DB", statusCode: "500");
            }

            // 3. Verify Admin Email
            var verifyAdminPayload = new
            {
                email = adminEmail,
                otpToken = adminOtp
            };
            var verifyAdminReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/onboarding/verify-email")
            {
                Content = JsonContent.Create(verifyAdminPayload)
            };
            verifyAdminReq.Headers.Add("X-Tenant-Subdomain", subdomain);

            var verifyAdminRes = await httpClient.SendAsync(verifyAdminReq);
            if (!verifyAdminRes.IsSuccessStatusCode)
            {
                return Response.Fail(message: "Admin verification failed", statusCode: ((int)verifyAdminRes.StatusCode).ToString());
            }

            // 4. Admin Login (Generate JWT token)
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
                return Response.Fail(message: "Admin login failed", statusCode: ((int)loginRes.StatusCode).ToString());
            }

            var loginResult = await loginRes.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>();
            var adminToken = loginResult!.Data.Token;

            // 5. Create Academic Year (Admin)
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
                return Response.Fail(message: "Create Academic Year failed", statusCode: ((int)yearRes.StatusCode).ToString());
            }

            var yearResult = await yearRes.Content.ReadFromJsonAsync<BaseResponse<AcademicYearResponse>>();
            var yearId = yearResult!.Data.Id;

            // 6. Create Term (Admin)
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
                return Response.Fail(message: "Create Term failed", statusCode: ((int)termRes.StatusCode).ToString());
            }

            // 7. Create Class (Admin)
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
                return Response.Fail(message: "Create Class failed", statusCode: ((int)classRes.StatusCode).ToString());
            }

            var classResult = await classRes.Content.ReadFromJsonAsync<BaseResponse<ClassResponse>>();
            var classId = classResult!.Data.Id;

            // 8. Create Subject (Admin)
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
                return Response.Fail(message: "Create Subject failed", statusCode: ((int)subjectRes.StatusCode).ToString());
            }

            // 9. Create Staff Member (Teacher role)
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
                return Response.Fail(message: "Create Teacher failed", statusCode: ((int)teacherRes.StatusCode).ToString());
            }

            // 10. Fetch Teacher OTP and Verify Email
            var teacherOtp = await GetOtpFromDb(teacherEmail);
            if (string.IsNullOrEmpty(teacherOtp))
            {
                return Response.Fail(message: "Teacher OTP not found in DB", statusCode: "500");
            }

            var verifyTeacherPayload = new
            {
                email = teacherEmail,
                otpToken = teacherOtp
            };
            var verifyTeacherReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/onboarding/verify-email")
            {
                Content = JsonContent.Create(verifyTeacherPayload)
            };
            verifyTeacherReq.Headers.Add("X-Tenant-Subdomain", subdomain);

            var verifyTeacherRes = await httpClient.SendAsync(verifyTeacherReq);
            if (!verifyTeacherRes.IsSuccessStatusCode)
            {
                return Response.Fail(message: "Teacher verification failed", statusCode: ((int)verifyTeacherRes.StatusCode).ToString());
            }

            // 11. Create Student (Admin)
            var studentPayload = new
            {
                firstName = "John",
                lastName = "Smith",
                email = studentEmail,
                password = defaultPassword,
                dateOfBirth = "2012-05-15",
                gender = "Male",
                guardianName = "Mr. Smith",
                guardianPhone = "08012345678",
                guardianEmail = $"guardian-{uniqueId}@test.com",
                medicalNotes = "None",
                photoUrl = (string?)null
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
                return Response.Fail(message: "Create Student failed", statusCode: ((int)studentRes.StatusCode).ToString());
            }

            var studentResult = await studentRes.Content.ReadFromJsonAsync<BaseResponse<StudentResponse>>();
            var studentId = studentResult!.Data.Id;

            // 12. Link Student to Class directly in the DB (since there's no assign endpoint yet)
            await AssignStudentToClassInDb(studentId, classId);

            // 13. Login as Teacher (Generate JWT token)
            var teacherLoginPayload = new
            {
                email = teacherEmail,
                password = defaultPassword
            };
            var teacherLoginReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/auth/login")
            {
                Content = JsonContent.Create(teacherLoginPayload)
            };
            teacherLoginReq.Headers.Add("X-Tenant-Subdomain", subdomain);

            var teacherLoginRes = await httpClient.SendAsync(teacherLoginReq);
            if (!teacherLoginRes.IsSuccessStatusCode)
            {
                return Response.Fail(message: "Teacher login failed", statusCode: ((int)teacherLoginRes.StatusCode).ToString());
            }

            var teacherLoginResult = await teacherLoginRes.Content.ReadFromJsonAsync<BaseResponse<AuthResponse>>();
            var teacherToken = teacherLoginResult!.Data.Token;

            // 14. Mark Attendance (Teacher)
            var attendancePayload = new
            {
                classId = classId,
                date = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"),
                records = new[]
                {
                    new { studentId = studentId, status = "Present", notes = "Attended lesson" }
                }
            };
            var attendanceReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/attendance")
            {
                Content = JsonContent.Create(attendancePayload)
            };
            attendanceReq.Headers.Add("X-Tenant-Subdomain", subdomain);
            attendanceReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherToken);

            var attendanceRes = await httpClient.SendAsync(attendanceReq);
            if (!attendanceRes.IsSuccessStatusCode)
            {
                return Response.Fail(message: "Mark Attendance failed", statusCode: ((int)attendanceRes.StatusCode).ToString());
            }

            return Response.Ok();
        })
        .WithLoadSimulations(
            // Simulate 5 parallel users going through this entire lifecycle flow
            Simulation.KeepConstant(copies: 5, during: TimeSpan.FromSeconds(30))
        );

        // Run NBomber
        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        return 0;
    }

    #region Helpers

    private static string GenerateRandomSchoolCode()
    {
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        lock (Rng)
        {
            return new string(new[] { chars[Rng.Next(26)], chars[Rng.Next(26)], chars[Rng.Next(26)] });
        }
    }

    private static async Task<string> GetOtpFromDb(string email)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("SELECT \"OtpToken\" FROM \"Users\" WHERE \"Email\" = @email LIMIT 1", conn);
        cmd.Parameters.AddWithValue("email", email);
        var otp = (string?)await cmd.ExecuteScalarAsync();
        return otp ?? "";
    }

    private static async Task AssignStudentToClassInDb(Guid studentId, Guid classId)
    {
        using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("UPDATE \"Students\" SET \"ClassId\" = @classId WHERE \"Id\" = @studentId", conn);
        cmd.Parameters.AddWithValue("classId", classId);
        cmd.Parameters.AddWithValue("studentId", studentId);
        await cmd.ExecuteNonQueryAsync();
    }

    #endregion
}

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

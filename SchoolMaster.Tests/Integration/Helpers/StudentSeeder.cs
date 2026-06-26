using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Infrastructure.Persistence;

namespace SchoolMaster.Tests.Integration.Helpers;

/// <summary>
/// Seeds a Student (with its required one-to-one User) directly into the database.
/// </summary>
public static class StudentSeeder
{
    /// <summary>
    /// Adds a student to the given class. The tenant is taken from the class itself so the
    /// student always lands in the same tenant as the class it belongs to.
    /// Returns the new student's id.
    /// </summary>
    public static async Task<Guid> SeedAsync(
        IServiceProvider services,
        Guid classId,
        string? guardianEmail = null)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolMasterContext>();

        // No HttpContext in this scope, so the tenant query filter resolves to Guid.Empty.
        // IgnoreQueryFilters lets us read the class to discover which tenant it belongs to.
        var cls = await db.Classes.IgnoreQueryFilters().FirstAsync(c => c.Id == classId);
        var tenantId = cls.TenantId;

        var unique = Guid.NewGuid().ToString("N")[..8];

        // Student requires a backing User row (one-to-one, Restrict delete). User has public
        // setters, so a plain object initialiser is fine here.
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FirstName = "Test",
            LastName = "Student",
            Email = $"student-{unique}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123!"),
            Roles = [UserRole.Student],
            Status = UserStatus.Active,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        var studentId = Guid.NewGuid();
        var student = new Student
        {
            Id = studentId,
            UserId = user.Id,
            TenantId = tenantId,
            ClassId = classId,
            StudentNumber = $"STU-{unique}",
            FirstName = "Test",
            LastName = "Student",
            DateOfBirth = new DateOnly(2012, 1, 1),
            Gender = Gender.Male,
            GuardianName = "Test Guardian",
            GuardianPhone = "08000000000",
            GuardianEmail = guardianEmail ?? $"guardian-{unique}@test.com",
            Status = StudentStatus.Active,
            EnrolledAt = DateTime.UtcNow
        };
        db.Students.Add(student);

        await db.SaveChangesAsync();
        return studentId;
    }
}

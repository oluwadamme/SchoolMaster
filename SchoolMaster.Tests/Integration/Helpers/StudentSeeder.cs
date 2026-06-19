using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Infrastructure.Persistence;

namespace SchoolMaster.Tests.Integration.Helpers;

/// <summary>
/// Seeds a Student (with its required one-to-one User) directly into the database.
/// There is no student-creation endpoint yet, and the Student entity exposes only private
/// setters with no public factory, so we populate it through EF Core's change tracker
/// (EntityEntry.Property), which bypasses CLR setter accessibility.
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
        var student = new Student();
        var entry = db.Entry(student);
        entry.Property(nameof(Student.Id)).CurrentValue = studentId;
        entry.Property(nameof(Student.UserId)).CurrentValue = user.Id;
        entry.Property(nameof(Student.TenantId)).CurrentValue = tenantId;
        entry.Property(nameof(Student.ClassId)).CurrentValue = classId;
        entry.Property(nameof(Student.StudentNumber)).CurrentValue = $"STU-{unique}";
        entry.Property(nameof(Student.FirstName)).CurrentValue = "Test";
        entry.Property(nameof(Student.LastName)).CurrentValue = "Student";
        entry.Property(nameof(Student.DateOfBirth)).CurrentValue = new DateOnly(2012, 1, 1);
        entry.Property(nameof(Student.Gender)).CurrentValue = Gender.Male;
        entry.Property(nameof(Student.GuardianName)).CurrentValue = "Test Guardian";
        entry.Property(nameof(Student.GuardianPhone)).CurrentValue = "08000000000";
        entry.Property(nameof(Student.GuardianEmail)).CurrentValue =
            guardianEmail ?? $"guardian-{unique}@test.com";
        entry.Property(nameof(Student.Status)).CurrentValue = StudentStatus.Active;
        entry.Property(nameof(Student.EnrolledAt)).CurrentValue = DateTime.UtcNow;
        entry.State = EntityState.Added;

        await db.SaveChangesAsync();
        return studentId;
    }
}

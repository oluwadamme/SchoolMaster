using Microsoft.Extensions.DependencyInjection;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;
using SchoolMaster.Infrastructure.Persistence;

namespace SchoolMaster.Tests.Integration.Helpers;

/// <summary>
/// creates users with specific roles directly into the database.
/// also to keep the code clean and simple
/// </summary>
public static class UserSeeder
{
    /// <summary>
    /// Creates a tenant and a user with the given roles .
    /// Returns the subdomain, email, and plain-text password needed for login.
    /// </summary>
    public static async Task<(string Subdomain, string Email, string Password)> SeedAsync(
        IServiceProvider services,
        List<UserRole> roles)
    {
        const string password = "Test@123!";
        var unique = Guid.NewGuid().ToString("N")[..8];
        var subdomain = $"s{unique}";
        var email = $"user-{unique}@test.com";

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolMasterContext>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Seeded School",
            Subdomain = subdomain,
            ContactEmail = $"contact-{unique}@test.com",
            Status = TenantStatus.Active,
            SchoolCode = "SEED",
            Plan = TenantPlan.Basic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            FirstName = "Seed",
            LastName = "User",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Roles = roles,
            Status = UserStatus.Active,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Tenants.Add(tenant);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return (subdomain, email, password);
    }
}

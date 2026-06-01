using Microsoft.EntityFrameworkCore;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Application.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchoolMaster.Domain.Enums;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SchoolMaster.Infrastructure.Persistence;

public class SchoolMasterContext(DbContextOptions<SchoolMasterContext> options, ICurrentTenant _currentTenant) : DbContext(options)
{

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(u => new { u.TenantId, u.Email}).IsUnique();

        modelBuilder.Entity<Tenant>().HasIndex(t => t.Subdomain).IsUnique();

        modelBuilder.Entity<Class>()
            .HasIndex(c => new { c.TenantId, c.Name }).IsUnique();

        modelBuilder.Entity<Subject>()
            .HasIndex(s => new { s.TenantId, s.Name }).IsUnique();

        var jsonOptions = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        modelBuilder.Entity<User>()
            .Property(u => u.Roles)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<List<UserRole>>(v, jsonOptions) ?? new List<UserRole>(),
                new ValueComparer<List<UserRole>>(
                    (c1, c2) => c1!.SequenceEqual(c2!),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()
                )
            );

        // User ← Student (one-to-one)
        modelBuilder.Entity<Student>()
            .HasOne(s => s.User)           // Student has one User
            .WithOne()                     // User has no nav property back
            .HasForeignKey<Student>(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // User ← Staff (one-to-one)
        modelBuilder.Entity<Staff>()
            .HasOne(s => s.User)
            .WithOne()
            .HasForeignKey<Staff>(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tenant ← User (one-to-many: a tenant has many users)
        modelBuilder.Entity<User>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Period → Class relationship
        modelBuilder.Entity<Period>()
            .HasOne(p => p.Class)
            .WithMany()
            .HasForeignKey(p => p.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        // Period → Subject relationship (optional)
        modelBuilder.Entity<Period>()
            .HasOne(p => p.Subject)
            .WithMany()
            .HasForeignKey(p => p.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // Term → AcademicYear relationship
        modelBuilder.Entity<Term>()
            .HasOne(t => t.AcademicYear)
            .WithMany(y => y.Terms)
            .HasForeignKey(t => t.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Student>()
        .HasQueryFilter(s => s.TenantId == _currentTenant.Id);

        modelBuilder.Entity<User>()
            .HasQueryFilter(s => s.TenantId == _currentTenant.Id);

        modelBuilder.Entity<Staff>()
            .HasQueryFilter(s => s.TenantId == _currentTenant.Id);

        modelBuilder.Entity<AcademicYear>()
            .HasQueryFilter(a => a.TenantId == _currentTenant.Id);

        modelBuilder.Entity<Term>()
            .HasQueryFilter(t => t.TenantId == _currentTenant.Id);

        modelBuilder.Entity<Class>()
            .HasQueryFilter(c => c.TenantId == _currentTenant.Id);

        modelBuilder.Entity<Subject>()
            .HasQueryFilter(s => s.TenantId == _currentTenant.Id);

        modelBuilder.Entity<Period>()
            .HasQueryFilter(p => p.TenantId == _currentTenant.Id);



    }

    public DbSet<User> Users { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Staff> Staff { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<AcademicYear> AcademicYears { get; set; }
    public DbSet<Term> Terms { get; set; }
    public DbSet<Class> Classes { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Period> Periods { get; set; }
}
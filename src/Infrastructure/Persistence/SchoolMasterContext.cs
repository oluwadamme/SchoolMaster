using Microsoft.EntityFrameworkCore;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Infrastructure.Persistence;

public class SchoolMasterContext(DbContextOptions<SchoolMasterContext> options, ICurrentTenant _currentTenant) : DbContext(options)
{

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        modelBuilder.Entity<Tenant>().HasIndex(t => t.Subdomain).IsUnique();
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

        modelBuilder.Entity<Student>()
        .HasQueryFilter(s => s.TenantId == _currentTenant.Id);

        modelBuilder.Entity<User>()
            .HasQueryFilter(s => s.TenantId == _currentTenant.Id);

        modelBuilder.Entity<Staff>()
            .HasQueryFilter(s => s.TenantId == _currentTenant.Id);


    }

    public DbSet<User> Users { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Staff> Staff { get; set; }
}
using Microsoft.EntityFrameworkCore;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Application.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using SchoolMaster.Domain.Enums;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SchoolMaster.Infrastructure.Persistence;

public class SchoolMasterContext(DbContextOptions<SchoolMasterContext> options, ICurrentTenant _currentTenant, ICurrentUser _currentUser) : DbContext(options)
{

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>()
            .HaveConversion<string>()
            .HaveColumnType("text");
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(u => new { u.TenantId, u.Email}).IsUnique();

        modelBuilder.Entity<Tenant>().HasIndex(t => t.Subdomain).IsUnique();

        // Ensure StudentNumber is unique within each school (Tenant)
        modelBuilder.Entity<Student>()
            .HasIndex(s => new { s.TenantId, s.StudentNumber })
            .IsUnique();

        // Ensure StaffNumber is unique within each school (Tenant)
        modelBuilder.Entity<Staff>()
            .HasIndex(s => new { s.TenantId, s.StaffNumber })
            .IsUnique();
        modelBuilder.Entity<Class>()
            .HasIndex(c => new { c.TenantId, c.Name }).IsUnique();

        modelBuilder.Entity<Subject>()
            .HasIndex(s => new { s.TenantId, s.Name }).IsUnique();

        // Enforce singleton active record — at most one current academic year per tenant
        modelBuilder.Entity<AcademicYear>()
            .HasIndex(y => y.TenantId)
            .IsUnique()
            .HasFilter("\"IsCurrent\" = true");

        // At most one current term per academic year per tenant.
        // The key is (TenantId, AcademicYearId) only — including TermNumber would let two terms
        // with different numbers both be current at once, which is exactly what we must prevent.
        modelBuilder.Entity<Term>()
            .HasIndex(t => new { t.TenantId, t.AcademicYearId })
            .IsUnique()
            .HasFilter("\"IsCurrent\" = true");

        modelBuilder.Entity<DailyAttendance>()
            .HasIndex(a => new { a.TenantId, a.StudentId, a.Date })
            .IsUnique(); // One record per student per day

        var jsonOptions = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };
        // telling the system we want to setup a special rule for the User table
        modelBuilder.Entity<User>()
            .Property(u => u.Roles)
            // Roles Property is a list
            // Because standard SQL databases don't have a built-in way to store a C# List 
            // When writing to the database, EFCore tells it to create an empty column, and specifically
            // an empty column that holds json.as opposed to a number column or date column
            // So when EF core sends user data 
            // we are forced to convert that specific list 
            // into a JSON text string so it can fit into the empty column the db has created.
            .HasColumnType("jsonb")
            // Because EF Core is just a C# tool, it doesn't automatically know how to 
            // translate a complex C# List into JSON text. 
            // EF Core has to be given the exact instructions on how to do this translation.
            .HasConversion(
                // When your code saves a User, this tells EF Core:
                // "Take the C# List (represented by the letter v)
                // and Serialize it (convert it to JSON text)
                v => JsonSerializer.Serialize(v, jsonOptions),
                // When EF Core reads data from the DB, it needs to convert it back into a C# List.
                // This line tells it how to Deserialize the JSON text back into a C# List
                v => JsonSerializer.Deserialize<List<UserRole>>(v, jsonOptions) ?? new List<UserRole>(),
                // EF Core needs to know how to compare two of tese c# List to see if they are equal
                // This is needed for things like caching and change tracking
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

        modelBuilder.Entity<DailyAttendance>()
            .HasOne<Student>()
            .WithMany()
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailyAttendance>()
            .HasOne<Class>()
            .WithMany()
            .HasForeignKey(a => a.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailyAttendance>()
            .HasOne<Term>()
            .WithMany()
            .HasForeignKey(a => a.TermId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailyAttendance>()
            .HasQueryFilter(a => a.TenantId == _currentTenant.Id);

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

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        OnBeforeSaveChanges();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void OnBeforeSaveChanges()
    {
        // This forces EF Core to take a hard look at all your C# objects in memory 
        // and officially mark down exactly what has been added, modified, or deleted
        ChangeTracker.DetectChanges();
        
        // We create an empty C# list to hold all the new log records we are about to generate
        var auditEntries = new List<AuditLog>();
        Guid tenantId = _currentTenant.Id;
        Guid? userId = null;
        // We try to safely extract the ID of the user who triggered the save operation
        try { userId = _currentUser.Id == Guid.Empty ? null : _currentUser.Id; } catch { } // safety

        // We start looping through every single object EF Core noticed
        foreach (var entry in ChangeTracker.Entries())
        {
            // We first check if the object is an AuditLog itself, or if it hasn't changed at all.
            // If either is true, we skip it.
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            // If the object is something else (like a Student, User, or Class), we create a new log entry for it.
            // each object changed has it's own auditEntry. at the end auditEntries
            // is the auditEntry of all the objects changed
            var auditEntry = new AuditLog
            {
                TenantId = tenantId,
                UserId = userId,
                TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                PrimaryKey = JsonSerializer.Serialize(entry.Metadata.FindPrimaryKey()?.Properties.ToDictionary(p => p.Name, p => entry.Property(p.Name).CurrentValue))
            };

            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();

            foreach (var property in entry.Properties)
            // We look at every single property (like FirstName, LastName, DateOfBirth)
            // inside the object that was changed.
            {
                if (property.IsTemporary)
                    continue;

                string propertyName = property.Metadata.Name;
                // Exclude sensitive fields if necessary
                if (propertyName.ToLower().Contains("password") || propertyName.ToLower().Contains("token"))
                    continue;
                // check what type pf modifcation is made
                if (entry.State == EntityState.Added)
                {
                    newValues[propertyName] = property.CurrentValue;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    oldValues[propertyName] = property.OriginalValue;
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (property.IsModified)
                    {
                        oldValues[propertyName] = property.OriginalValue;
                        newValues[propertyName] = property.CurrentValue;
                    }
                }
            }
            // If a student is Added, the oldValues basket stays completely empty. 
            // Because it is empty (oldValues.Count is 0), we never turn it into JSON. The OldValues column in the database just stays as null
            if (oldValues.Count > 0)
                auditEntry.OldValues = JsonSerializer.Serialize(oldValues);
            if (newValues.Count > 0)
                auditEntry.NewValues = JsonSerializer.Serialize(newValues);

            auditEntries.Add(auditEntry);
        }

        if (auditEntries.Any())
        {
            AuditLogs.AddRange(auditEntries);
        }
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
    public DbSet<DailyAttendance> DailyAttendances { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
}
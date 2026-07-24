namespace SchoolMaster.Domain.Entities;

using System;
using System.Text.Json;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // The tenant (school) where this action occurred
    public Guid TenantId { get; set; }

    // The user who made the change (might be null if system action)
    public Guid? UserId { get; set; }

    // Table that was modified
    public required string TableName { get; set; }

    // Insert, Update, or Delete
    public required string Action { get; set; }

    // Primary Key of the modified record
    public required string PrimaryKey { get; set; }

    // JSON snapshot of data before change
    public string? OldValues { get; set; }

    // JSON snapshot of data after change
    public string? NewValues { get; set; }

    // When this change happened
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

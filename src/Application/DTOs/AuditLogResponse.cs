namespace SchoolMaster.Application.DTOs;

using System;

public class AuditLogResponse
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public required string TableName { get; set; }
    public required string Action { get; set; }
    public required string PrimaryKey { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; }
}

namespace SchoolMaster.Application.Services;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;

public class AuditLogService(IAuditLogRepository _auditLogRepository) : IAuditLogService
{
    public async Task<IEnumerable<AuditLogResponse>> GetAllAsync()
    {
        var logs = await _auditLogRepository.GetAllAsync();
        
        return logs.Select(log => new AuditLogResponse
        {
            Id = log.Id,
            UserId = log.UserId,
            TableName = log.TableName,
            Action = log.Action,
            PrimaryKey = log.PrimaryKey,
            OldValues = log.OldValues,
            NewValues = log.NewValues,
            Timestamp = log.Timestamp
        });
    }
}

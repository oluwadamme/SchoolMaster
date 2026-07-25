namespace SchoolMaster.Application.Services.Interfaces;

using System.Collections.Generic;
using System.Threading.Tasks;
using SchoolMaster.Application.DTOs;

public interface IAuditLogService
{
    Task<IEnumerable<AuditLogResponse>> GetAllAsync();
}

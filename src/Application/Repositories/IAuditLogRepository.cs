namespace SchoolMaster.Application.Repositories;

using System.Collections.Generic;
using System.Threading.Tasks;
using SchoolMaster.Domain.Entities;

public interface IAuditLogRepository
{
    Task<IEnumerable<AuditLog>> GetAllAsync();
}

namespace SchoolMaster.Infrastructure.Repositories;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Infrastructure.Persistence;

public class AuditLogRepository(SchoolMasterContext _context) : IAuditLogRepository
{
    public async Task<IEnumerable<AuditLog>> GetAllAsync()
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync();
    }
}

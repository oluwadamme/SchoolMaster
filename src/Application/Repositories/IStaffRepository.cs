namespace SchoolMaster.Application.Repositories;

using SchoolMaster.Domain.Entities;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public interface IStaffRepository
{
    Task AddStaffAsync(Staff staff);
    Task AddStaffBulkAsync(IEnumerable<Staff> staff);
    Task<Staff?> GetStaffByIdAsync(Guid staffId);
    Task<bool> ExistsByStaffNumberAsync(string staffNumber, Guid tenantId);
    Task<string?> GetLastStaffNumberAsync(Guid tenantId, string prefix);
}
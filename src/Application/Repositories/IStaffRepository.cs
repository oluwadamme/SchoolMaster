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
    Task<Staff?> GetStaffByIdIgnoringFiltersAsync(Guid staffId, Guid tenantId);
    Task<IReadOnlyList<Staff>> GetAllStaffAsync(Guid tenantId);
    Task<bool> ExistsByStaffNumberAsync(string staffNumber, Guid tenantId);
    Task<string?> GetLastStaffNumberAsync(Guid tenantId, string prefix);
}
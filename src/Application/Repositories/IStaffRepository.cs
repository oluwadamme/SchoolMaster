namespace SchoolMaster.Application.Repositories;

using SchoolMaster.Domain.Entities;
using System;
using System.Threading.Tasks;

public interface IStaffRepository
{
    Task AddStaffAsync(Staff staff);
    // This checks if a Staff Number is already used in this specific school
    Task<bool> ExistsByStaffNumberAsync(string staffNumber, Guid tenantId);
}
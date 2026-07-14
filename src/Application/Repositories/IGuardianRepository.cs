using SchoolMaster.Domain.Entities;

namespace SchoolMaster.Application.Repositories;

public interface IGuardianRepository
{
    Task AddGuardianAsync(Guardian guardian);
    Task AddGuardiansBulkAsync(IEnumerable<Guardian> guardians);
    Task<Guardian?> GetGuardianByEmailAsync(string email, Guid tenantId);
    Task<List<Guardian>> GetGuardiansByEmailsAsync(IEnumerable<string> emails, Guid tenantId);
    Task<Guardian?> GetGuardianByIdAsync(Guid id, Guid tenantId);
}

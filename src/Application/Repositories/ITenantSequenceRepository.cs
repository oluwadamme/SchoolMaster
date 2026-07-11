namespace SchoolMaster.Application.Repositories;
public interface ITenantSequenceRepository
{
    // Atomically reserves `count` numbers, returns the FIRST value of the reserved block.
    Task<long> ReserveBlockAsync(Guid tenantId, string sequenceType, int year, int count);
}


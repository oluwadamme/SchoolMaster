using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Infrastructure.Persistence;
namespace SchoolMaster.Infrastructure.Repositories;
public class TenantSequenceRepository(SchoolMasterContext context) : ITenantSequenceRepository
{
    public async Task<long> ReserveBlockAsync(Guid tenantId, string sequenceType, int year, int count)
    {
        // ON CONFLICT ... RETURNING runs with no ambient transaction, so the reservation
        // commits immediately and the row lock serializes concurrent imports. Numbers are
        // consumed even if the later insert fails, which is fine: gaps are acceptable,
        // exactly like a Postgres SEQUENCE.
        const string sql = @"
            INSERT INTO ""TenantNumberSequences"" (""Id"",""TenantId"",""SequenceType"",""Year"",""LastValue"")
            VALUES (gen_random_uuid(), {0}, {1}, {2}, {3})
            ON CONFLICT (""TenantId"",""SequenceType"",""Year"")
            DO UPDATE SET ""LastValue"" = ""TenantNumberSequences"".""LastValue"" + {3}
            RETURNING ""LastValue"";";

        var newHighWater = await context.Database
            .SqlQueryRaw<long>(sql, tenantId, sequenceType, year, count)
            .SingleAsync();

        return newHighWater - count + 1; // start of the block
    }
}

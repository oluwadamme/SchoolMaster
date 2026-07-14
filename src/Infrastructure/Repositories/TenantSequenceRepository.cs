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
            VALUES (gen_random_uuid(), @tenantId, @sequenceType, @year, @count)
            ON CONFLICT (""TenantId"",""SequenceType"",""Year"")
            DO UPDATE SET ""LastValue"" = ""TenantNumberSequences"".""LastValue"" + @count
            RETURNING ""LastValue"";";

        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        
        var pTenantId = command.CreateParameter();
        pTenantId.ParameterName = "@tenantId";
        pTenantId.Value = tenantId;
        command.Parameters.Add(pTenantId);

        var pSequenceType = command.CreateParameter();
        pSequenceType.ParameterName = "@sequenceType";
        pSequenceType.Value = sequenceType;
        command.Parameters.Add(pSequenceType);

        var pYear = command.CreateParameter();
        pYear.ParameterName = "@year";
        pYear.Value = year;
        command.Parameters.Add(pYear);

        var pCount = command.CreateParameter();
        pCount.ParameterName = "@count";
        pCount.Value = count;
        command.Parameters.Add(pCount);

        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync();
        }

        var result = await command.ExecuteScalarAsync();
        var newHighWater = Convert.ToInt64(result);

        return newHighWater - count + 1; // start of the block
    }
}

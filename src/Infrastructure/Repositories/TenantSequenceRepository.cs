using Microsoft.EntityFrameworkCore;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Infrastructure.Persistence;
namespace SchoolMaster.Infrastructure.Repositories;
public class TenantSequenceRepository(SchoolMasterContext context) : ITenantSequenceRepository
{
    // keep track of last number used and add to it, then return
    public async Task<long> ReserveBlockAsync(Guid tenantId, string sequenceType, int year, int count)
    {
        // ON CONFLICT ... RETURNING runs with no ambient transaction, so the reservation
        // commits immediately and the row lock serializes concurrent imports. Numbers are
        // consumed even if the later insert fails, which is fine: gaps are acceptable,
        // exactly like a Postgres SEQUENCE.
        // creates a row in TenantNumberSequences table that has Id, TenantID
        // SequenceType, Year, LastValue. then plugs in the input parameters in those columns
        // when this function is called again, it goes to check the table, if the row is already
        // there, it updates the last value to 2
        // it never creates a second row, it keeps updating the last value
        // CODE BREAKDOWN
        // "INSERT INTO": insert into these columns in tenant number sequences
        // "VALUES": these values
        // "ON CONFLICT": if row already exists, update last value
        const string sql = @"
            INSERT INTO ""TenantNumberSequences"" (""Id"",""TenantId"",""SequenceType"",""Year"",""LastValue"")
            VALUES (gen_random_uuid(), @tenantId, @sequenceType, @year, @count)
            ON CONFLICT (""TenantId"",""SequenceType"",""Year"")
            DO UPDATE SET ""LastValue"" = ""TenantNumberSequences"".""LastValue"" + @count
            RETURNING ""LastValue"";";
        
        //  These two lines create a direct, low-level connection to the database. We create a blank command object,
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        
        // this tells the command, whenever you see @tenantId in the SQL text, replace it with 
        // the actual tenantId variable we passed into this function
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
        
        // The command is fired into the database.
        // Because the very last line of our SQL text says RETURNING "LastValue", the database 
        // does all the math, creates or updates the row, and immediately returns the 
        // final updated number back to us.
        // That number gets stored inside the result variable
        var result = await command.ExecuteScalarAsync();
        // This line simply converts it into a standard C# number (a long integer) 
        // so we can do math on it. We call this newHighWater
        var newHighWater = Convert.ToInt64(result);

        // calculating the starting point of the block of numbers that were reserved
        // i.e if your count is more than one

        var startingPoint = newHighWater - count + 1;

        return startingPoint; // start of the block
    }
}

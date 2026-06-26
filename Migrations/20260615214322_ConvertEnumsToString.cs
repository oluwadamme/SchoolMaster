using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class ConvertEnumsToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Periods""
                ALTER COLUMN ""Type"" TYPE TEXT
                USING CASE ""Type""::integer
                    WHEN 0 THEN 'Timetabled'
                    WHEN 1 THEN 'DailyRegister'
                    ELSE NULL
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Periods""
                ALTER COLUMN ""DayOfWeek"" TYPE TEXT
                USING CASE ""DayOfWeek""::integer
                    WHEN 0 THEN 'Sunday'
                    WHEN 1 THEN 'Monday'
                    WHEN 2 THEN 'Tuesday'
                    WHEN 3 THEN 'Wednesday'
                    WHEN 4 THEN 'Thursday'
                    WHEN 5 THEN 'Friday'
                    WHEN 6 THEN 'Saturday'
                    ELSE NULL
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down()
            migrationBuilder.Sql(@"
                ALTER TABLE ""Periods""
                ALTER COLUMN ""Type"" TYPE INTEGER
                USING CASE ""Type""
                    WHEN 'Timetabled' THEN 0
                    WHEN 'DailyRegister' THEN 1
                    WHEN 'NonAcademic' THEN 2
                    ELSE NULL
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Periods""
                ALTER COLUMN ""DayOfWeek"" TYPE INTEGER
                USING CASE ""DayOfWeek""
                    WHEN 'Sunday' THEN 0
                    WHEN 'Monday' THEN 1
                    WHEN 'Tuesday' THEN 2
                    WHEN 'Wednesday' THEN 3
                    WHEN 'Thursday' THEN 4
                    WHEN 'Friday' THEN 5
                    WHEN 'Saturday' THEN 6
                    ELSE NULL
                END;
            ");

        }
    }
}

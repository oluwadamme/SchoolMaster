using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class ConvertRolesToStringEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Users""
                SET ""Roles"" = (
                    SELECT jsonb_agg(
                        CASE val::int
                            WHEN 0 THEN 'Admin'
                            WHEN 1 THEN 'Teacher'
                            WHEN 2 THEN 'Student'
                            WHEN 3 THEN 'Parent'
                            WHEN 4 THEN 'Staff'
                        END
                    )
                    FROM jsonb_array_elements_text(""Roles"") AS val
                )
                WHERE ""Roles"" IS NOT NULL
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

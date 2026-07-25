using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class FixRolesDoubleEscaping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        UPDATE ""Users""
        SET ""Roles"" = (
            SELECT jsonb_agg(trim('""' from val))
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

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Roles",
                table: "Users",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
            migrationBuilder.Sql(@"
                UPDATE ""Users""
                SET ""Roles"" = jsonb_build_array(""Role"")
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Roles",
                table: "Users");
        }
    }
}

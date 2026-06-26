using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "Roles",
                table: "Users",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SchoolCode",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Students_TenantId_StudentNumber",
                table: "Students",
                columns: new[] { "TenantId", "StudentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_TenantId_StaffNumber",
                table: "Staff",
                columns: new[] { "TenantId", "StaffNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_TenantId_StudentNumber",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Staff_TenantId_StaffNumber",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "Roles",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SchoolCode",
                table: "Tenants");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}

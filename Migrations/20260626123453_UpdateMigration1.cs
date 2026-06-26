using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMigration1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Terms_TenantId_AcademicYearId_TermNumber",
                table: "Terms");

            migrationBuilder.CreateIndex(
                name: "IX_Terms_TenantId_AcademicYearId",
                table: "Terms",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true,
                filter: "\"IsCurrent\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Terms_TenantId_AcademicYearId",
                table: "Terms");

            migrationBuilder.CreateIndex(
                name: "IX_Terms_TenantId_AcademicYearId_TermNumber",
                table: "Terms",
                columns: new[] { "TenantId", "AcademicYearId", "TermNumber" },
                unique: true,
                filter: "\"IsCurrent\" = true");
        }
    }
}

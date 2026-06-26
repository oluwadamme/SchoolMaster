using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentRecordUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Terms_TenantId_AcademicYearId",
                table: "Terms",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true,
                filter: "\"IsCurrent\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_TenantId",
                table: "AcademicYears",
                column: "TenantId",
                unique: true,
                filter: "\"IsCurrent\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Terms_TenantId_AcademicYearId",
                table: "Terms");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_TenantId",
                table: "AcademicYears");
        }
    }
}

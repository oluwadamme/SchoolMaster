using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexToTenantNumberSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_TenantNumberSequences_TenantId_SequenceType_Year",
                table: "TenantNumberSequences",
                columns: new[] { "TenantId", "SequenceType", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantNumberSequences_TenantId_SequenceType_Year",
                table: "TenantNumberSequences");
        }
    }
}

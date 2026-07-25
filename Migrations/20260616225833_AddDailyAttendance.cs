using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Terms_TenantId_AcademicYearId",
                table: "Terms");

            migrationBuilder.DropIndex(
                name: "IX_Terms_TenantId_AcademicYearId_TermNumber",
                table: "Terms");

            migrationBuilder.CreateTable(
                name: "DailyAttendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MarkedByTeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyAttendances_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyAttendances_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyAttendances_Terms_TermId",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Terms_TenantId_AcademicYearId_TermNumber",
                table: "Terms",
                columns: new[] { "TenantId", "AcademicYearId", "TermNumber" },
                unique: true,
                filter: "\"IsCurrent\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_DailyAttendances_ClassId",
                table: "DailyAttendances",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyAttendances_StudentId",
                table: "DailyAttendances",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyAttendances_TenantId_StudentId_Date",
                table: "DailyAttendances",
                columns: new[] { "TenantId", "StudentId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyAttendances_TermId",
                table: "DailyAttendances",
                column: "TermId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyAttendances");

            migrationBuilder.DropIndex(
                name: "IX_Terms_TenantId_AcademicYearId_TermNumber",
                table: "Terms");

            migrationBuilder.CreateIndex(
                name: "IX_Terms_TenantId_AcademicYearId",
                table: "Terms",
                columns: new[] { "TenantId", "AcademicYearId" },
                unique: true,
                filter: "\"IsCurrent\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Terms_TenantId_AcademicYearId_TermNumber",
                table: "Terms",
                columns: new[] { "TenantId", "AcademicYearId", "TermNumber" },
                unique: true);
        }
    }
}

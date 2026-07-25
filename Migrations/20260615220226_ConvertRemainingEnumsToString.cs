using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolMaster.Migrations
{
    /// <inheritdoc />
    public partial class ConvertRemainingEnumsToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tenants.Status (TenantStatus)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Tenants""
                ALTER COLUMN ""Status"" TYPE TEXT
                USING CASE ""Status""::integer
                    WHEN 0 THEN 'Active'
                    WHEN 1 THEN 'Suspended'
                    WHEN 2 THEN 'Trial'
                    ELSE NULL
                END;
            ");

            // Tenants.Plan (TenantPlan)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Tenants""
                ALTER COLUMN ""Plan"" TYPE TEXT
                USING CASE ""Plan""::integer
                    WHEN 0 THEN 'Free'
                    WHEN 1 THEN 'Basic'
                    WHEN 2 THEN 'Pro'
                    ELSE NULL
                END;
            ");

            // Users.Status (UserStatus)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Users""
                ALTER COLUMN ""Status"" TYPE TEXT
                USING CASE ""Status""::integer
                    WHEN 0 THEN 'Active'
                    WHEN 1 THEN 'Inactive'
                    WHEN 2 THEN 'Suspended'
                    WHEN 3 THEN 'PendingVerification'
                    ELSE NULL
                END;
            ");

            // Staff.StaffRole (StaffRole)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Staff""
                ALTER COLUMN ""StaffRole"" TYPE TEXT
                USING CASE ""StaffRole""::integer
                    WHEN 0 THEN 'Teacher'
                    WHEN 1 THEN 'Admin'
                    WHEN 2 THEN 'Accountant'
                    WHEN 3 THEN 'Librarian'
                    ELSE NULL
                END;
            ");

            // Staff.EmploymentType (EmploymentType)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Staff""
                ALTER COLUMN ""EmploymentType"" TYPE TEXT
                USING CASE ""EmploymentType""::integer
                    WHEN 0 THEN 'FullTime'
                    WHEN 1 THEN 'PartTime'
                    WHEN 2 THEN 'Contract'
                    ELSE NULL
                END;
            ");

            // Staff.Status (StaffStatus)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Staff""
                ALTER COLUMN ""Status"" TYPE TEXT
                USING CASE ""Status""::integer
                    WHEN 0 THEN 'Active'
                    WHEN 1 THEN 'OnLeave'
                    WHEN 2 THEN 'Terminated'
                    ELSE NULL
                END;
            ");

            // Students.Gender (Gender)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Students""
                ALTER COLUMN ""Gender"" TYPE TEXT
                USING CASE ""Gender""::integer
                    WHEN 0 THEN 'Male'
                    WHEN 1 THEN 'Female'
                    WHEN 2 THEN 'Other'
                    ELSE NULL
                END;
            ");

            // Students.Status (StudentStatus)
            migrationBuilder.Sql(@"
                ALTER TABLE ""Students""
                ALTER COLUMN ""Status"" TYPE TEXT
                USING CASE ""Status""::integer
                    WHEN 0 THEN 'Active'
                    WHEN 1 THEN 'Transferred'
                    WHEN 2 THEN 'Withdrawn'
                    ELSE NULL
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Tenants""
                ALTER COLUMN ""Status"" TYPE INTEGER
                USING CASE ""Status""
                    WHEN 'Active' THEN 0
                    WHEN 'Suspended' THEN 1
                    WHEN 'Trial' THEN 2
                    ELSE NULL
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Tenants""
                ALTER COLUMN ""Plan"" TYPE INTEGER
                USING CASE ""Plan""
                    WHEN 'Free' THEN 0
                    WHEN 'Basic' THEN 1
                    WHEN 'Pro' THEN 2
                    ELSE NULL
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Users""
                ALTER COLUMN ""Status"" TYPE INTEGER
                USING CASE ""Status""
                    WHEN 'Active' THEN 0
                    WHEN 'Inactive' THEN 1
                    WHEN 'Suspended' THEN 2
                    WHEN 'PendingVerification' THEN 3
                    ELSE NULL
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Staff""
                ALTER COLUMN ""StaffRole"" TYPE INTEGER
                USING CASE ""StaffRole""
                    WHEN 'Teacher' THEN 0
                    WHEN 'Admin' THEN 1
                    WHEN 'Accountant' THEN 2
                    WHEN 'Librarian' THEN 3
                    ELSE NULL
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Staff""
                ALTER COLUMN ""EmploymentType"" TYPE INTEGER
                USING CASE ""EmploymentType""
                    WHEN 'FullTime' THEN 0
                    WHEN 'PartTime' THEN 1
                    WHEN 'Contract' THEN 2
                    ELSE NULL
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Staff""
                ALTER COLUMN ""Status"" TYPE INTEGER
                USING CASE ""Status""
                    WHEN 'Active' THEN 0
                    WHEN 'OnLeave' THEN 1
                    WHEN 'Terminated' THEN 2
                    ELSE NULL
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Students""
                ALTER COLUMN ""Gender"" TYPE INTEGER
                USING CASE ""Gender""
                    WHEN 'Male' THEN 0
                    WHEN 'Female' THEN 1
                    WHEN 'Other' THEN 2
                    ELSE NULL
                END;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Students""
                ALTER COLUMN ""Status"" TYPE INTEGER
                USING CASE ""Status""
                    WHEN 'Active' THEN 0
                    WHEN 'Transferred' THEN 1
                    WHEN 'Withdrawn' THEN 2
                    ELSE NULL
                END;
            ");
        }
    }
}

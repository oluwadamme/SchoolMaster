using SchoolMaster.Domain.Authorization;
using SchoolMaster.Domain.Enums;
using Xunit;

namespace SchoolMaster.Tests.Unit.Authorization;

public class RolePermissionsTests
{
    // -------------------------------------------------------------------------
    // Admin
    // -------------------------------------------------------------------------

    [Fact]
    public void Admin_HasStudentsCrudPermissions()
    {
        var permissions = RolePermissions.For(UserRole.Admin);

        Assert.Contains(Permission.StudentsCreate, permissions);
        Assert.Contains(Permission.StudentsRead, permissions);
        Assert.Contains(Permission.StudentsUpdate, permissions);
        Assert.Contains(Permission.StudentsDelete, permissions);
    }

    [Fact]
    public void Admin_HasStaffManagementPermissions()
    {
        var permissions = RolePermissions.For(UserRole.Admin);

        Assert.Contains(Permission.StaffCreate, permissions);
        Assert.Contains(Permission.StaffRead, permissions);
        Assert.Contains(Permission.StaffUpdate, permissions);
        Assert.Contains(Permission.StaffAssignSubject, permissions);
        Assert.Contains(Permission.StaffApproveLeave, permissions);
    }

    [Fact]
    public void Admin_HasAttendanceViewPermissions()
    {
        var permissions = RolePermissions.For(UserRole.Admin);

        Assert.Contains(Permission.AttendanceViewStudent, permissions);
        Assert.Contains(Permission.AttendanceViewClass, permissions);
        Assert.Contains(Permission.AttendanceViewReport, permissions);
        Assert.Contains(Permission.AttendanceViewAlerts, permissions);
    }

    [Fact]
    public void Admin_HasUsersDeactivatePermission()
    {
        // Critical: this permission guards PATCH /auth/users/deactivate-by-email
        var permissions = RolePermissions.For(UserRole.Admin);

        Assert.Contains(Permission.UsersDeactivate, permissions);
    }

    [Fact]
    public void Admin_DoesNotHaveAttendanceMark()
    {
        // Admins manage the school, they do not mark attendance
        var permissions = RolePermissions.For(UserRole.Admin);

        Assert.DoesNotContain(Permission.AttendanceMark, permissions);
        Assert.DoesNotContain(Permission.AttendanceBulkMark, permissions);
    }

    // -------------------------------------------------------------------------
    // Teacher
    // -------------------------------------------------------------------------

    [Fact]
    public void Teacher_HasAttendanceMarkPermissions()
    {
        var permissions = RolePermissions.For(UserRole.Teacher);

        Assert.Contains(Permission.AttendanceMark, permissions);
        Assert.Contains(Permission.AttendanceBulkMark, permissions);
    }

    [Fact]
    public void Teacher_HasReadOnlyStudentAccess()
    {
        var permissions = RolePermissions.For(UserRole.Teacher);

        Assert.Contains(Permission.StudentsRead, permissions);
        Assert.DoesNotContain(Permission.StudentsCreate, permissions);
        Assert.DoesNotContain(Permission.StudentsUpdate, permissions);
        Assert.DoesNotContain(Permission.StudentsDelete, permissions);
    }

    [Fact]
    public void Teacher_DoesNotHaveAdminPrivileges()
    {
        var permissions = RolePermissions.For(UserRole.Teacher);

        Assert.DoesNotContain(Permission.StaffApproveLeave, permissions);
        Assert.DoesNotContain(Permission.StaffCreate, permissions);
        Assert.DoesNotContain(Permission.AcademicManage, permissions);
        Assert.DoesNotContain(Permission.UsersDeactivate, permissions);
    }

    // -------------------------------------------------------------------------
    // Student
    // -------------------------------------------------------------------------

    [Fact]
    public void Student_HasOnlyTimetableViewPermission()
    {
        var permissions = RolePermissions.For(UserRole.Student);

        Assert.Contains(Permission.AcademicViewTimetable, permissions);
        Assert.Single(permissions);
    }

    [Fact]
    public void Student_HasNoWritePermissions()
    {
        var permissions = RolePermissions.For(UserRole.Student);

        Assert.DoesNotContain(Permission.StudentsCreate, permissions);
        Assert.DoesNotContain(Permission.AttendanceMark, permissions);
        Assert.DoesNotContain(Permission.UsersDeactivate, permissions);
    }

    // -------------------------------------------------------------------------
    // Parent
    // -------------------------------------------------------------------------

    [Fact]
    public void Parent_CanReadStudentsAndViewAttendance()
    {
        var permissions = RolePermissions.For(UserRole.Parent);

        Assert.Contains(Permission.StudentsRead, permissions);
        Assert.Contains(Permission.AttendanceViewStudent, permissions);
    }

    [Fact]
    public void Parent_CannotModifyStudentsOrMarkAttendance()
    {
        var permissions = RolePermissions.For(UserRole.Parent);

        Assert.DoesNotContain(Permission.StudentsCreate, permissions);
        Assert.DoesNotContain(Permission.StudentsUpdate, permissions);
        Assert.DoesNotContain(Permission.AttendanceMark, permissions);
        Assert.DoesNotContain(Permission.UsersDeactivate, permissions);
    }

    // -------------------------------------------------------------------------
    // Staff
    // -------------------------------------------------------------------------

    [Fact]
    public void Staff_CanReadOwnProfileAndSubmitLeave()
    {
        var permissions = RolePermissions.For(UserRole.Staff);

        Assert.Contains(Permission.StaffRead, permissions);
        Assert.Contains(Permission.StaffSubmitLeave, permissions);
    }

    [Fact]
    public void Staff_CannotApproveLeaveOrManageStudents()
    {
        var permissions = RolePermissions.For(UserRole.Staff);

        Assert.DoesNotContain(Permission.StaffApproveLeave, permissions);
        Assert.DoesNotContain(Permission.StudentsCreate, permissions);
        Assert.DoesNotContain(Permission.UsersDeactivate, permissions);
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void For_UnknownRole_ReturnsEmptyList()
    {
        var permissions = RolePermissions.For((UserRole)99);

        Assert.Empty(permissions);
    }

    [Fact]
    public void For_AllDefinedRoles_ReturnsNonNullList()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var permissions = RolePermissions.For(role);
            Assert.NotNull(permissions);
        }
    }
}

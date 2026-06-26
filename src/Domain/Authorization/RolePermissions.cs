using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Domain.Authorization;
// this static prevents you from being able to instantiate this class, thus providing a single source of truth
public static class RolePermissions
{
    // this static stores the data below in memory.
    private static readonly Dictionary<UserRole, IReadOnlyList<Permission>> _map = new()
    {
        [UserRole.Admin] = new[]
        {
            Permission.StudentsCreate,
            Permission.StudentsRead,
            Permission.StudentsUpdate,
            Permission.StudentsDelete,
            Permission.StudentsBulkImport,
            Permission.StudentsReadHistory,
            Permission.StudentsUploadPhoto,
            Permission.StudentsPayment,
            Permission.StudentsViewGrades,
            Permission.StaffCreate,
            Permission.StaffRead,
            Permission.StaffUpdate,
            Permission.StaffAssignSubject,
            Permission.StaffApproveLeave,
            Permission.AcademicManage,
            Permission.AcademicViewTimetable,
            Permission.AttendanceViewStudent,
            Permission.AttendanceViewClass,
            Permission.AttendanceViewReport,
            Permission.AttendanceViewAlerts,
            Permission.UsersDeactivate,
            Permission.StaffManage
        },

        [UserRole.Teacher] = new[]
        {
            Permission.StudentsRead,
            Permission.StudentsReadHistory,
            Permission.StaffRead,
            Permission.AcademicViewTimetable,
            Permission.AttendanceMark,
            Permission.AttendanceBulkMark,
            Permission.AttendanceViewStudent,
            Permission.AttendanceViewClass,
            Permission.AttendanceViewAlerts,
            Permission.StudentsPayment,
            Permission.StudentsViewGrades
        },

        [UserRole.Student] = new[]
        {
            Permission.AcademicViewTimetable
        },

        [UserRole.Parent] = new[]
        {
            Permission.StudentsRead,
            Permission.AttendanceViewStudent,
            Permission.StudentsPayment,
            Permission.StudentsViewGrades
        },

        [UserRole.Staff] = new[]
        {
            Permission.StaffRead,
            Permission.StaffSubmitLeave,
            Permission.StudentsPayment,
            Permission.StudentsViewGrades
        }
    };

    public static IReadOnlyList<Permission> For(UserRole role)
    {
        return _map.TryGetValue(role, out var permissions)
            ? permissions
            : Array.Empty<Permission>();
    }
}

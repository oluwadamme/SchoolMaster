namespace SchoolMaster.Domain.Enums;

public enum Permission
{
    // Students
    StudentsCreate,
    StudentsRead,
    StudentsUpdate,
    StudentsDelete,
    StudentsBulkImport,
    StudentsReadHistory,
    StudentsUploadPhoto,
    StudentsPayment,
    StudentsViewGrades,

    // Staff
    StaffCreate,
    StaffRead,
    StaffUpdate,
    StaffAssignSubject,
    StaffSubmitLeave,
    StaffApproveLeave,

    // Academic
    AcademicManage,
    AcademicViewTimetable,

    // Attendance
    AttendanceMark,
    AttendanceBulkMark,
    AttendanceViewStudent,
    AttendanceViewClass,
    AttendanceViewReport,
    AttendanceViewAlerts,

    UsersDeactivate
}

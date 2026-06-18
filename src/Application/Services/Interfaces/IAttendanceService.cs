using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Application.Services.Interfaces;

public interface IAttendanceService
{
    Task<BaseResponse<MarkAttendanceResponse>> MarkAttendanceAsync(MarkAttendanceRequest request);
    Task<BaseResponse<ClassAttendanceResponse>> GetClassAttendanceAsync(Guid classId, DateOnly date);
    Task<BaseResponse<StudentAttendanceSummaryResponse>> GetStudentAttendanceSummaryAsync(
        Guid studentId, Guid? termId, DateOnly? from, DateOnly? to);
}

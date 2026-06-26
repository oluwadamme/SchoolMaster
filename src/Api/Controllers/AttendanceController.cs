using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Api.Authorization;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AttendanceController(IAttendanceService attendanceService) : ControllerBase
{
    /// <summary>Mark attendance for a class</summary>
    /// <remarks>
    /// Marks daily attendance for one or more students in a class. All student IDs must belong to the specified class.
    /// Submitting a record for a student who was already marked that day updates their status (upsert behaviour).
    /// Re-marking an already-absent student as absent does not send a duplicate notification.
    /// An absence notification email is automatically queued for each student newly marked Absent.
    /// </remarks>
    [HasPermission(Permission.AttendanceMark)]
    [HttpPost]
    public async Task<ActionResult<BaseResponse<MarkAttendanceResponse>>> MarkAttendance(
        [FromBody] MarkAttendanceRequest request)
    {
        var result = await attendanceService.MarkAttendanceAsync(request);
        return Ok(result);
    }

    /// <summary>Get class attendance for a specific date</summary>
    /// <remarks>
    /// Returns all attendance records for the given class on the given date.
    /// Returns an empty records list (not 404) if no attendance has been marked yet for that date.
    /// </remarks>
    [HasPermission(Permission.AttendanceViewClass)]
    [HttpGet("class/{classId:guid}")]
    public async Task<ActionResult<BaseResponse<ClassAttendanceResponse>>> GetClassAttendance(
        Guid classId, [FromQuery] DateOnly date)
    {
        var result = await attendanceService.GetClassAttendanceAsync(classId, date);
        return Ok(result);
    }

    /// <summary>Get attendance summary for a student</summary>
    /// <remarks>
    /// Returns a summary (total days, present, absent, late, excused, attendance percentage) and the full record list.
    /// Optionally filter by <c>termId</c> to scope results to a specific academic term.
    /// Optionally filter by date range using <c>from</c> and <c>to</c> query parameters (format: yyyy-MM-dd).
    /// Attendance percentage treats Present, Late, and Excused as attended days.
    /// </remarks>
    [HasPermission(Permission.AttendanceViewStudent)]
    [HttpGet("student/{studentId:guid}")]
    public async Task<ActionResult<BaseResponse<StudentAttendanceSummaryResponse>>> GetStudentAttendanceSummary(
        Guid studentId,
        [FromQuery] Guid? termId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var result = await attendanceService.GetStudentAttendanceSummaryAsync(studentId, termId, from, to);
        return Ok(result);
    }
}

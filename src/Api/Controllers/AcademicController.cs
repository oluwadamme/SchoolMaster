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
public class AcademicController : ControllerBase
{
    private readonly IAcademicService _academicService;

    public AcademicController(IAcademicService academicService)
    {
        _academicService = academicService;
    }

    /// <summary>Create a new academic year</summary>
    /// <remarks>
    /// Creates an academic year for the current school (tenant). The name must be unique within the school (e.g. "2025/2026").
    /// Set <c>SetAsCurrent = true</c> to make this the active year — the previously current year is automatically demoted.
    /// Only one academic year can be current at a time.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPost("years")]
    public async Task<ActionResult<BaseResponse<AcademicYearResponse>>> CreateAcademicYear(
        [FromBody] CreateAcademicYearRequest request)
    {
        var result = await _academicService.CreateAcademicYearAsync(request);
        return CreatedAtAction(nameof(GetAcademicYears), null, result);
    }

    /// <summary>List all academic years</summary>
    /// <remarks>
    /// Returns a paginated list of all academic years for the current school, ordered most recent first.
    /// Use <c>page</c> and <c>pageSize</c> (max 100) to control the page. Defaults: page 1, pageSize 20.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpGet("years")]
    public async Task<ActionResult<BaseResponse<PagedResponse<AcademicYearResponse>>>> GetAcademicYears(
        [FromQuery] PaginationRequest pagination)
    {
        var result = await _academicService.GetAcademicYearsAsync(pagination.Page, pagination.PageSize);
        return Ok(result);
    }

    /// <summary>Partially update an academic year</summary>
    /// <remarks>
    /// Only fields included in the request body are updated — omitted fields keep their current value.
    /// If you change the date range, all existing terms must still fit within the new window, otherwise the request is rejected.
    /// Sending <c>SetAsCurrent = true</c> promotes this year to the active year (old one is demoted automatically).
    /// Sending <c>SetAsCurrent = false</c> has no effect — you cannot demote a year without promoting another.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPatch("years/{yearId:guid}")]
    public async Task<ActionResult<BaseResponse<AcademicYearResponse>>> UpdateAcademicYear(
        Guid yearId, [FromBody] UpdateAcademicYearRequest request)
    {
        var result = await _academicService.UpdateAcademicYearAsync(yearId, request);
        return Ok(result);
    }

    /// <summary>Create a new term within an academic year</summary>
    /// <remarks>
    /// Terms belong to a specific academic year (identified by <c>AcademicYearId</c> in the request body).
    /// Term dates must fall entirely within the parent academic year's date range.
    /// No two terms within the same year may have overlapping dates.
    /// <c>TermNumber</c> is used for ordering and must be 1, 2, or 3.
    /// Set <c>SetAsCurrent = true</c> to make this the active term for the year — the previously current term is auto-demoted.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPost("terms")]
    public async Task<ActionResult<BaseResponse<TermResponse>>> CreateTerm(
        [FromBody] CreateTermRequest request)
    {
        var result = await _academicService.CreateTermAsync(request);
        return CreatedAtAction(nameof(GetTermsByYear), new { yearId = result.Data?.AcademicYearId }, result);
    }

    /// <summary>List all terms for an academic year</summary>
    /// <remarks>
    /// Returns all terms belonging to the specified academic year, ordered by term number.
    /// Returns 404 if the academic year does not exist or does not belong to the current school.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpGet("years/{yearId:guid}/terms")]
    // In the route string "years/{yearId:guid}/terms" the part {yearId:guid} is a placeholder.
    // It tells the web framework: “When a request comes in, there will be a value in this position, and I want to capture it.”
    public async Task<ActionResult<BaseResponse<List<TermResponse>>>> GetTermsByYear(Guid yearId)
    {
        var result = await _academicService.GetTermsByYearAsync(yearId);
        return Ok(result);
    }

    /// <summary>Partially update a term</summary>
    /// <remarks>
    /// Only fields included in the request body are updated. The updated dates must still fall within the parent academic year's range
    /// and must not overlap any other term in the same year.
    /// Sending <c>SetAsCurrent = true</c> promotes this term to the active term for its year.
    /// Sending <c>SetAsCurrent = false</c> has no effect.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPatch("terms/{termId:guid}")]
    public async Task<ActionResult<BaseResponse<TermResponse>>> UpdateTerm(
        Guid termId, [FromBody] UpdateTermRequest request)
    {
        var result = await _academicService.UpdateTermAsync(termId, request);
        return Ok(result);
    }

    /// <summary>Create a new class</summary>
    /// <remarks>
    /// Creates a class (e.g. "JSS 1A") for the current school. Class names must be unique within the school.
    /// <c>FormTeacherId</c> is optional — it references a staff member's ID. It can be assigned now or added later via PATCH.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPost("classes")]
    public async Task<ActionResult<BaseResponse<ClassResponse>>> CreateClass(
        [FromBody] CreateClassRequest request)
    {
        var result = await _academicService.CreateClassAsync(request);
        return CreatedAtAction(nameof(GetClasses), null, result);
    }

    /// <summary>List all classes</summary>
    /// <remarks>
    /// Returns a paginated list of classes for the current school.
    /// Use <c>page</c> and <c>pageSize</c> (max 100) to control the page. Defaults: page 1, pageSize 20.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpGet("classes")]
    public async Task<ActionResult<BaseResponse<PagedResponse<ClassResponse>>>> GetClasses(
        [FromQuery] PaginationRequest pagination)
    {
        var result = await _academicService.GetClassesAsync(pagination.Page, pagination.PageSize);
        return Ok(result);
    }

    /// <summary>Partially update a class</summary>
    /// <remarks>
    /// Only fields included in the request body are updated.
    /// To assign a form teacher, send <c>"formTeacherId": "&lt;staff-id&gt;"</c>.
    /// To clear the form teacher, send <c>"formTeacherId": null</c> explicitly.
    /// Omitting <c>formTeacherId</c> entirely leaves the current value unchanged.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPatch("classes/{classId:guid}")]
    public async Task<ActionResult<BaseResponse<ClassResponse>>> UpdateClass(
        Guid classId, [FromBody] UpdateClassRequest request)
        
    {
        var result = await _academicService.UpdateClassAsync(classId, request);
        return Ok(result);
    }

    /// <summary>Create a new subject</summary>
    /// <remarks>
    /// Creates a subject (e.g. "Mathematics") for the current school. Subject names must be unique within the school.
    /// <c>Code</c> is an optional short identifier (e.g. "MTH", max 20 characters) useful for timetable display.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPost("subjects")]
    public async Task<ActionResult<BaseResponse<SubjectResponse>>> CreateSubject(
        [FromBody] CreateSubjectRequest request)
    {
        var result = await _academicService.CreateSubjectAsync(request);
        return CreatedAtAction(nameof(GetSubjects), null, result);
    }

    /// <summary>List all subjects</summary>
    /// <remarks>
    /// Returns a paginated list of subjects for the current school.
    /// Use <c>page</c> and <c>pageSize</c> (max 100) to control the page. Defaults: page 1, pageSize 20.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpGet("subjects")]
    public async Task<ActionResult<BaseResponse<PagedResponse<SubjectResponse>>>> GetSubjects(
        [FromQuery] PaginationRequest pagination)
    {
        var result = await _academicService.GetSubjectsAsync(pagination.Page, pagination.PageSize);
        return Ok(result);
    }

    /// <summary>Partially update a subject</summary>
    /// <remarks>
    /// Only fields included in the request body are updated.
    /// To assign a subject code, send <c>"code": "MTH"</c>.
    /// To clear the code, send <c>"code": null</c> explicitly.
    /// Omitting <c>code</c> entirely leaves the current value unchanged.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPatch("subjects/{subjectId:guid}")]
    public async Task<ActionResult<BaseResponse<SubjectResponse>>> UpdateSubject(
        Guid subjectId, [FromBody] UpdateSubjectRequest request)
    {
        var result = await _academicService.UpdateSubjectAsync(subjectId, request);
        return Ok(result);
    }

    /// <summary>Add a period to a class timetable</summary>
    /// <remarks>
    /// Adds a time slot to a class's weekly timetable. There are three period types:
    ///
    /// - **Timetabled** — a regular lesson. Requires <c>SubjectId</c> and <c>TeacherId</c>. The period name defaults to the subject name if omitted.
    /// - **DailyRegister** — morning/afternoon registration. No subject or teacher required. Name defaults to "Morning Register".
    /// - **NonAcademic** — break, lunch, assembly, etc. No subject or teacher required. <c>Name</c> is required (e.g. "Lunch Break").
    ///
    /// No two periods for the same class on the same day may have overlapping time slots.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPost("classes/{classId:guid}/periods")]
    public async Task<ActionResult<BaseResponse<PeriodResponse>>> CreatePeriod(
        Guid classId,
        [FromBody] CreatePeriodRequest request)
    {
        var result = await _academicService.CreatePeriodAsync(classId, request);
        return CreatedAtAction(nameof(GetTimetable), new { classId }, result);
    }

    /// <summary>Get the full timetable for a class</summary>
    /// <remarks>
    /// Returns all periods for the specified class, ordered by day of week then start time.
    /// This endpoint is accessible to any authenticated user with the <c>AcademicViewTimetable</c> permission,
    /// which includes teachers and students — not just admin users.
    /// </remarks>
    [HasPermission(Permission.AcademicViewTimetable)]
    [HttpGet("classes/{classId:guid}/timetable")]
    public async Task<ActionResult<BaseResponse<TimetableResponse>>> GetTimetable(Guid classId)
    {
        var result = await _academicService.GetTimetableAsync(classId);
        return Ok(result);
    }

    /// <summary>Partially update a period</summary>
    /// <remarks>
    /// Only fields included in the request body are updated.
    /// If <c>Type</c> changes to <c>DailyRegister</c> or <c>NonAcademic</c>, the subject is automatically cleared.
    /// To clear the assigned teacher, send <c>"teacherId": null</c> explicitly — omitting it keeps the current value.
    /// The updated time slot must not overlap any other period on the same day for this class.
    /// </remarks>
    [HasPermission(Permission.AcademicManage)]
    [HttpPatch("classes/{classId:guid}/periods/{periodId:guid}")]
    public async Task<ActionResult<BaseResponse<PeriodResponse>>> UpdatePeriod(
        Guid classId, Guid periodId, [FromBody] UpdatePeriodRequest request)
    {
        var result = await _academicService.UpdatePeriodAsync(classId, periodId, request);
        return Ok(result);
    }
}

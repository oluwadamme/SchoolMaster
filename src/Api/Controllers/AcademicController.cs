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

    [HasPermission(Permission.AcademicManage)]
    [HttpPost("years")]
    public async Task<ActionResult<BaseResponse<AcademicYearResponse>>> CreateAcademicYear(
        [FromBody] CreateAcademicYearRequest request)
    {
        var result = await _academicService.CreateAcademicYearAsync(request);
        return CreatedAtAction(nameof(GetAcademicYears), null, result);
    }

    [HasPermission(Permission.AcademicManage)]
    [HttpGet("years")]
    public async Task<ActionResult<BaseResponse<PagedResponse<AcademicYearResponse>>>> GetAcademicYears(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _academicService.GetAcademicYearsAsync(page, pageSize);
        return Ok(result);
    }

    [HasPermission(Permission.AcademicManage)]
    [HttpPost("terms")]
    public async Task<ActionResult<BaseResponse<TermResponse>>> CreateTerm(
        [FromBody] CreateTermRequest request)
    {
        var result = await _academicService.CreateTermAsync(request);
        return CreatedAtAction(nameof(GetTermsByYear), new { yearId = result.Data?.AcademicYearId }, result);
    }

    [HasPermission(Permission.AcademicManage)]
    [HttpGet("years/{yearId:guid}/terms")]
    public async Task<ActionResult<BaseResponse<List<TermResponse>>>> GetTermsByYear(Guid yearId)
    {
        var result = await _academicService.GetTermsByYearAsync(yearId);
        return Ok(result);
    }

    [HasPermission(Permission.AcademicManage)]
    [HttpPost("classes")]
    public async Task<ActionResult<BaseResponse<ClassResponse>>> CreateClass(
        [FromBody] CreateClassRequest request)
    {
        var result = await _academicService.CreateClassAsync(request);
        return CreatedAtAction(nameof(GetClasses), null, result);
    }

    [HasPermission(Permission.AcademicManage)]
    [HttpGet("classes")]
    public async Task<ActionResult<BaseResponse<PagedResponse<ClassResponse>>>> GetClasses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _academicService.GetClassesAsync(page, pageSize);
        return Ok(result);
    }

    [HasPermission(Permission.AcademicManage)]
    [HttpPost("subjects")]
    public async Task<ActionResult<BaseResponse<SubjectResponse>>> CreateSubject(
        [FromBody] CreateSubjectRequest request)
    {
        var result = await _academicService.CreateSubjectAsync(request);
        return CreatedAtAction(nameof(GetSubjects), null, result);
    }

    [HasPermission(Permission.AcademicManage)]
    [HttpGet("subjects")]
    public async Task<ActionResult<BaseResponse<PagedResponse<SubjectResponse>>>> GetSubjects(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _academicService.GetSubjectsAsync(page, pageSize);
        return Ok(result);
    }

    [HasPermission(Permission.AcademicManage)]
    [HttpPost("classes/{classId:guid}/periods")]
    public async Task<ActionResult<BaseResponse<TimetableResponse>>> CreatePeriod(
        Guid classId,
        [FromBody] CreatePeriodRequest request)
    {
        var result = await _academicService.CreatePeriodAsync(classId, request);
        return CreatedAtAction(nameof(GetTimetable), new { classId }, result);
    }

    [HasPermission(Permission.AcademicViewTimetable)]
    [HttpGet("classes/{classId:guid}/timetable")]
    public async Task<ActionResult<BaseResponse<TimetableResponse>>> GetTimetable(Guid classId)
    {
        var result = await _academicService.GetTimetableAsync(classId);
        return Ok(result);
    }


}

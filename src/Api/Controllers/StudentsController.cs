using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Api.Authorization;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/v1/students")]
[HasPermission(Permission.IsEmailVerified)]// Checks JWT token and ensures the user's email is verified before they can acces
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpPost]
    [HasPermission(Permission.StudentsCreate)] // Only Admins can enroll students
    public async Task<ActionResult<BaseResponse<StudentResponse>>> CreateStudent([FromBody] CreateStudentRequest request)
    {
        var response = await _studentService.CreateStudentAsync(request);
        
        return Ok(response);
    }

    [HttpPost("bulk")]
    [HasPermission(Permission.StudentsCreate)] // Bulk enrollment for admins
    // IEnumerable<T> is a .NET interface that represents any read‑only collection of items of type T
    public async Task<ActionResult<BaseResponse<IReadOnlyList<StudentResponse>>>> EnrollStudentsBulk([FromBody] IEnumerable<CreateStudentRequest> requests)
    {
        var response = await _studentService.EnrollStudentsBulkAsync(requests);
        return Ok(response);
    }

    /// <summary>
    /// Updates an existing student.
    /// </summary>
    [HttpPut]
    [HasPermission(Permission.StudentsUpdate)]
    public async Task<ActionResult<BaseResponse<StudentResponse>>> UpdateStudent([FromBody] UpdateStudentRequest request)
    {
        var response = await _studentService.UpdateStudentAsync(request);
        return Ok(response);
    }
}
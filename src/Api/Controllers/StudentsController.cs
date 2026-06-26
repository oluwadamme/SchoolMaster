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

}
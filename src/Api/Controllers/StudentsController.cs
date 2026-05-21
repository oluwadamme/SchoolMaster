using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Requires a valid JWT token
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")] // Only Admins can enroll students
    public async Task<ActionResult<BaseResponse<StudentResponse>>> CreateStudent([FromBody] CreateStudentRequest request)
    {
        var response = await _studentService.CreateStudentAsync(request);
        
        return Ok(response);
    }

}
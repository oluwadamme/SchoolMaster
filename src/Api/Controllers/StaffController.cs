using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/v1/staff")]
[Authorize(Roles = "Admin")]
public class StaffController : ControllerBase
{
    private readonly IStaffService _staffService;

    public StaffController(IStaffService staffService)
    {
        _staffService = staffService;
    }

    /// <summary>
    /// Creates a new staff member and their associated user account.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BaseResponse<StaffResponse>>> CreateStaff([FromBody] CreateStaffRequest request)
    {
        var response = await _staffService.CreateStaffAsync(request);
        
        return CreatedAtAction(nameof(CreateStaff), new { id = response.Data?.Id }, response);
    }
}
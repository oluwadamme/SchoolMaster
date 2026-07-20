using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Api.Authorization;
using SchoolMaster.Domain.Enums;
using System.Collections.Generic;

namespace SchoolMaster.Api.Controllers;

[ApiController]
[Route("api/v1/staff")]
[HasPermission(Permission.IsEmailVerified)] // User must have verified their email
[HasPermission(Permission.StaffManage)]     // Restricts access to Admins
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
    // when a request is made to this method, The ASP.NET Core system looks at the HasPermissionAttribute object
    // sitting in memory for that exact method, and reads the text string stored inside it
    // the system takes that string and gets the HasPermissionRequirement object that matches the string
    // it then passes that object to the HandleRequirementAsync method in the HasPermissionHandler class
    
    [HasPermission(Permission.StaffManage)]
    public async Task<ActionResult<BaseResponse<StaffResponse>>> CreateStaff([FromBody] CreateStaffRequest request)
    {
        var response = await _staffService.CreateStaffAsync(request);
        
        return CreatedAtAction(nameof(CreateStaff), new { id = response.Data?.Id }, response);
    }

    [HttpPost("bulk")]
    [HasPermission(Permission.StaffManage)] // Bulk enrollment for admins
    public async Task<ActionResult<BaseResponse<BulkEnrollmentResult>>> EnrollStaffBulk([FromBody] BulkEnrollStaffRequest requests)
    {
        var response = await _staffService.EnrollStaffBulkAsync(requests);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves all staff members for the current tenant.
    /// </summary>
    [HttpGet]
    [HasPermission(Permission.StaffManage)]
    public async Task<ActionResult<BaseResponse<PagedResponse<StaffResponse>>>> GetAllStaff([FromQuery] PaginationRequest pagination)
    {
        var response = await _staffService.GetAllStaffAsync(pagination.Page, pagination.PageSize);
        return Ok(response);
    }

    /// <summary>
    /// Resends the 7-day invitation email to a staff member.
    /// </summary>
    [HttpPost("resend-invitation")]
    [HasPermission(Permission.StaffManage)]
    public async Task<ActionResult<BaseResponse<bool>>> ResendInvitation([FromBody] ResendOtpRequest request)
    {
        var response = await _staffService.ResendStaffInvitationAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Updates an existing staff member.
    /// </summary>
    [HttpPut]
    [HasPermission(Permission.StaffUpdate)]
    public async Task<ActionResult<BaseResponse<StaffResponse>>> UpdateStaff([FromBody] UpdateStaffRequest request)
    {
        var response = await _staffService.UpdateStaffAsync(request);
        return Ok(response);
    }
}
namespace SchoolMaster.Api.Controllers;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolMaster.Api.Authorization;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.Enums;

[ApiController]
[Route("api/v1/auditlogs")]
[HasPermission(Permission.IsEmailVerified)]
[HasPermission(Permission.StaffManage)] // Restrict to admins (or whoever has StaffManage)
public class AuditLogsController(IAuditLogService _auditLogService) : ControllerBase
{
    /// <summary>
    /// Retrieves all audit logs for the current tenant.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLogResponse>>> GetAllLogs()
    {
        var logs = await _auditLogService.GetAllAsync();
        return Ok(logs);
    }
}

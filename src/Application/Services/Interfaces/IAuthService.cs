namespace SchoolMaster.Application.Services.Interfaces;

using System;
using System.Threading.Tasks;
using SchoolMaster.Application.DTOs;

public interface IAuthService
{
    Task<BaseResponse<Guid>> CreateAdminAsync(CreateAdminRequest request, Guid tenantId);
}

namespace SchoolMaster.Application.Services.Interfaces;

using SchoolMaster.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IStaffService
{
    Task<BaseResponse<StaffResponse>> CreateStaffAsync(CreateStaffRequest request);
    Task<BaseResponse<bool>> ResendStaffInvitationAsync(ResendOtpRequest request);
    Task<BaseResponse<StaffResponse>> UpdateStaffAsync(UpdateStaffRequest request);
    Task<BaseResponse<BulkEnrollmentResult>> EnrollStaffBulkAsync(BulkEnrollStaffRequest requests);
    Task<BaseResponse<PagedResponse<StaffResponse>>> GetAllStaffAsync(int page, int pageSize);
}
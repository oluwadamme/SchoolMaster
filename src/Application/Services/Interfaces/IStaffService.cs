namespace SchoolMaster.Application.Services.Interfaces;

using SchoolMaster.Application.DTOs;
using System.Threading.Tasks;

public interface IStaffService
{
    Task<BaseResponse<StaffResponse>> CreateStaffAsync(CreateStaffRequest request);
    Task<BaseResponse<bool>> ResendStaffInvitationAsync(ResendOtpRequest request);
    Task<BaseResponse<StaffResponse>> UpdateStaffAsync(UpdateStaffRequest request);
}
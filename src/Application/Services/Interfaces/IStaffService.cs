namespace SchoolMaster.Application.Services.Interfaces;

using SchoolMaster.Application.DTOs;
using System.Threading.Tasks;

public interface IStaffService
{
    Task<BaseResponse<StaffResponse>> CreateStaffAsync(CreateStaffRequest request);
}
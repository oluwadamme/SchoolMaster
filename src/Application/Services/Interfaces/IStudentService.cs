using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Application.Services.Interfaces;

public interface IStudentService
{
    Task<BaseResponse<StudentResponse>> CreateStudentAsync(CreateStudentRequest request);
}
using SchoolMaster.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SchoolMaster.Application.Services.Interfaces;

public interface IStudentService
{
    Task<BaseResponse<PagedResponse<StudentResponse>>> GetAllStudentsAsync(int page, int pageSize);
    Task<BaseResponse<StudentResponse>> CreateStudentAsync(CreateStudentRequest request);
    Task<BaseResponse<StudentResponse>> UpdateStudentAsync(UpdateStudentRequest request);
    Task<BaseResponse<BulkEnrollmentResult>> EnrollStudentsBulkAsync(BulkEnrollStudentsRequest requests);
}
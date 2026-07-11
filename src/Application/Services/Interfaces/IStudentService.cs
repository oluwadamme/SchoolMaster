using SchoolMaster.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SchoolMaster.Application.Services.Interfaces;

public interface IStudentService
{
    Task<BaseResponse<StudentResponse>> CreateStudentAsync(CreateStudentRequest request);
    Task<BaseResponse<StudentResponse>> UpdateStudentAsync(UpdateStudentRequest request);
    Task<BaseResponse<IReadOnlyList<StudentResponse>>> EnrollStudentsBulkAsync(BulkEnrollStudentsRequest requests);
}
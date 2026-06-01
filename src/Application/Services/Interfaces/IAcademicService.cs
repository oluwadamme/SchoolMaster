// src/Application/Services/Interfaces/IAcademicService.cs
namespace SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Application.DTOs;

public interface IAcademicService
{
    Task<BaseResponse<AcademicYearResponse>> CreateAcademicYearAsync(CreateAcademicYearRequest request);
    Task<BaseResponse<List<AcademicYearResponse>>> GetAcademicYearsAsync();

    Task<BaseResponse<TermResponse>> CreateTermAsync(CreateTermRequest request);

    Task<BaseResponse<ClassResponse>> CreateClassAsync(CreateClassRequest request);
    Task<BaseResponse<List<ClassResponse>>> GetClassesAsync();

    Task<BaseResponse<SubjectResponse>> CreateSubjectAsync(CreateSubjectRequest request);

    Task<BaseResponse<TimetableResponse>> CreatePeriodAsync(CreatePeriodRequest request);
    Task<BaseResponse<TimetableResponse>> GetTimetableAsync(Guid classId);
}

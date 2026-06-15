// src/Application/Services/Interfaces/IAcademicService.cs
using SchoolMaster.Application.DTOs;

namespace SchoolMaster.Application.Services.Interfaces;

public interface IAcademicService
{
    Task<BaseResponse<AcademicYearResponse>> CreateAcademicYearAsync(CreateAcademicYearRequest request);
    Task<BaseResponse<PagedResponse<AcademicYearResponse>>> GetAcademicYearsAsync(int page, int pageSize);

    Task<BaseResponse<TermResponse>> CreateTermAsync(CreateTermRequest request);
    Task<BaseResponse<List<TermResponse>>> GetTermsByYearAsync(Guid academicYearId);

    Task<BaseResponse<ClassResponse>> CreateClassAsync(CreateClassRequest request);
    Task<BaseResponse<PagedResponse<ClassResponse>>> GetClassesAsync(int page, int pageSize);

    Task<BaseResponse<SubjectResponse>> CreateSubjectAsync(CreateSubjectRequest request);
    Task<BaseResponse<PagedResponse<SubjectResponse>>> GetSubjectsAsync(int page, int pageSize);

    Task<BaseResponse<TimetableResponse>> CreatePeriodAsync(Guid classId, CreatePeriodRequest request);
    Task<BaseResponse<TimetableResponse>> GetTimetableAsync(Guid classId);
}

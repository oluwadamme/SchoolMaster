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

    Task<BaseResponse<PeriodResponse>> CreatePeriodAsync(Guid classId, CreatePeriodRequest request);
    Task<BaseResponse<TimetableResponse>> GetTimetableAsync(Guid classId);

    Task<BaseResponse<AcademicYearResponse>> UpdateAcademicYearAsync(Guid yearId, UpdateAcademicYearRequest request);
    Task<BaseResponse<TermResponse>> UpdateTermAsync(Guid termId, UpdateTermRequest request);
    Task<BaseResponse<ClassResponse>> UpdateClassAsync(Guid classId, UpdateClassRequest request);
    Task<BaseResponse<SubjectResponse>> UpdateSubjectAsync(Guid subjectId, UpdateSubjectRequest request);
    Task<BaseResponse<PeriodResponse>> UpdatePeriodAsync(Guid classId, Guid periodId, UpdatePeriodRequest request);
}

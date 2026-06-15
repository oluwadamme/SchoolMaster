using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.CustomException;

namespace SchoolMaster.Application.Services;

public class AcademicService : IAcademicService
{

    private readonly IAcademicYearRepository _academicYearRepository;
    private readonly IClassRepository _classRepo;
    private readonly ISubjectRepository _subjectRepo;
    private readonly IPeriodRepository _periodRepo;
    private readonly ICurrentTenant _currentTenant;

    public AcademicService(
        IAcademicYearRepository academicYearRepository,
        IClassRepository classRepository,
        ISubjectRepository subjectRepository,
        IPeriodRepository periodRepository,
        ICurrentTenant currentTenant
    )
    {
        _academicYearRepository = academicYearRepository;
        _classRepo = classRepository;
        _subjectRepo = subjectRepository;
        _periodRepo = periodRepository;
        _currentTenant = currentTenant;
    }

    public async Task<BaseResponse<AcademicYearResponse>> CreateAcademicYearAsync(CreateAcademicYearRequest request)
    {
        var tenantId = _currentTenant.Id;

        if (await _academicYearRepository.ExistsByNameAsync(request.Name))
        {
            throw new DuplicateAcademicYearException($"An academic year named '{request.Name}' already exists.");
        }

        // If the new academic year is to be set as current, unset the existing current academic year
        if (request.SetAsCurrent)
        {
            var existing = await _academicYearRepository.GetCurrentAsync();
            if (existing != null)
            {
                existing.UnsetCurrent();
                await _academicYearRepository.UpdateAsync(existing);
            }
        }
        var year = AcademicYear.Create(tenantId, request.Name, request.StartDate, request.EndDate, request.SetAsCurrent);
        await _academicYearRepository.AddAsync(year);
        var response = new AcademicYearResponse(year.Id, year.Name, year.StartDate, year.EndDate, year.IsCurrent);
        return BaseResponse<AcademicYearResponse>.SuccessResponse("Academic year created successfully", response);

    }

    public async Task<BaseResponse<PagedResponse<AcademicYearResponse>>> GetAcademicYearsAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (items, totalCount) = await _academicYearRepository.GetAllAsync(page, pageSize);
        var mapped = items.Select(y =>
            new AcademicYearResponse(y.Id, y.Name, y.StartDate, y.EndDate, y.IsCurrent)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var paged = new PagedResponse<AcademicYearResponse>(mapped, page, pageSize, totalCount, totalPages);
        return BaseResponse<PagedResponse<AcademicYearResponse>>.SuccessResponse("Academic years retrieved", paged);
    }

    public async Task<BaseResponse<TermResponse>> CreateTermAsync(CreateTermRequest request)
    {
        var tenantId = _currentTenant.Id;

        var year = await _academicYearRepository.GetByIdAsync(request.AcademicYearId);
        if (year == null)
        {
            throw new AcademicYearNotFoundException($"Academic year {request.AcademicYearId} not found.");
        }
        // If the new term is to be set as current, unset the existing current term for that academic year
        if (request.SetAsCurrent)
        {
            var existing = await _academicYearRepository.GetCurrentTermAsync(request.AcademicYearId);
            if (existing != null)
            {
                existing.UnsetCurrent();
                await _academicYearRepository.UpdateTermAsync(existing);
            }
        }

        var term = Term.Create(tenantId, request.AcademicYearId, request.Name,
            request.TermNumber, request.StartDate, request.EndDate, request.SetAsCurrent);
        await _academicYearRepository.AddTermAsync(term);
        var response = new TermResponse(term.Id, term.AcademicYearId, term.Name, term.TermNumber,
            term.StartDate, term.EndDate, term.IsCurrent);
        return BaseResponse<TermResponse>.SuccessResponse(
            "Term created successfully",
            response);
    }

    public async Task<BaseResponse<List<TermResponse>>> GetTermsByYearAsync(Guid academicYearId)
    {
        _ = await _academicYearRepository.GetByIdAsync(academicYearId)
            ?? throw new AcademicYearNotFoundException($"Academic year {academicYearId} not found.");

        var terms = await _academicYearRepository.GetTermsByYearAsync(academicYearId);
        var response = terms.Select(t =>
            new TermResponse(t.Id, t.AcademicYearId, t.Name, t.TermNumber, t.StartDate, t.EndDate, t.IsCurrent))
            .ToList();
        return BaseResponse<List<TermResponse>>.SuccessResponse("Terms retrieved", response);
    }

    public async Task<BaseResponse<ClassResponse>> CreateClassAsync(CreateClassRequest request)
    {
        var tenantId = _currentTenant.Id;

        if (await _classRepo.ExistsByNameAsync(request.Name))
        {
            throw new DuplicateClassNameException($"A class named '{request.Name}' already exists.");
        }

        var cls = Class.Create(tenantId, request.Name, request.FormTeacherId);
        await _classRepo.AddAsync(cls);

        var response = new ClassResponse(cls.Id, cls.Name, cls.FormTeacherId);

        return BaseResponse<ClassResponse>.SuccessResponse(
            "Class created successfully",
            response);
    }

    public async Task<BaseResponse<PagedResponse<ClassResponse>>> GetClassesAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;
        var (items, totalCount) = await _classRepo.GetAllAsync(page, pageSize);
        var mapped = items.Select(c => new ClassResponse(c.Id, c.Name, c.FormTeacherId)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var paged = new PagedResponse<ClassResponse>(mapped, page, pageSize, totalCount, totalPages);
        return BaseResponse<PagedResponse<ClassResponse>>.SuccessResponse("Classes retrieved", paged);
    }

    public async Task<BaseResponse<SubjectResponse>> CreateSubjectAsync(CreateSubjectRequest request)
    {
        var tenantId = _currentTenant.Id;
        if (await _subjectRepo.ExistsByNameAsync(request.Name))
        {
            throw new DuplicateSubjectCodeException($"A subject with name '{request.Name}' already exists.");
        }
        var subject = Subject.Create(tenantId, request.Name, request.Code);
        await _subjectRepo.AddAsync(subject);

        var response = new SubjectResponse(subject.Id, subject.Name, subject.Code);
        return BaseResponse<SubjectResponse>.SuccessResponse(
            "Subject created successfully",
            response);
    }

    public async Task<BaseResponse<PagedResponse<SubjectResponse>>> GetSubjectsAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (items, totalCount) = await _subjectRepo.GetAllAsync(page, pageSize);
        var mapped = items.Select(s => new SubjectResponse(s.Id, s.Name, s.Code)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var paged = new PagedResponse<SubjectResponse>(mapped, page, pageSize, totalCount, totalPages);
        return BaseResponse<PagedResponse<SubjectResponse>>.SuccessResponse("Subjects retrieved", paged);
    }

    public async Task<BaseResponse<TimetableResponse>> CreatePeriodAsync(Guid classId, CreatePeriodRequest request)
    {
        var tenantId = _currentTenant.Id;

        var cls = await _classRepo.GetByIdAsync(classId)
            ?? throw new ClassNotFoundException($"Class {classId} not found.");

        if (request.SubjectId.HasValue)
        {
            _ = await _subjectRepo.GetByIdAsync(request.SubjectId.Value)
                ?? throw new SubjectNotFoundException($"Subject {request.SubjectId} not found.");
        }

        var period = Period.Create(tenantId, classId, request.SubjectId, request.TeacherId,
            request.DayOfWeek, request.StartTime, request.EndTime, request.Type, request.Name);
        await _periodRepo.AddAsync(period);

        var allPeriods = await _periodRepo.GetPeriodsByClassIdAsync(classId);
        var periodResponses = allPeriods
            .OrderBy(p => p.DayOfWeek)
            .ThenBy(p => p.StartTime)
            .Select(p => new PeriodResponse(p.Id, p.ClassId, p.SubjectId, p.TeacherId,
                p.DayOfWeek, p.StartTime, p.EndTime, p.Type, p.Name))
            .ToList();
        return BaseResponse<TimetableResponse>.SuccessResponse(
            "Period added successfully",
            new TimetableResponse(cls.Id, cls.Name, periodResponses));
    }

    public async Task<BaseResponse<TimetableResponse>> GetTimetableAsync(Guid classId)
    {
        var cls = await _classRepo.GetByIdAsync(classId);
        if (cls == null)
        {
            throw new ClassNotFoundException($"Class {classId} not found.");
        }

        var periods = await _periodRepo.GetPeriodsByClassIdAsync(classId);
        var periodResponses = periods
            .OrderBy(p => p.DayOfWeek)
            .ThenBy(p => p.StartTime)
            .Select(p => new PeriodResponse(p.Id, p.ClassId, p.SubjectId, p.TeacherId,
                p.DayOfWeek, p.StartTime, p.EndTime, p.Type, p.Name))
            .ToList();

        return BaseResponse<TimetableResponse>.SuccessResponse(
            "Timetable retrieved",
            new TimetableResponse(cls.Id, cls.Name, periodResponses));
    }


}

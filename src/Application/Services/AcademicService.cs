using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Enums;

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

        // Term dates must sit inside the academic year's window
        if (request.StartDate < year.StartDate || request.EndDate > year.EndDate)
            throw new TermDateOutOfRangeException(
                $"Term dates must fall within the academic year window ({year.StartDate} to {year.EndDate}).");

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

        // Ensure this term's dates do not overlap any existing term in the same year
        var existingTerms = await _academicYearRepository.GetTermsByYearAsync(request.AcademicYearId);
        var hasOverlap = existingTerms.Any(t =>
            request.StartDate < t.EndDate && request.EndDate > t.StartDate);
        if (hasOverlap)
            throw new TermDateOverlapException(
                "Term dates overlap with an existing term in this academic year.");

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
        var (items, totalCount) = await _subjectRepo.GetAllAsync(page, pageSize);
        var mapped = items.Select(s => new SubjectResponse(s.Id, s.Name, s.Code)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var paged = new PagedResponse<SubjectResponse>(mapped, page, pageSize, totalCount, totalPages);
        return BaseResponse<PagedResponse<SubjectResponse>>.SuccessResponse("Subjects retrieved", paged);
    }

    public async Task<BaseResponse<PeriodResponse>> CreatePeriodAsync(Guid classId, CreatePeriodRequest request)
    {
        var tenantId = _currentTenant.Id;

        _ = await _classRepo.GetByIdAsync(classId)
            ?? throw new ClassNotFoundException($"Class {classId} not found.");

        string periodName;
        if (request.Type == PeriodType.Timetabled)
        {
            var subject = await _subjectRepo.GetByIdAsync(request.SubjectId!.Value)
                ?? throw new SubjectNotFoundException($"Subject {request.SubjectId} not found.");
            periodName = request.Name ?? subject.Name;
        }
        else if (request.Type == PeriodType.DailyRegister)
        {
            periodName = request.Name ?? "Morning Register";
        }
        else
        {
            // NonAcademic — validator guarantees Name is present
            periodName = request.Name!;
        }

        // Conflict check — no two periods on the same day may overlap in time
        var existingPeriods = await _periodRepo.GetPeriodsByClassAndDayAsync(classId, request.DayOfWeek);
        var hasConflict = existingPeriods.Any(p =>
            request.StartTime < p.EndTime && request.EndTime > p.StartTime);

        if (hasConflict)
            throw new PeriodTimeConflictException(
                $"A period already exists on {request.DayOfWeek} that overlaps {request.StartTime}–{request.EndTime}.");

        var period = Period.Create(tenantId, classId, request.SubjectId, request.TeacherId,
            request.DayOfWeek, request.StartTime, request.EndTime, request.Type, periodName);
        await _periodRepo.AddAsync(period);

        return BaseResponse<PeriodResponse>.SuccessResponse(
            "Period added successfully",
            new PeriodResponse(period.Id, period.ClassId, period.SubjectId, period.TeacherId,
                period.DayOfWeek, period.StartTime, period.EndTime, period.Type, period.Name));
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

    public async Task<BaseResponse<AcademicYearResponse>> UpdateAcademicYearAsync(Guid yearId, UpdateAcademicYearRequest request)
    {
        var year = await _academicYearRepository.GetByIdAsync(yearId)
            ?? throw new AcademicYearNotFoundException($"Academic year {yearId} not found.");

        var newName = request.Name ?? year.Name;
        var newStart = request.StartDate ?? year.StartDate;
        var newEnd = request.EndDate ?? year.EndDate;

        if (newEnd <= newStart)
            throw new ArgumentException("End date must be after start date.");

        if (newName != year.Name && await _academicYearRepository.ExistsByNameAsync(newName))
            throw new DuplicateAcademicYearException($"An academic year named '{newName}' already exists.");

        // If the date window is shrinking, verify all existing terms still fit inside it
        if (newStart != year.StartDate || newEnd != year.EndDate)
        {
            var terms = await _academicYearRepository.GetTermsByYearAsync(yearId);
            var outOfRange = terms.Where(t => t.StartDate < newStart || t.EndDate > newEnd).ToList();
            if (outOfRange.Count > 0)
                throw new TermDateOutOfRangeException(
                    $"Updating the academic year dates would place {outOfRange.Count} existing term(s) out of range: " +
                    string.Join(", ", outOfRange.Select(t => t.Name)));
        }

        if (request.SetAsCurrent == true && !year.IsCurrent)
        {
            var current = await _academicYearRepository.GetCurrentAsync();
            if (current != null)
            {
                current.UnsetCurrent();
                await _academicYearRepository.UpdateAsync(current);
            }
            year.SetAsCurrent();
        }

        year.Update(newName, newStart, newEnd);
        await _academicYearRepository.UpdateAsync(year);

        return BaseResponse<AcademicYearResponse>.SuccessResponse(
            "Academic year updated",
            new AcademicYearResponse(year.Id, year.Name, year.StartDate, year.EndDate, year.IsCurrent));
    }

    public async Task<BaseResponse<TermResponse>> UpdateTermAsync(Guid termId, UpdateTermRequest request)
    {
        var term = await _academicYearRepository.GetTermByIdAsync(termId)
            ?? throw new TermNotFoundException($"Term {termId} not found.");

        var year = await _academicYearRepository.GetByIdAsync(term.AcademicYearId)
            ?? throw new AcademicYearNotFoundException($"Academic year {term.AcademicYearId} not found.");

        var newName = request.Name ?? term.Name;
        var newNumber = request.TermNumber ?? term.TermNumber;
        var newStart = request.StartDate ?? term.StartDate;
        var newEnd = request.EndDate ?? term.EndDate;

        if (newEnd <= newStart)
            throw new ArgumentException("End date must be after start date.");

        if (newStart < year.StartDate || newEnd > year.EndDate)
            throw new TermDateOutOfRangeException(
                $"Term dates must fall within the academic year window ({year.StartDate} to {year.EndDate}).");

        // Overlap check — exclude this term from the comparison
        var siblings = await _academicYearRepository.GetTermsByYearAsync(term.AcademicYearId);
        var hasOverlap = siblings.Any(t =>
            t.Id != termId &&
            newStart < t.EndDate && newEnd > t.StartDate);
        if (hasOverlap)
            throw new TermDateOverlapException(
                "Updated term dates overlap with an existing term in this academic year.");

        if (request.SetAsCurrent == true && !term.IsCurrent)
        {
            var current = await _academicYearRepository.GetCurrentTermAsync(term.AcademicYearId);
            if (current != null)
            {
                current.UnsetCurrent();
                await _academicYearRepository.UpdateTermAsync(current);
            }
            term.SetAsCurrent();
        }

        term.Update(newName, newNumber, newStart, newEnd);
        await _academicYearRepository.UpdateTermAsync(term);

        return BaseResponse<TermResponse>.SuccessResponse(
            "Term updated",
            new TermResponse(term.Id, term.AcademicYearId, term.Name, term.TermNumber,
                term.StartDate, term.EndDate, term.IsCurrent));
    }

    public async Task<BaseResponse<ClassResponse>> UpdateClassAsync(Guid classId, UpdateClassRequest request)
    {
        var cls = await _classRepo.GetByIdAsync(classId)
            ?? throw new ClassNotFoundException($"Class {classId} not found.");

        var newName = request.Name ?? cls.Name;

        if (newName != cls.Name && await _classRepo.ExistsByNameAsync(newName))
            throw new DuplicateClassNameException($"A class named '{newName}' already exists.");


        var newFormTeacherId = request.FormTeacherId.HasValue
            ? request.FormTeacherId.Value
            : cls.FormTeacherId;

        cls.Update(newName, newFormTeacherId);
        await _classRepo.UpdateAsync(cls);

        return BaseResponse<ClassResponse>.SuccessResponse(
            "Class updated",
            new ClassResponse(cls.Id, cls.Name, cls.FormTeacherId));
    }

    public async Task<BaseResponse<SubjectResponse>> UpdateSubjectAsync(Guid subjectId, UpdateSubjectRequest request)
    {
        var subject = await _subjectRepo.GetByIdAsync(subjectId)
            ?? throw new SubjectNotFoundException($"Subject {subjectId} not found.");

        var newName = request.Name ?? subject.Name;

        if (newName != subject.Name && await _subjectRepo.ExistsByNameAsync(newName))
            throw new DuplicateSubjectCodeException($"A subject named '{newName}' already exists.");

        var newCode = request.Code.HasValue ? request.Code.Value : subject.Code;

        subject.Update(newName, newCode);
        await _subjectRepo.UpdateAsync(subject);

        return BaseResponse<SubjectResponse>.SuccessResponse(
            "Subject updated",
            new SubjectResponse(subject.Id, subject.Name, subject.Code));
    }

    public async Task<BaseResponse<PeriodResponse>> UpdatePeriodAsync(Guid classId, Guid periodId, UpdatePeriodRequest request)
    {
        _ = await _classRepo.GetByIdAsync(classId)
            ?? throw new ClassNotFoundException($"Class {classId} not found.");

        var period = await _periodRepo.GetByIdAsync(periodId)
            ?? throw new PeriodNotFoundException($"Period {periodId} not found.");

        var newType = request.Type ?? period.Type;
        var newDay = request.DayOfWeek ?? period.DayOfWeek;
        var newStart = request.StartTime ?? period.StartTime;
        var newEnd = request.EndTime ?? period.EndTime;

        if (newEnd <= newStart)
            throw new ArgumentException("End time must be after start time.");

        // Resolve subject and name based on the effective type
        Guid? newSubjectId;
        string newName;

        if (newType == PeriodType.Timetabled)
        {
            newSubjectId = request.SubjectId ?? period.SubjectId
                ?? throw new ArgumentException("SubjectId is required for timetabled periods.");

            var subject = await _subjectRepo.GetByIdAsync(newSubjectId.Value)
                ?? throw new SubjectNotFoundException($"Subject {newSubjectId} not found.");

            newName = request.Name ?? subject.Name;
        }
        else if (newType == PeriodType.DailyRegister)
        {
            newSubjectId = null;
            newName = request.Name ?? "Morning Register";
        }
        else
        {
            newSubjectId = null;
            newName = request.Name
                ?? (period.Type == PeriodType.NonAcademic ? period.Name
                    : throw new ArgumentException("Name is required for non-academic periods."));
        }

        // Resolve teacher — Optional<T> handles explicit clearing
        var newTeacherId = request.TeacherId.HasValue ? request.TeacherId.Value : period.TeacherId;

        // Conflict check — exclude this period from the overlap scan
        var siblings = await _periodRepo.GetPeriodsByClassAndDayAsync(classId, newDay, excludePeriodId: periodId);
        var hasConflict = siblings.Any(p => newStart < p.EndTime && newEnd > p.StartTime);
        if (hasConflict)
            throw new PeriodTimeConflictException(
                $"A period already exists on {newDay} that overlaps {newStart}–{newEnd}.");

        period.Update(newSubjectId, newTeacherId, newDay, newStart, newEnd, newType, newName);
        await _periodRepo.UpdateAsync(period);

        return BaseResponse<PeriodResponse>.SuccessResponse(
            "Period updated",
            new PeriodResponse(period.Id, period.ClassId, period.SubjectId, period.TeacherId,
                period.DayOfWeek, period.StartTime, period.EndTime, period.Type, period.Name));
    }

}

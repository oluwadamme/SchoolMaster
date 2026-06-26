using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Application.Services;

public class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _attendanceRepo;
    private readonly IStudentRepository _studentRepo;
    private readonly IClassRepository _classRepo;
    private readonly IAcademicYearRepository _academicYearRepo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public AttendanceService(
        IAttendanceRepository attendanceRepo,
        IStudentRepository studentRepo,
        IClassRepository classRepo,
        IAcademicYearRepository academicYearRepo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _attendanceRepo = attendanceRepo;
        _studentRepo = studentRepo;
        _classRepo = classRepo;
        _academicYearRepo = academicYearRepo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<BaseResponse<MarkAttendanceResponse>> MarkAttendanceAsync(MarkAttendanceRequest request)
    {
        var tenantId = _currentTenant.Id;
        var teacherId = _currentUser.Id;

        // Resolve the active academic context before touching any records.
        // Attendance cannot be marked outside a term — it must be anchored to one.
        var currentYear = await _academicYearRepo.GetCurrentAsync()
            ?? throw new AcademicYearNotFoundException(
                "No active academic year is set. Please set a current academic year before marking attendance.");

        var currentTerm = await _academicYearRepo.GetCurrentTermAsync(currentYear.Id)
            ?? throw new TermNotFoundException(
                "No active term is set for the current academic year. Please set a current term before marking attendance.");

        // Sequential, not parallel: both queries share the same scoped DbContext, and EF Core
        // forbids concurrent operations on one context instance ("a second operation was started
        // on this context before a previous operation completed"). Parallelising here would need
        // separate DbContext instances, which is not worth it for two cheap lookups.
        _ = await _classRepo.GetByIdAsync(request.ClassId)
            ?? throw new ClassNotFoundException($"Class {request.ClassId} not found.");
        var validStudentIds = await _studentRepo.GetStudentIdsByClassIdAsync(request.ClassId);

        foreach (var entry in request.Records)
        {
            if (!validStudentIds.Contains(entry.StudentId))
                throw new StudentNotInClassException(
                    $"Student {entry.StudentId} does not belong to class {request.ClassId}.");
        }

        // Load existing records for this class+date — supports upsert (correcting mistakes)
        var existing = await _attendanceRepo.GetByClassAndDateAsync(request.ClassId, request.Date);
        var existingByStudent = existing.ToDictionary(r => r.StudentId);

        var toCreate = new List<DailyAttendance>();
        var updatedExisting = new List<DailyAttendance>();

        foreach (var entry in request.Records)
        {
            if (existingByStudent.TryGetValue(entry.StudentId, out var existingRecord))
            {
                existingRecord.UpdateStatus(entry.Status, teacherId, entry.Notes);
                updatedExisting.Add(existingRecord);
            }
            else
            {
                var record = DailyAttendance.Create(
                    tenantId, entry.StudentId, request.ClassId,
                    currentTerm.Id, request.Date, entry.Status, teacherId, entry.Notes);
                toCreate.Add(record);
            }
        }

        if (toCreate.Count > 0)
            await _attendanceRepo.AddRangeAsync(toCreate);

        // Count domain events across all touched records — entity is the source of truth for
        // when a notification fires. This avoids duplicating the entity's internal condition here.
        var notificationsQueued = toCreate.Concat(updatedExisting)
            .Sum(r => r.DomainEvents.Count);

        var allStatuses = request.Records.Select(r => r.Status).ToList();
        var response = new MarkAttendanceResponse(
            TotalMarked:         allStatuses.Count,
            Present:             allStatuses.Count(s => s == AttendanceStatus.Present),
            Absent:              allStatuses.Count(s => s == AttendanceStatus.Absent),
            Late:                allStatuses.Count(s => s == AttendanceStatus.Late),
            Excused:             allStatuses.Count(s => s == AttendanceStatus.Excused),
            NotificationsQueued: notificationsQueued
        );

        return BaseResponse<MarkAttendanceResponse>.SuccessResponse(
            "Attendance marked successfully.", response);
    }

    public async Task<BaseResponse<ClassAttendanceResponse>> GetClassAttendanceAsync(
        Guid classId, DateOnly date)
    {
        _ = await _classRepo.GetByIdAsync(classId)
            ?? throw new ClassNotFoundException($"Class {classId} not found.");

        var records = await _attendanceRepo.GetByClassAndDateAsync(classId, date);
        var mapped = records
            .Select(r => new AttendanceRecordResponse(r.StudentId, r.TermId, r.Date, r.Status, r.Notes))
            .ToList();

        return BaseResponse<ClassAttendanceResponse>.SuccessResponse(
            "Class attendance retrieved.",
            new ClassAttendanceResponse(classId, date, mapped));
    }

    public async Task<BaseResponse<StudentAttendanceSummaryResponse>> GetStudentAttendanceSummaryAsync(
        Guid studentId, Guid? termId, DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ArgumentException("'from' date must be earlier than or equal to 'to' date.");

        if (!await _studentRepo.ExistsAsync(studentId))
            throw new UserNotFoundException($"Student {studentId} not found.");

        var records = await _attendanceRepo.GetByStudentAsync(studentId, termId, from, to);

        var mapped = records
            .Select(r => new AttendanceRecordResponse(r.StudentId, r.TermId, r.Date, r.Status, r.Notes))
            .ToList();

        var total   = records.Count;
        var present = records.Count(r => r.Status == AttendanceStatus.Present);
        var absent  = records.Count(r => r.Status == AttendanceStatus.Absent);
        var late    = records.Count(r => r.Status == AttendanceStatus.Late);
        var excused = records.Count(r => r.Status == AttendanceStatus.Excused);

        // Present + Late + Excused count as attended (industry standard)
        var attended   = present + late + excused;
        var percentage = total == 0 ? 0 : Math.Round(attended / (double)total * 100, 2);

        return BaseResponse<StudentAttendanceSummaryResponse>.SuccessResponse(
            "Student attendance retrieved.",
            new StudentAttendanceSummaryResponse(
                studentId, total, present, absent, late, excused, percentage, mapped));
    }
}

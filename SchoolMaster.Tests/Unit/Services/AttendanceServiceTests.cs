using Moq;
using Xunit;
using SchoolMaster.Application.DTOs;
using SchoolMaster.Application.Repositories;
using SchoolMaster.Application.Services;
using SchoolMaster.Application.Services.Interfaces;
using SchoolMaster.Domain.CustomException;
using SchoolMaster.Domain.Entities;
using SchoolMaster.Domain.Enums;

namespace SchoolMaster.Tests.Unit.Services;

public class AttendanceServiceTests
{
    private readonly Mock<IAttendanceRepository> _attendanceRepo = new();
    private readonly Mock<IStudentRepository> _studentRepo = new();
    private readonly Mock<IClassRepository> _classRepo = new();
    private readonly Mock<IAcademicYearRepository> _academicYearRepo = new();
    private readonly Mock<ICurrentTenant> _currentTenant = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _studentA = Guid.NewGuid();
    private readonly Guid _studentB = Guid.NewGuid();
    private readonly DateOnly _date = new(2026, 6, 15);

    private readonly AcademicYear _year;
    private readonly Term _term;
    private readonly Class _class;

    public AttendanceServiceTests()
    {
        _year  = AcademicYear.Create(_tenantId, "2025/2026", new(2025, 9, 1), new(2026, 7, 31), true);
        _term  = Term.Create(_tenantId, _year.Id, "First Term", 1, new(2025, 9, 1), new(2025, 12, 20), true);
        _class = Class.Create(_tenantId, "JSS 1A", null);

        // Happy-path defaults — individual tests override what they need.
        _currentTenant.SetupGet(t => t.Id).Returns(_tenantId);
        _currentUser.SetupGet(u => u.Id).Returns(_teacherId);
        _academicYearRepo.Setup(r => r.GetCurrentAsync()).ReturnsAsync(_year);
        _academicYearRepo.Setup(r => r.GetCurrentTermAsync(_year.Id)).ReturnsAsync(_term);
        _classRepo.Setup(r => r.GetByIdAsync(_class.Id)).ReturnsAsync(_class);
        _studentRepo.Setup(r => r.GetStudentIdsByClassIdAsync(_class.Id))
            .ReturnsAsync([_studentA, _studentB]);
        _attendanceRepo.Setup(r => r.GetByClassAndDateAsync(_class.Id, It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<DailyAttendance>());
        _attendanceRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<DailyAttendance>>()))
            .Returns(Task.CompletedTask);
    }

    private AttendanceService CreateSut() => new(
        _attendanceRepo.Object,
        _studentRepo.Object,
        _classRepo.Object,
        _academicYearRepo.Object,
        _currentTenant.Object,
        _currentUser.Object);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private MarkAttendanceRequest MarkRequest(params (Guid Id, AttendanceStatus Status)[] entries)
        => new(_class.Id, _date,
            entries.Select(e => new AttendanceEntryRequest(e.Id, e.Status, null)).ToList());

    // Simulates an already-persisted record: events raised at creation have been dispatched
    // and cleared by a previous Unit of Work commit.
    private DailyAttendance ExistingRecord(Guid studentId, AttendanceStatus status)
    {
        var record = DailyAttendance.Create(
            _tenantId, studentId, _class.Id, _term.Id, _date, status, _teacherId, null);
        record.ClearDomainEvents();
        return record;
    }

    // ── MarkAttendanceAsync — guard clauses ───────────────────────────────────

    [Fact]
    public async Task MarkAttendanceAsync_WhenNoCurrentAcademicYear_ThrowsAcademicYearNotFoundException()
    {
        _academicYearRepo.Setup(r => r.GetCurrentAsync()).ReturnsAsync((AcademicYear?)null);

        await Assert.ThrowsAsync<AcademicYearNotFoundException>(() =>
            CreateSut().MarkAttendanceAsync(MarkRequest((_studentA, AttendanceStatus.Present))));
    }

    [Fact]
    public async Task MarkAttendanceAsync_WhenNoCurrentTerm_ThrowsTermNotFoundException()
    {
        _academicYearRepo.Setup(r => r.GetCurrentTermAsync(_year.Id)).ReturnsAsync((Term?)null);

        await Assert.ThrowsAsync<TermNotFoundException>(() =>
            CreateSut().MarkAttendanceAsync(MarkRequest((_studentA, AttendanceStatus.Present))));
    }

    [Fact]
    public async Task MarkAttendanceAsync_WhenClassNotFound_ThrowsClassNotFoundException()
    {
        _classRepo.Setup(r => r.GetByIdAsync(_class.Id)).ReturnsAsync((Class?)null);

        await Assert.ThrowsAsync<ClassNotFoundException>(() =>
            CreateSut().MarkAttendanceAsync(MarkRequest((_studentA, AttendanceStatus.Present))));
    }

    [Fact]
    public async Task MarkAttendanceAsync_WithStudentNotInClass_ThrowsStudentNotInClassException()
    {
        var outsider = Guid.NewGuid(); // not in the class roster

        await Assert.ThrowsAsync<StudentNotInClassException>(() =>
            CreateSut().MarkAttendanceAsync(MarkRequest((outsider, AttendanceStatus.Present))));
    }

    [Fact]
    public async Task MarkAttendanceAsync_WithStudentNotInClass_DoesNotPersistAnyRecord()
    {
        var outsider = Guid.NewGuid();

        await Assert.ThrowsAsync<StudentNotInClassException>(() =>
            CreateSut().MarkAttendanceAsync(MarkRequest(
                (_studentA, AttendanceStatus.Present),
                (outsider, AttendanceStatus.Absent))));

        // Validation runs fully before any write — partial state must not be persisted.
        _attendanceRepo.Verify(r => r.AddRangeAsync(It.IsAny<List<DailyAttendance>>()), Times.Never);
    }

    // ── MarkAttendanceAsync — happy path ──────────────────────────────────────

    [Fact]
    public async Task MarkAttendanceAsync_WithValidRequest_ReturnsStatusBreakdown()
    {
        var result = await CreateSut().MarkAttendanceAsync(MarkRequest(
            (_studentA, AttendanceStatus.Present),
            (_studentB, AttendanceStatus.Absent)));

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.TotalMarked);
        Assert.Equal(1, result.Data.Present);
        Assert.Equal(1, result.Data.Absent);
    }

    [Fact]
    public async Task MarkAttendanceAsync_AnchorsCreatedRecordsToCurrentTerm()
    {
        List<DailyAttendance>? captured = null;
        _attendanceRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<DailyAttendance>>()))
            .Callback<List<DailyAttendance>>(r => captured = r)
            .Returns(Task.CompletedTask);

        await CreateSut().MarkAttendanceAsync(MarkRequest((_studentA, AttendanceStatus.Present)));

        Assert.NotNull(captured);
        Assert.All(captured!, r => Assert.Equal(_term.Id, r.TermId));
    }

    [Fact]
    public async Task MarkAttendanceAsync_StampsRecordsWithCurrentUserAsMarkingTeacher()
    {
        List<DailyAttendance>? captured = null;
        _attendanceRepo.Setup(r => r.AddRangeAsync(It.IsAny<List<DailyAttendance>>()))
            .Callback<List<DailyAttendance>>(r => captured = r)
            .Returns(Task.CompletedTask);

        await CreateSut().MarkAttendanceAsync(MarkRequest((_studentA, AttendanceStatus.Present)));

        // MarkedByTeacherId must come from the authenticated user, never the tenant id.
        Assert.All(captured!, r => Assert.Equal(_teacherId, r.MarkedByTeacherId));
    }

    [Fact]
    public async Task MarkAttendanceAsync_WithNewAbsence_QueuesOneNotification()
    {
        var result = await CreateSut().MarkAttendanceAsync(MarkRequest(
            (_studentA, AttendanceStatus.Present),
            (_studentB, AttendanceStatus.Absent)));

        Assert.Equal(1, result.Data!.NotificationsQueued);
    }

    [Fact]
    public async Task MarkAttendanceAsync_WithNoAbsences_QueuesNoNotifications()
    {
        var result = await CreateSut().MarkAttendanceAsync(MarkRequest(
            (_studentA, AttendanceStatus.Present),
            (_studentB, AttendanceStatus.Late)));

        Assert.Equal(0, result.Data!.NotificationsQueued);
    }

    // ── MarkAttendanceAsync — upsert behaviour ────────────────────────────────

    [Fact]
    public async Task MarkAttendanceAsync_WhenRecordExists_UpdatesInsteadOfCreating()
    {
        var existing = ExistingRecord(_studentA, AttendanceStatus.Absent);
        _attendanceRepo.Setup(r => r.GetByClassAndDateAsync(_class.Id, _date))
            .ReturnsAsync([existing]);

        var result = await CreateSut().MarkAttendanceAsync(MarkRequest(
            (_studentA, AttendanceStatus.Present)));

        Assert.True(result.Success);
        Assert.Equal(AttendanceStatus.Present, existing.Status);
        // Nothing new to insert — the existing record was updated in place.
        _attendanceRepo.Verify(r => r.AddRangeAsync(It.IsAny<List<DailyAttendance>>()), Times.Never);
    }

    [Fact]
    public async Task MarkAttendanceAsync_ReMarkingAlreadyAbsentAsAbsent_DoesNotQueueDuplicate()
    {
        var existing = ExistingRecord(_studentA, AttendanceStatus.Absent);
        _attendanceRepo.Setup(r => r.GetByClassAndDateAsync(_class.Id, _date))
            .ReturnsAsync([existing]);

        var result = await CreateSut().MarkAttendanceAsync(MarkRequest(
            (_studentA, AttendanceStatus.Absent)));

        // Already absent → not a NEW absence → no second notification.
        Assert.Equal(0, result.Data!.NotificationsQueued);
    }

    [Fact]
    public async Task MarkAttendanceAsync_ChangingPresentToAbsent_QueuesNotification()
    {
        var existing = ExistingRecord(_studentA, AttendanceStatus.Present);
        _attendanceRepo.Setup(r => r.GetByClassAndDateAsync(_class.Id, _date))
            .ReturnsAsync([existing]);

        var result = await CreateSut().MarkAttendanceAsync(MarkRequest(
            (_studentA, AttendanceStatus.Absent)));

        // Transition from present to absent is a new absence → notify.
        Assert.Equal(1, result.Data!.NotificationsQueued);
        Assert.Equal(AttendanceStatus.Absent, existing.Status);
    }

    // ── GetClassAttendanceAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetClassAttendanceAsync_WhenClassNotFound_ThrowsClassNotFoundException()
    {
        _classRepo.Setup(r => r.GetByIdAsync(_class.Id)).ReturnsAsync((Class?)null);

        await Assert.ThrowsAsync<ClassNotFoundException>(() =>
            CreateSut().GetClassAttendanceAsync(_class.Id, _date));
    }

    [Fact]
    public async Task GetClassAttendanceAsync_ReturnsMappedRecords()
    {
        _attendanceRepo.Setup(r => r.GetByClassAndDateAsync(_class.Id, _date))
            .ReturnsAsync(
            [
                ExistingRecord(_studentA, AttendanceStatus.Present),
                ExistingRecord(_studentB, AttendanceStatus.Absent),
            ]);

        var result = await CreateSut().GetClassAttendanceAsync(_class.Id, _date);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Records.Count);
        Assert.Equal(_class.Id, result.Data.ClassId);
    }

    // ── GetStudentAttendanceSummaryAsync ──────────────────────────────────────

    [Fact]
    public async Task GetStudentAttendanceSummaryAsync_WhenStudentNotFound_ThrowsUserNotFoundException()
    {
        _studentRepo.Setup(r => r.ExistsAsync(_studentA)).ReturnsAsync(false);

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            CreateSut().GetStudentAttendanceSummaryAsync(_studentA, null, null, null));
    }

    [Fact]
    public async Task GetStudentAttendanceSummaryAsync_WithInvertedDateRange_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateSut().GetStudentAttendanceSummaryAsync(
                _studentA, null, new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public async Task GetStudentAttendanceSummaryAsync_TreatsLateAndExcusedAsAttended()
    {
        _studentRepo.Setup(r => r.ExistsAsync(_studentA)).ReturnsAsync(true);
        _attendanceRepo.Setup(r => r.GetByStudentAsync(_studentA, null, null, null))
            .ReturnsAsync(
            [
                ExistingRecord(_studentA, AttendanceStatus.Present),
                ExistingRecord(_studentA, AttendanceStatus.Present),
                ExistingRecord(_studentA, AttendanceStatus.Absent),
                ExistingRecord(_studentA, AttendanceStatus.Late),
                ExistingRecord(_studentA, AttendanceStatus.Excused),
            ]);

        var result = await CreateSut().GetStudentAttendanceSummaryAsync(_studentA, null, null, null);

        Assert.Equal(5, result.Data!.TotalDays);
        Assert.Equal(2, result.Data.PresentDays);
        Assert.Equal(1, result.Data.AbsentDays);
        Assert.Equal(1, result.Data.LateDays);
        Assert.Equal(1, result.Data.ExcusedDays);
        // Present + Late + Excused = 4 attended of 5 = 80%
        Assert.Equal(80, result.Data.AttendancePercentage);
    }

    [Fact]
    public async Task GetStudentAttendanceSummaryAsync_WithNoRecords_ReturnsZeroPercentage()
    {
        _studentRepo.Setup(r => r.ExistsAsync(_studentA)).ReturnsAsync(true);
        _attendanceRepo.Setup(r => r.GetByStudentAsync(_studentA, null, null, null))
            .ReturnsAsync(new List<DailyAttendance>());

        var result = await CreateSut().GetStudentAttendanceSummaryAsync(_studentA, null, null, null);

        Assert.Equal(0, result.Data!.TotalDays);
        Assert.Equal(0, result.Data.AttendancePercentage);
    }
}

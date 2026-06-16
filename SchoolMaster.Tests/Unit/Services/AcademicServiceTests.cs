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

public class AcademicServiceTests
{
    private readonly Mock<IAcademicYearRepository> _yearRepo = new();
    private readonly Mock<IClassRepository> _classRepo = new();
    private readonly Mock<ISubjectRepository> _subjectRepo = new();
    private readonly Mock<IPeriodRepository> _periodRepo = new();
    private readonly Mock<ICurrentTenant> _currentTenant = new();

    private readonly Guid _tenantId = Guid.NewGuid();

    public AcademicServiceTests()
    {
        _currentTenant.SetupGet(t => t.Id).Returns(_tenantId);
    }

    private AcademicService CreateSut() => new(
        _yearRepo.Object,
        _classRepo.Object,
        _subjectRepo.Object,
        _periodRepo.Object,
        _currentTenant.Object);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private AcademicYear MakeYear(string name = "2025/2026", bool isCurrent = false)
        => AcademicYear.Create(_tenantId, name,
            new DateOnly(2025, 9, 1), new DateOnly(2026, 7, 31), isCurrent);

    private Term MakeTerm(Guid yearId, int termNumber = 1,
        DateOnly? start = null, DateOnly? end = null, bool isCurrent = false)
        => Term.Create(_tenantId, yearId,
            $"Term {termNumber}", termNumber,
            start ?? new DateOnly(2025, 9, 1),
            end ?? new DateOnly(2025, 12, 20),
            isCurrent);

    private Class MakeClass(string name = "JSS 1A")
        => Class.Create(_tenantId, name, null);

    private Subject MakeSubject(string name = "Mathematics", string? code = "MATH")
        => Subject.Create(_tenantId, name, code);

    private Period MakePeriod(Guid classId, Guid? subjectId = null,
        DayOfWeek day = DayOfWeek.Monday,
        TimeOnly? start = null, TimeOnly? end = null,
        PeriodType type = PeriodType.Timetabled)
        => Period.Create(_tenantId, classId, subjectId, null, day,
            start ?? new TimeOnly(8, 0), end ?? new TimeOnly(9, 0), type, "Test Period");

    // ── CreateAcademicYearAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateAcademicYearAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        _yearRepo.Setup(r => r.ExistsByNameAsync("2025/2026")).ReturnsAsync(false);
        _yearRepo.Setup(r => r.AddAsync(It.IsAny<AcademicYear>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAcademicYearAsync(new CreateAcademicYearRequest(
            "2025/2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 7, 31), false));

        Assert.True(result.Success);
        Assert.Equal("2025/2026", result.Data!.Name);
    }

    [Fact]
    public async Task CreateAcademicYearAsync_WithDuplicateName_ThrowsDuplicateAcademicYearException()
    {
        _yearRepo.Setup(r => r.ExistsByNameAsync("2025/2026")).ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateAcademicYearException>(() =>
            CreateSut().CreateAcademicYearAsync(new CreateAcademicYearRequest(
                "2025/2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 7, 31), false)));
    }

    [Fact]
    public async Task CreateAcademicYearAsync_WhenSetAsCurrent_UnsetsExistingCurrentYear()
    {
        var existingCurrent = MakeYear("2024/2025", isCurrent: true);
        _yearRepo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _yearRepo.Setup(r => r.GetCurrentAsync()).ReturnsAsync(existingCurrent);
        _yearRepo.Setup(r => r.UpdateAsync(It.IsAny<AcademicYear>())).Returns(Task.CompletedTask);
        _yearRepo.Setup(r => r.AddAsync(It.IsAny<AcademicYear>())).Returns(Task.CompletedTask);

        await CreateSut().CreateAcademicYearAsync(new CreateAcademicYearRequest(
            "2025/2026", new DateOnly(2025, 9, 1), new DateOnly(2026, 7, 31), true));

        Assert.False(existingCurrent.IsCurrent);
        _yearRepo.Verify(r => r.UpdateAsync(existingCurrent), Times.Once);
    }

    // ── CreateTermAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTermAsync_WhenYearNotFound_ThrowsAcademicYearNotFoundException()
    {
        _yearRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        await Assert.ThrowsAsync<AcademicYearNotFoundException>(() =>
            CreateSut().CreateTermAsync(new CreateTermRequest(
                Guid.NewGuid(), "First Term", 1,
                new DateOnly(2025, 9, 1), new DateOnly(2025, 12, 20), false)));
    }

    [Fact]
    public async Task CreateTermAsync_WhenDatesOutsideYearWindow_ThrowsTermDateOutOfRangeException()
    {
        var year = MakeYear();
        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.GetTermsByYearAsync(year.Id)).ReturnsAsync(new List<Term>());

        // End date goes beyond academic year end
        await Assert.ThrowsAsync<TermDateOutOfRangeException>(() =>
            CreateSut().CreateTermAsync(new CreateTermRequest(
                year.Id, "First Term", 1,
                new DateOnly(2025, 9, 1), new DateOnly(2026, 9, 1), false)));
    }

    [Fact]
    public async Task CreateTermAsync_WhenDatesOverlapExistingTerm_ThrowsTermDateOverlapException()
    {
        var year = MakeYear();
        var existing = MakeTerm(year.Id,
            start: new DateOnly(2025, 9, 1), end: new DateOnly(2025, 12, 20));

        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.GetCurrentTermAsync(year.Id)).ReturnsAsync((Term?)null);
        _yearRepo.Setup(r => r.GetTermsByYearAsync(year.Id)).ReturnsAsync(new List<Term> { existing });

        // New term starts before existing term ends → overlap
        await Assert.ThrowsAsync<TermDateOverlapException>(() =>
            CreateSut().CreateTermAsync(new CreateTermRequest(
                year.Id, "Second Term", 2,
                new DateOnly(2025, 11, 1), new DateOnly(2026, 3, 31), false)));
    }

    [Fact]
    public async Task CreateTermAsync_WithAdjacentDates_DoesNotThrowOverlapException()
    {
        var year = MakeYear();
        var existing = MakeTerm(year.Id,
            start: new DateOnly(2025, 9, 1), end: new DateOnly(2025, 12, 20));

        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.GetCurrentTermAsync(year.Id)).ReturnsAsync((Term?)null);
        _yearRepo.Setup(r => r.GetTermsByYearAsync(year.Id)).ReturnsAsync(new List<Term> { existing });
        _yearRepo.Setup(r => r.AddTermAsync(It.IsAny<Term>())).Returns(Task.CompletedTask);

        // Starts exactly when existing ends → adjacent, not overlapping
        var result = await CreateSut().CreateTermAsync(new CreateTermRequest(
            year.Id, "Second Term", 2,
            new DateOnly(2025, 12, 20), new DateOnly(2026, 4, 30), false));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CreateTermAsync_WhenSetAsCurrent_UnsetsExistingCurrentTerm()
    {
        var year = MakeYear();
        var existingCurrent = MakeTerm(year.Id, isCurrent: true);

        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.GetCurrentTermAsync(year.Id)).ReturnsAsync(existingCurrent);
        _yearRepo.Setup(r => r.UpdateTermAsync(It.IsAny<Term>())).Returns(Task.CompletedTask);
        _yearRepo.Setup(r => r.GetTermsByYearAsync(year.Id)).ReturnsAsync(new List<Term>());
        _yearRepo.Setup(r => r.AddTermAsync(It.IsAny<Term>())).Returns(Task.CompletedTask);

        await CreateSut().CreateTermAsync(new CreateTermRequest(
            year.Id, "Second Term", 2,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 30), true));

        Assert.False(existingCurrent.IsCurrent);
        _yearRepo.Verify(r => r.UpdateTermAsync(existingCurrent), Times.Once);
    }

    // ── CreateClassAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateClassAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        _classRepo.Setup(r => r.ExistsByNameAsync("JSS 1A")).ReturnsAsync(false);
        _classRepo.Setup(r => r.AddAsync(It.IsAny<Class>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateClassAsync(new CreateClassRequest("JSS 1A", null));

        Assert.True(result.Success);
        Assert.Equal("JSS 1A", result.Data!.Name);
        Assert.Null(result.Data.FormTeacherId);
    }

    [Fact]
    public async Task CreateClassAsync_WithDuplicateName_ThrowsDuplicateClassNameException()
    {
        _classRepo.Setup(r => r.ExistsByNameAsync("JSS 1A")).ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateClassNameException>(() =>
            CreateSut().CreateClassAsync(new CreateClassRequest("JSS 1A", null)));
    }

    // ── CreateSubjectAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSubjectAsync_WithValidRequest_ReturnsSuccessResponse()
    {
        _subjectRepo.Setup(r => r.ExistsByNameAsync("Mathematics")).ReturnsAsync(false);
        _subjectRepo.Setup(r => r.AddAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateSubjectAsync(new CreateSubjectRequest("Mathematics", "MATH"));

        Assert.True(result.Success);
        Assert.Equal("Mathematics", result.Data!.Name);
        Assert.Equal("MATH", result.Data.Code);
    }

    [Fact]
    public async Task CreateSubjectAsync_WithDuplicateName_ThrowsDuplicateSubjectCodeException()
    {
        _subjectRepo.Setup(r => r.ExistsByNameAsync("Mathematics")).ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateSubjectCodeException>(() =>
            CreateSut().CreateSubjectAsync(new CreateSubjectRequest("Mathematics", null)));
    }

    // ── CreatePeriodAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePeriodAsync_WhenClassNotFound_ThrowsClassNotFoundException()
    {
        _classRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Class?)null);

        await Assert.ThrowsAsync<ClassNotFoundException>(() =>
            CreateSut().CreatePeriodAsync(Guid.NewGuid(), new CreatePeriodRequest(
                null, null, DayOfWeek.Monday,
                new TimeOnly(8, 0), new TimeOnly(9, 0), PeriodType.DailyRegister, null)));
    }

    [Fact]
    public async Task CreatePeriodAsync_WhenSubjectNotFoundForTimetabled_ThrowsSubjectNotFoundException()
    {
        var cls = MakeClass();
        var subjectId = Guid.NewGuid();

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _subjectRepo.Setup(r => r.GetByIdAsync(subjectId)).ReturnsAsync((Subject?)null);

        await Assert.ThrowsAsync<SubjectNotFoundException>(() =>
            CreateSut().CreatePeriodAsync(cls.Id, new CreatePeriodRequest(
                subjectId, null, DayOfWeek.Monday,
                new TimeOnly(8, 0), new TimeOnly(9, 0), PeriodType.Timetabled, null)));
    }

    [Fact]
    public async Task CreatePeriodAsync_WhenTimeOverlapsExistingPeriod_ThrowsPeriodTimeConflictException()
    {
        var cls = MakeClass();
        var subject = MakeSubject();
        var existingPeriod = MakePeriod(cls.Id, subject.Id,
            start: new TimeOnly(8, 0), end: new TimeOnly(9, 0));

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _subjectRepo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
        _periodRepo.Setup(r => r.GetPeriodsByClassAndDayAsync(cls.Id, DayOfWeek.Monday, null))
            .ReturnsAsync(new List<Period> { existingPeriod });

        // New period overlaps existing 08:00-09:00
        await Assert.ThrowsAsync<PeriodTimeConflictException>(() =>
            CreateSut().CreatePeriodAsync(cls.Id, new CreatePeriodRequest(
                subject.Id, null, DayOfWeek.Monday,
                new TimeOnly(8, 30), new TimeOnly(9, 30), PeriodType.Timetabled, null)));
    }

    [Fact]
    public async Task CreatePeriodAsync_TimetabledWithNoName_UsesSubjectNameAsDefault()
    {
        var cls = MakeClass();
        var subject = MakeSubject("Mathematics");

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _subjectRepo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
        _periodRepo.Setup(r => r.GetPeriodsByClassAndDayAsync(cls.Id, DayOfWeek.Monday, null))
            .ReturnsAsync(new List<Period>());
        _periodRepo.Setup(r => r.AddAsync(It.IsAny<Period>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreatePeriodAsync(cls.Id, new CreatePeriodRequest(
            subject.Id, null, DayOfWeek.Monday,
            new TimeOnly(8, 0), new TimeOnly(9, 0), PeriodType.Timetabled, null));

        Assert.Equal("Mathematics", result.Data!.Name);
    }

    [Fact]
    public async Task CreatePeriodAsync_TimetabledWithCustomName_UsesProvidedName()
    {
        var cls = MakeClass();
        var subject = MakeSubject("Mathematics");

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _subjectRepo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
        _periodRepo.Setup(r => r.GetPeriodsByClassAndDayAsync(cls.Id, DayOfWeek.Monday, null))
            .ReturnsAsync(new List<Period>());
        _periodRepo.Setup(r => r.AddAsync(It.IsAny<Period>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreatePeriodAsync(cls.Id, new CreatePeriodRequest(
            subject.Id, null, DayOfWeek.Monday,
            new TimeOnly(8, 0), new TimeOnly(9, 0), PeriodType.Timetabled, "Advanced Maths"));

        Assert.Equal("Advanced Maths", result.Data!.Name);
    }

    [Fact]
    public async Task CreatePeriodAsync_DailyRegisterWithNoName_UsesMorningRegisterDefault()
    {
        var cls = MakeClass();

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _periodRepo.Setup(r => r.GetPeriodsByClassAndDayAsync(cls.Id, DayOfWeek.Monday, null))
            .ReturnsAsync(new List<Period>());
        _periodRepo.Setup(r => r.AddAsync(It.IsAny<Period>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreatePeriodAsync(cls.Id, new CreatePeriodRequest(
            null, null, DayOfWeek.Monday,
            new TimeOnly(7, 45), new TimeOnly(8, 0), PeriodType.DailyRegister, null));

        Assert.Equal("Morning Register", result.Data!.Name);
    }

    [Fact]
    public async Task CreatePeriodAsync_NonAcademic_UsesProvidedName()
    {
        var cls = MakeClass();

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _periodRepo.Setup(r => r.GetPeriodsByClassAndDayAsync(cls.Id, DayOfWeek.Monday, null))
            .ReturnsAsync(new List<Period>());
        _periodRepo.Setup(r => r.AddAsync(It.IsAny<Period>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreatePeriodAsync(cls.Id, new CreatePeriodRequest(
            null, null, DayOfWeek.Monday,
            new TimeOnly(10, 30), new TimeOnly(10, 45), PeriodType.NonAcademic, "Break"));

        Assert.Equal("Break", result.Data!.Name);
        Assert.Equal(PeriodType.NonAcademic, result.Data.Type);
    }

    // ── UpdateAcademicYearAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateAcademicYearAsync_WhenYearNotFound_ThrowsAcademicYearNotFoundException()
    {
        _yearRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        await Assert.ThrowsAsync<AcademicYearNotFoundException>(() =>
            CreateSut().UpdateAcademicYearAsync(Guid.NewGuid(),
                new UpdateAcademicYearRequest("New Name", null, null, null)));
    }

    [Fact]
    public async Task UpdateAcademicYearAsync_WhenNewNameAlreadyTaken_ThrowsDuplicateAcademicYearException()
    {
        var year = MakeYear("2025/2026");
        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.ExistsByNameAsync("2024/2025")).ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateAcademicYearException>(() =>
            CreateSut().UpdateAcademicYearAsync(year.Id,
                new UpdateAcademicYearRequest("2024/2025", null, null, null)));
    }

    [Fact]
    public async Task UpdateAcademicYearAsync_WhenShrinkingDatesLeaveTermsOutOfRange_ThrowsTermDateOutOfRangeException()
    {
        var year = MakeYear();
        var term = MakeTerm(year.Id,
            start: new DateOnly(2025, 9, 1), end: new DateOnly(2025, 12, 20));

        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _yearRepo.Setup(r => r.GetTermsByYearAsync(year.Id)).ReturnsAsync(new List<Term> { term });

        // Shrink year end to before the term's end date
        await Assert.ThrowsAsync<TermDateOutOfRangeException>(() =>
            CreateSut().UpdateAcademicYearAsync(year.Id,
                new UpdateAcademicYearRequest(null, null, new DateOnly(2025, 11, 1), null)));
    }

    [Fact]
    public async Task UpdateAcademicYearAsync_WhenSetAsCurrent_PromotesYearAndDemotesExistingCurrent()
    {
        var year = MakeYear("2025/2026", isCurrent: false);
        var existingCurrent = MakeYear("2024/2025", isCurrent: true);

        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _yearRepo.Setup(r => r.GetTermsByYearAsync(year.Id)).ReturnsAsync(new List<Term>());
        _yearRepo.Setup(r => r.GetCurrentAsync()).ReturnsAsync(existingCurrent);
        _yearRepo.Setup(r => r.UpdateAsync(It.IsAny<AcademicYear>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAcademicYearAsync(year.Id,
            new UpdateAcademicYearRequest(null, null, null, true));

        Assert.True(result.Data!.IsCurrent);
        Assert.False(existingCurrent.IsCurrent);
    }

    [Fact]
    public async Task UpdateAcademicYearAsync_WhenSetAsCurrentFalse_DoesNotDemoteYear()
    {
        var year = MakeYear("2025/2026", isCurrent: true);

        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _yearRepo.Setup(r => r.GetTermsByYearAsync(year.Id)).ReturnsAsync(new List<Term>());
        _yearRepo.Setup(r => r.UpdateAsync(It.IsAny<AcademicYear>())).Returns(Task.CompletedTask);

        // SetAsCurrent=false should be a no-op — year stays current
        var result = await CreateSut().UpdateAcademicYearAsync(year.Id,
            new UpdateAcademicYearRequest(null, null, null, false));

        Assert.True(result.Data!.IsCurrent);
    }

    // ── UpdateTermAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTermAsync_WhenTermNotFound_ThrowsTermNotFoundException()
    {
        _yearRepo.Setup(r => r.GetTermByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Term?)null);

        await Assert.ThrowsAsync<TermNotFoundException>(() =>
            CreateSut().UpdateTermAsync(Guid.NewGuid(),
                new UpdateTermRequest("New Name", null, null, null, null)));
    }

    [Fact]
    public async Task UpdateTermAsync_WhenDatesOverlapAnotherTerm_ThrowsTermDateOverlapException()
    {
        var year = MakeYear();
        var termToUpdate = MakeTerm(year.Id, termNumber: 1,
            start: new DateOnly(2025, 9, 1), end: new DateOnly(2025, 12, 20));
        var sibling = MakeTerm(year.Id, termNumber: 2,
            start: new DateOnly(2026, 1, 1), end: new DateOnly(2026, 4, 30));

        _yearRepo.Setup(r => r.GetTermByIdAsync(termToUpdate.Id)).ReturnsAsync(termToUpdate);
        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.GetTermsByYearAsync(year.Id))
            .ReturnsAsync(new List<Term> { termToUpdate, sibling });

        // Extending into sibling's range
        await Assert.ThrowsAsync<TermDateOverlapException>(() =>
            CreateSut().UpdateTermAsync(termToUpdate.Id,
                new UpdateTermRequest(null, null, null, new DateOnly(2026, 2, 1), null)));
    }

    [Fact]
    public async Task UpdateTermAsync_DoesNotConsiderSelfInOverlapCheck()
    {
        var year = MakeYear();
        var term = MakeTerm(year.Id,
            start: new DateOnly(2025, 9, 1), end: new DateOnly(2025, 12, 20));

        _yearRepo.Setup(r => r.GetTermByIdAsync(term.Id)).ReturnsAsync(term);
        _yearRepo.Setup(r => r.GetByIdAsync(year.Id)).ReturnsAsync(year);
        _yearRepo.Setup(r => r.GetTermsByYearAsync(year.Id))
            .ReturnsAsync(new List<Term> { term }); // only self in list
        _yearRepo.Setup(r => r.UpdateTermAsync(It.IsAny<Term>())).Returns(Task.CompletedTask);

        // Changing name only — same dates, self is excluded from overlap check
        var result = await CreateSut().UpdateTermAsync(term.Id,
            new UpdateTermRequest("Autumn Term", null, null, null, null));

        Assert.True(result.Success);
        Assert.Equal("Autumn Term", result.Data!.Name);
    }

    // ── UpdateClassAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateClassAsync_WhenClassNotFound_ThrowsClassNotFoundException()
    {
        _classRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Class?)null);

        await Assert.ThrowsAsync<ClassNotFoundException>(() =>
            CreateSut().UpdateClassAsync(Guid.NewGuid(),
                new UpdateClassRequest("New Name", default)));
    }

    [Fact]
    public async Task UpdateClassAsync_WhenNewNameAlreadyTaken_ThrowsDuplicateClassNameException()
    {
        var cls = MakeClass("JSS 1A");
        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _classRepo.Setup(r => r.ExistsByNameAsync("JSS 1B")).ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateClassNameException>(() =>
            CreateSut().UpdateClassAsync(cls.Id,
                new UpdateClassRequest("JSS 1B", default)));
    }

    [Fact]
    public async Task UpdateClassAsync_WhenFormTeacherIdAbsent_KeepsExistingValue()
    {
        var teacherId = Guid.NewGuid();
        var cls = Class.Create(_tenantId, "JSS 1A", teacherId);
        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _classRepo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _classRepo.Setup(r => r.UpdateAsync(It.IsAny<Class>())).Returns(Task.CompletedTask);

        // FormTeacherId absent (default Optional<Guid?> — HasValue = false)
        var result = await CreateSut().UpdateClassAsync(cls.Id,
            new UpdateClassRequest(null, default));

        Assert.Equal(teacherId, result.Data!.FormTeacherId);
    }

    [Fact]
    public async Task UpdateClassAsync_WhenFormTeacherIdExplicitlyNull_ClearsIt()
    {
        var teacherId = Guid.NewGuid();
        var cls = Class.Create(_tenantId, "JSS 1A", teacherId);
        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _classRepo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _classRepo.Setup(r => r.UpdateAsync(It.IsAny<Class>())).Returns(Task.CompletedTask);

        // FormTeacherId present with null → HasValue = true, Value = null
        Optional<Guid?> clearTeacher = (Guid?)null;
        var result = await CreateSut().UpdateClassAsync(cls.Id,
            new UpdateClassRequest(null, clearTeacher));

        Assert.Null(result.Data!.FormTeacherId);
    }

    // ── UpdateSubjectAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSubjectAsync_WhenSubjectNotFound_ThrowsSubjectNotFoundException()
    {
        _subjectRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Subject?)null);

        await Assert.ThrowsAsync<SubjectNotFoundException>(() =>
            CreateSut().UpdateSubjectAsync(Guid.NewGuid(),
                new UpdateSubjectRequest("New Name", default)));
    }

    [Fact]
    public async Task UpdateSubjectAsync_WhenCodeAbsent_KeepsExistingCode()
    {
        var subject = MakeSubject("Mathematics", "MATH");
        _subjectRepo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
        _subjectRepo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _subjectRepo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateSubjectAsync(subject.Id,
            new UpdateSubjectRequest(null, default));

        Assert.Equal("MATH", result.Data!.Code);
    }

    [Fact]
    public async Task UpdateSubjectAsync_WhenCodeExplicitlyNull_ClearsCode()
    {
        var subject = MakeSubject("Mathematics", "MATH");
        _subjectRepo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
        _subjectRepo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>())).ReturnsAsync(false);
        _subjectRepo.Setup(r => r.UpdateAsync(It.IsAny<Subject>())).Returns(Task.CompletedTask);

        Optional<string?> clearCode = (string?)null;
        var result = await CreateSut().UpdateSubjectAsync(subject.Id,
            new UpdateSubjectRequest(null, clearCode));

        Assert.Null(result.Data!.Code);
    }

    // ── UpdatePeriodAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePeriodAsync_WhenPeriodNotFound_ThrowsPeriodNotFoundException()
    {
        var cls = MakeClass();
        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _periodRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Period?)null);

        await Assert.ThrowsAsync<PeriodNotFoundException>(() =>
            CreateSut().UpdatePeriodAsync(cls.Id, Guid.NewGuid(),
                new UpdatePeriodRequest(null, default, null, null, null, null, null)));
    }

    [Fact]
    public async Task UpdatePeriodAsync_WhenTimeOverlapsAnotherPeriod_ThrowsPeriodTimeConflictException()
    {
        var cls = MakeClass();
        var subject = MakeSubject();
        // Use a NonAcademic period so no subject lookup is needed before the conflict check
        var period = MakePeriod(cls.Id, subjectId: null,
            start: new TimeOnly(8, 0), end: new TimeOnly(9, 0),
            type: PeriodType.NonAcademic);
        var sibling = MakePeriod(cls.Id, subjectId: null,
            start: new TimeOnly(9, 0), end: new TimeOnly(10, 0),
            type: PeriodType.NonAcademic);

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _periodRepo.Setup(r => r.GetByIdAsync(period.Id)).ReturnsAsync(period);
        _periodRepo.Setup(r => r.GetPeriodsByClassAndDayAsync(cls.Id, DayOfWeek.Monday, period.Id))
            .ReturnsAsync(new List<Period> { sibling });

        // Extending into sibling's slot — conflict check fires before any subject lookup
        await Assert.ThrowsAsync<PeriodTimeConflictException>(() =>
            CreateSut().UpdatePeriodAsync(cls.Id, period.Id,
                new UpdatePeriodRequest(null, default, null, null, new TimeOnly(9, 30), null, "Break")));
    }

    [Fact]
    public async Task UpdatePeriodAsync_WhenTypeChangesToNonAcademic_ClearsSubjectId()
    {
        var cls = MakeClass();
        var subject = MakeSubject();
        var period = MakePeriod(cls.Id, subject.Id,
            type: PeriodType.Timetabled);

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _periodRepo.Setup(r => r.GetByIdAsync(period.Id)).ReturnsAsync(period);
        _periodRepo.Setup(r => r.GetPeriodsByClassAndDayAsync(cls.Id, DayOfWeek.Monday, period.Id))
            .ReturnsAsync(new List<Period>());
        _periodRepo.Setup(r => r.UpdateAsync(It.IsAny<Period>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdatePeriodAsync(cls.Id, period.Id,
            new UpdatePeriodRequest(null, default, null, null, null, PeriodType.NonAcademic, "Break"));

        Assert.Null(result.Data!.SubjectId);
        Assert.Equal(PeriodType.NonAcademic, result.Data.Type);
        Assert.Equal("Break", result.Data.Name);
    }

    [Fact]
    public async Task UpdatePeriodAsync_WhenTeacherIdExplicitlyNull_ClearsTeacher()
    {
        var cls = MakeClass();
        var subject = MakeSubject();
        var teacherId = Guid.NewGuid();
        var period = Period.Create(_tenantId, cls.Id, subject.Id, teacherId,
            DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(9, 0), PeriodType.Timetabled, "Maths");

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _periodRepo.Setup(r => r.GetByIdAsync(period.Id)).ReturnsAsync(period);
        _subjectRepo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
        _periodRepo.Setup(r => r.GetPeriodsByClassAndDayAsync(cls.Id, DayOfWeek.Monday, period.Id))
            .ReturnsAsync(new List<Period>());
        _periodRepo.Setup(r => r.UpdateAsync(It.IsAny<Period>())).Returns(Task.CompletedTask);

        Optional<Guid?> clearTeacher = (Guid?)null;
        var result = await CreateSut().UpdatePeriodAsync(cls.Id, period.Id,
            new UpdatePeriodRequest(null, clearTeacher, null, null, null, null, null));

        Assert.Null(result.Data!.TeacherId);
    }

    [Fact]
    public async Task UpdatePeriodAsync_ExcludesSelfFromConflictCheck()
    {
        var cls = MakeClass();
        var subject = MakeSubject();
        var period = MakePeriod(cls.Id, subject.Id,
            start: new TimeOnly(8, 0), end: new TimeOnly(9, 0));

        _classRepo.Setup(r => r.GetByIdAsync(cls.Id)).ReturnsAsync(cls);
        _periodRepo.Setup(r => r.GetByIdAsync(period.Id)).ReturnsAsync(period);
        _subjectRepo.Setup(r => r.GetByIdAsync(subject.Id)).ReturnsAsync(subject);
        // No siblings — only self was excluded
        _periodRepo.Setup(r => r.GetPeriodsByClassAndDayAsync(cls.Id, DayOfWeek.Monday, period.Id))
            .ReturnsAsync(new List<Period>());
        _periodRepo.Setup(r => r.UpdateAsync(It.IsAny<Period>())).Returns(Task.CompletedTask);

        // Shifting same period to a slightly different time — should not conflict with itself
        var result = await CreateSut().UpdatePeriodAsync(cls.Id, period.Id,
            new UpdatePeriodRequest(null, default, null, new TimeOnly(8, 15), new TimeOnly(9, 15), null, null));

        Assert.True(result.Success);
        Assert.Equal(new TimeOnly(8, 15), result.Data!.StartTime);
    }
}

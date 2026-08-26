using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Exceptions;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 7 — Publish gate enforcement & transactional safety.</summary>
public sealed class AiSchedCapPrompt7PublishGateTests
{
    private readonly Mock<ITimetableRepository> _repository = new();
    private readonly Mock<IScheduleVersionRepository> _versionRepository = new();
    private readonly Mock<IArchiveReasonRepository> _archiveReasonRepository = new();
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<ITimetableChangeHistoryService> _historyService = new();
    private readonly Mock<ITimetableService> _timetableService = new();
    private readonly Mock<ITimetablePublishReadinessService> _readiness = new();

    public AiSchedCapPrompt7PublishGateTests()
    {
        _currentUser.Setup(x => x.TenantId).Returns(1);
        _currentUser.Setup(x => x.UserId).Returns(10);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Clean_ready_timetable_publishes_and_saves()
    {
        var entity = LockedTimetable();
        SetupLoad(entity);
        SetupReady();
        _timetableService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimetableDto { Id = 1, Status = TimetableStatus.Published, Name = "T", AcademicYearId = 10 });

        var result = await CreateService().PublishAsync(1, null);

        Assert.Equal(TimetableStatus.Published, result.Status);
        Assert.Equal(TimetableStatus.Published, entity.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _historyService.Verify(h => h.RecordAsync(
            1, TimetableChangeOperation.Publish, null, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Critical_blocker_rejects_with_no_mutation()
    {
        var entity = LockedTimetable();
        SetupLoad(entity);
        SetupNotReady(Blocking("ROOM_DOUBLE_BOOKING", "Critical"));

        var ex = await Assert.ThrowsAsync<PublishNotReadyException>(() => CreateService().PublishAsync(1, null));

        Assert.False(ex.Readiness.IsReady);
        Assert.Contains(ex.Readiness.Findings, f => f.Code == "ROOM_DOUBLE_BOOKING" && f.IsBlocking);
        Assert.Equal(TimetableStatus.Locked, entity.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _historyService.Verify(h => h.RecordAsync(
            It.IsAny<int>(), It.IsAny<TimetableChangeOperation>(), It.IsAny<int?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ROOM_CAPACITY_rejects_with_no_mutation()
    {
        var entity = LockedTimetable();
        SetupLoad(entity);
        SetupNotReady(Blocking("ROOM_CAPACITY", "Error", entryId: 9, roomId: 3));

        var ex = await Assert.ThrowsAsync<PublishNotReadyException>(() => CreateService().PublishAsync(1, null));

        Assert.Contains(ex.Readiness.Findings, f => f.Code == "ROOM_CAPACITY" && f.IsBlocking && f.TimetableEntryId == 9);
        Assert.Equal(TimetableStatus.Locked, entity.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TEACHING_GROUP_CAPACITY_EXCEEDED_rejects_with_no_mutation()
    {
        var entity = LockedTimetable();
        SetupLoad(entity);
        SetupNotReady(Blocking("TEACHING_GROUP_CAPACITY_EXCEEDED", "Error", teachingGroupId: 7));

        var ex = await Assert.ThrowsAsync<PublishNotReadyException>(() => CreateService().PublishAsync(1, null));

        Assert.Contains(ex.Readiness.Findings, f =>
            f.Code == "TEACHING_GROUP_CAPACITY_EXCEEDED" && f.IsBlocking && f.TeachingGroupId == 7);
        Assert.Equal(TimetableStatus.Locked, entity.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Warning_only_allows_publish()
    {
        var entity = LockedTimetable();
        SetupLoad(entity);
        SetupReady(nonBlocking: Finding("FACULTY_PREFERENCE", "Warning", blocking: false));
        _timetableService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimetableDto { Id = 1, Status = TimetableStatus.Published, Name = "T", AcademicYearId = 10 });

        await CreateService().PublishAsync(1, null);

        Assert.Equal(TimetableStatus.Published, entity.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Non_capacity_Error_does_not_block_when_readiness_IsReady()
    {
        var entity = LockedTimetable();
        SetupLoad(entity);
        SetupReady(nonBlocking: Finding("ROOM_WRONG_TYPE", "Error", blocking: false));
        _timetableService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimetableDto { Id = 1, Status = TimetableStatus.Published, Name = "T", AcademicYearId = 10 });

        await CreateService().PublishAsync(1, null);

        Assert.Equal(TimetableStatus.Published, entity.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Frozen_preserves_existing_DomainException_contract_before_gate()
    {
        var entity = LockedTimetable();
        entity.IsFrozen = true;
        SetupLoad(entity);

        await Assert.ThrowsAsync<DomainException>(() => CreateService().PublishAsync(1, null));

        _readiness.Verify(r => r.EvaluatePublishReadinessAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(TimetableStatus.Locked, entity.Status);
    }

    [Fact]
    public async Task NotEligible_Draft_preserves_existing_DomainException_contract()
    {
        var entity = LockedTimetable();
        entity.Status = TimetableStatus.Draft;
        SetupLoad(entity);

        await Assert.ThrowsAsync<DomainException>(() => CreateService().PublishAsync(1, null));

        _readiness.Verify(r => r.EvaluatePublishReadinessAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Scope_conflict_preserves_existing_DomainException_contract()
    {
        var entity = LockedTimetable();
        var existing = new Timetable
        {
            Id = 2, TenantId = 1, AcademicYearId = 10, DepartmentId = 3,
            Status = TimetableStatus.Published, Name = "Existing"
        };
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _context.Setup(c => c.SchedulingTimetables).Returns(new[] { entity, existing }.AsAsyncQueryable());

        await Assert.ThrowsAsync<DomainException>(() => CreateService().PublishAsync(1, null));

        _readiness.Verify(r => r.EvaluatePublishReadinessAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Archived_blocked_by_readiness_gate_with_no_mutation()
    {
        // Existing PublishAsync did not explicitly reject Archived; Prompt 6/7 readiness does.
        var entity = LockedTimetable();
        entity.Status = TimetableStatus.Archived;
        // Make lifecycle eligibility pass via approved version so gate is reached.
        entity.ScheduleVersionId = 99;
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _context.Setup(c => c.SchedulingTimetables).Returns(new[] { entity }.AsAsyncQueryable());
        _versionRepository.Setup(v => v.GetByIdAsync(1, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScheduleVersion { Id = 99, TenantId = 1, Status = ScheduleVersionStatus.Approved });
        SetupNotReady(Blocking(TimetablePublishReadinessService.LifecycleArchivedCode, "Error"));

        var ex = await Assert.ThrowsAsync<PublishNotReadyException>(() => CreateService().PublishAsync(1, null));

        Assert.Contains(ex.Readiness.Findings, f =>
            f.Code == TimetablePublishReadinessService.LifecycleArchivedCode && f.IsBlocking);
        Assert.Equal(TimetableStatus.Archived, entity.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Gate_runs_before_mutation_and_uses_authoritative_readiness_service()
    {
        var entity = LockedTimetable();
        SetupLoad(entity);
        SetupNotReady(Blocking("ROOM_CAPACITY", "Error"));

        await Assert.ThrowsAsync<PublishNotReadyException>(() => CreateService().PublishAsync(1, null));

        _readiness.Verify(r => r.EvaluatePublishReadinessAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(TimetableStatus.Locked, entity.Status);
    }

    [Fact]
    public async Task Cross_tenant_missing_timetable_throws_not_found()
    {
        _repository.Setup(r => r.GetByIdAsync(1, 55, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Timetable?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateService().PublishAsync(55, null));
        _readiness.Verify(r => r.EvaluatePublishReadinessAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Blocked_publish_does_not_create_history_or_change_status()
    {
        var entity = LockedTimetable();
        var before = entity.Status;
        SetupLoad(entity);
        SetupNotReady(Blocking("ROOM_CAPACITY", "Error"));

        await Assert.ThrowsAsync<PublishNotReadyException>(() => CreateService().PublishAsync(1, null));

        Assert.Equal(before, entity.Status);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _historyService.VerifyNoOtherCalls();
    }

    private void SetupLoad(Timetable entity)
    {
        _repository.Setup(r => r.GetByIdAsync(1, entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _context.Setup(c => c.SchedulingTimetables).Returns(new[] { entity }.AsAsyncQueryable());
    }

    private void SetupReady(PublishReadinessFindingDto? nonBlocking = null)
    {
        var findings = nonBlocking is null
            ? Array.Empty<PublishReadinessFindingDto>()
            : new[] { nonBlocking };
        _readiness.Setup(r => r.EvaluatePublishReadinessAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new TimetablePublishReadinessResultDto
            {
                TimetableId = id,
                IsReady = true,
                LifecycleState = TimetableStatus.Locked,
                BlockingFindingCount = 0,
                WarningFindingCount = findings.Count(f => f.Severity == "Warning"),
                Findings = findings
            });
    }

    private void SetupNotReady(params PublishReadinessFindingDto[] blockers)
    {
        _readiness.Setup(r => r.EvaluatePublishReadinessAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new TimetablePublishReadinessResultDto
            {
                TimetableId = id,
                IsReady = false,
                LifecycleState = TimetableStatus.Locked,
                BlockingFindingCount = blockers.Length,
                Findings = blockers
            });
    }

    private TimetableLifecycleService CreateService() => new(
        _repository.Object,
        _versionRepository.Object,
        _archiveReasonRepository.Object,
        _context.Object,
        _unitOfWork.Object,
        _currentUser.Object,
        _historyService.Object,
        _timetableService.Object,
        _readiness.Object,
        Mock.Of<FluentValidation.IValidator<FreezeTimetableRequest>>(),
        Mock.Of<FluentValidation.IValidator<UnlockFrozenTimetableRequest>>());

    private static Timetable LockedTimetable() => new()
    {
        Id = 1,
        TenantId = 1,
        AcademicYearId = 10,
        DepartmentId = 3,
        Status = TimetableStatus.Locked,
        Name = "T",
        IsFrozen = false
    };

    private static PublishReadinessFindingDto Blocking(
        string code,
        string severity,
        int? entryId = null,
        int? roomId = null,
        int? teachingGroupId = null) =>
        Finding(code, severity, blocking: true, entryId, roomId, teachingGroupId);

    private static PublishReadinessFindingDto Finding(
        string code,
        string severity,
        bool blocking,
        int? entryId = null,
        int? roomId = null,
        int? teachingGroupId = null) => new()
    {
        Code = code,
        Severity = severity,
        IsBlocking = blocking,
        Title = code,
        Why = "why",
        RecommendedAction = "act",
        TimetableEntryId = entryId,
        RoomId = roomId,
        TeachingGroupId = teachingGroupId
    };
}

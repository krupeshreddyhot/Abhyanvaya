using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Exceptions;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-CAP Prompt 10 — Cross-layer consistency &amp; transactional integrity.</summary>
public sealed class AiSchedCapPrompt10TransactionalIntegrityTests
{
    private sealed class AmbientCurrentUser : ICurrentUserService
    {
        public int UserId { get; set; } = 1;
        public string Role { get; set; } = "Admin";
        public int TenantId { get; set; } = 1;
        public int StaffId { get; set; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        private readonly IUnitOfWork _inner;
        public int SaveCount { get; private set; }

        public CountingUnitOfWork(IUnitOfWork inner) => _inner = inner;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return _inner.SaveChangesAsync(cancellationToken);
        }

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            => _inner.ExecuteInTransactionAsync(action, cancellationToken);

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => _inner.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default)
            => _inner.RollbackAsync(cancellationToken);
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated commit failure — must not leave partial durable state.");

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RollbackAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static (ApplicationDbContext Db, TeachingGroupApplicationService Assign, CountingUnitOfWork Uow)
        CreateAssignSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("cap-p10-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        var projector = new TimetableSectionProjector(db, user);
        var uow = new CountingUnitOfWork(db);
        var assign = new TeachingGroupApplicationService(new TimetableRepository(db), db, uow, user, projector);
        return (db, assign, uow);
    }

    private static Section NewSection(int tenantId, string code) => new()
    {
        TenantId = tenantId,
        CollegeId = 1,
        AcademicYearId = 1,
        CourseId = 1,
        GroupId = 2,
        SemesterId = 3,
        SectionCode = code,
        SectionName = "Section " + code,
        Status = "Active",
        CreatedDate = DateTime.UtcNow,
    };

    private static async Task<(Timetable Tt, TimetableEntry Entry, TeachingGroup Tg, Section SecA, Section SecB)> SeedAsync(
        ApplicationDbContext db,
        int tenantId = 1)
    {
        var tg = new TeachingGroup
        {
            TenantId = tenantId,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            SubjectAllocationId = 10,
            Type = TeachingGroupType.Custom,
            MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
            Status = TeachingGroupStatus.Active,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Code = "TG-P10",
            Name = "TG-P10",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TeachingGroup>().Add(tg);

        var secA = NewSection(tenantId, "A");
        var secB = NewSection(tenantId, "B");
        db.Set<Section>().AddRange(secA, secB);

        var tt = new Timetable
        {
            TenantId = tenantId,
            Name = "Draft",
            AcademicYearId = 1,
            Status = TimetableStatus.Draft,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<Timetable>().Add(tt);
        await db.SaveChangesAsync();

        var entry = new TimetableEntry
        {
            TenantId = tenantId,
            TimetableId = tt.Id,
            DayOfWeek = 1,
            TimeSlotId = 1,
            SubjectAllocationId = 10,
            TeachingGroupId = null,
            StaffId = 1,
            RoomId = 1,
            DepartmentId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TimetableEntry>().Add(entry);
        await db.SaveChangesAsync();

        db.Set<TeachingGroupSection>().AddRange(
            new TeachingGroupSection
            {
                TenantId = tenantId,
                TeachingGroupId = tg.Id,
                SectionId = secA.Id,
                IsPrimary = true,
                CreatedDate = DateTime.UtcNow,
            },
            new TeachingGroupSection
            {
                TenantId = tenantId,
                TeachingGroupId = tg.Id,
                SectionId = secB.Id,
                IsPrimary = false,
                CreatedDate = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        return (tt, entry, tg, secA, secB);
    }

    private static async Task<List<int>> ActiveProjectedSectionIdsAsync(ApplicationDbContext db, int entryId)
        => await db.Set<TimetableSection>()
            .Where(x => x.TimetableEntryId == entryId && !x.IsDeleted)
            .Select(x => x.SectionId)
            .OrderBy(x => x)
            .ToListAsync();

    [Fact]
    public async Task Assign_stages_TeachingGroupId_projects_and_saves_once()
    {
        var (db, assign, uow) = CreateAssignSut();
        var (_, entry, tg, secA, secB) = await SeedAsync(db);
        var savesBefore = uow.SaveCount;

        var result = await assign.AssignToTimetableEntryAsync(entry.Id, tg.Id);

        Assert.Equal(tg.Id, result.TeachingGroupId);
        Assert.Equal(1, uow.SaveCount - savesBefore);
        Assert.Equal(
            new[] { secA.Id, secB.Id }.OrderBy(x => x),
            await ActiveProjectedSectionIdsAsync(db, entry.Id));
        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync(x => x.TeachingGroupId == tg.Id));
    }

    [Fact]
    public async Task Assign_failed_SaveChanges_does_not_persist_TeachingGroupId_or_projection()
    {
        var store = "cap-p10-atomic-fail-" + Guid.NewGuid().ToString("N");
        var user = new AmbientCurrentUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(store)
            .Options;
        await using (var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance))
        {
            var projector = new TimetableSectionProjector(db, user);
            var assign = new TeachingGroupApplicationService(
                new TimetableRepository(db), db, new FailingUnitOfWork(), user, projector);
            var (_, entry, tg, _, _) = await SeedAsync(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                assign.AssignToTimetableEntryAsync(entry.Id, tg.Id));
        }

        await using var verify = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(store).Options,
            user,
            NullLogger<ApplicationDbContext>.Instance);
        var reloaded = await verify.Set<TimetableEntry>().AsNoTracking().SingleAsync();
        Assert.Null(reloaded.TeachingGroupId);
        Assert.Empty(await verify.Set<TimetableSection>()
            .Where(x => x.TimetableEntryId == reloaded.Id && !x.IsDeleted)
            .ToListAsync());
    }

    [Fact]
    public async Task Clear_clears_projection_nulls_TeachingGroupId_and_saves_once()
    {
        var (db, assign, uow) = CreateAssignSut();
        var (_, entry, tg, _, _) = await SeedAsync(db);
        await assign.AssignToTimetableEntryAsync(entry.Id, tg.Id);
        var savesBefore = uow.SaveCount;

        var cleared = await assign.ClearFromTimetableEntryAsync(entry.Id);

        Assert.Null(cleared.TeachingGroupId);
        Assert.Equal(1, uow.SaveCount - savesBefore);
        Assert.Empty(await ActiveProjectedSectionIdsAsync(db, entry.Id));
    }

    [Fact]
    public async Task Clear_failed_SaveChanges_does_not_persist_partial_clear()
    {
        var store = "cap-p10-clear-fail-" + Guid.NewGuid().ToString("N");
        var user = new AmbientCurrentUser();
        int entryId;
        int tgId;

        await using (var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(store).Options,
            user,
            NullLogger<ApplicationDbContext>.Instance))
        {
            var projector = new TimetableSectionProjector(db, user);
            var okUow = new CountingUnitOfWork(db);
            var assignOk = new TeachingGroupApplicationService(new TimetableRepository(db), db, okUow, user, projector);
            var (_, entry, tg, secA, secB) = await SeedAsync(db);
            entryId = entry.Id;
            tgId = tg.Id;
            await assignOk.AssignToTimetableEntryAsync(entryId, tgId);
            Assert.Equal(
                new[] { secA.Id, secB.Id }.OrderBy(x => x),
                await ActiveProjectedSectionIdsAsync(db, entryId));

            var assignFail = new TeachingGroupApplicationService(
                new TimetableRepository(db), db, new FailingUnitOfWork(), user, projector);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                assignFail.ClearFromTimetableEntryAsync(entryId));
        }

        await using var verify = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(store).Options,
            user,
            NullLogger<ApplicationDbContext>.Instance);
        var reloaded = await verify.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entryId);
        Assert.Equal(tgId, reloaded.TeachingGroupId);
        Assert.Equal(2, await verify.Set<TimetableSection>()
            .CountAsync(x => x.TimetableEntryId == entryId && !x.IsDeleted));
    }

    [Fact]
    public void Projector_has_no_SaveChanges_or_UnitOfWork()
    {
        var projector = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs"));
        Assert.DoesNotContain("SaveChangesAsync", projector);
        Assert.DoesNotContain("SaveChanges(", projector);
        Assert.DoesNotContain("IUnitOfWork", projector);
        Assert.DoesNotContain("_unitOfWork", projector);
    }

    [Fact]
    public async Task Publish_blocked_performs_zero_mutation()
    {
        var entity = LockedTimetable();
        var unitOfWork = new Mock<IUnitOfWork>();
        var readiness = new Mock<ITimetablePublishReadinessService>();
        readiness.Setup(r => r.EvaluatePublishReadinessAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimetablePublishReadinessResultDto
            {
                TimetableId = 1,
                IsReady = false,
                LifecycleState = TimetableStatus.Locked,
                BlockingFindingCount = 1,
                Findings =
                [
                    new PublishReadinessFindingDto
                    {
                        Code = "ROOM_CAPACITY",
                        Severity = "Error",
                        IsBlocking = true,
                        Title = "ROOM_CAPACITY",
                        Why = "over",
                        RecommendedAction = "fix",
                    }
                ]
            });

        var service = CreateLifecycle(entity, unitOfWork, readiness);
        await Assert.ThrowsAsync<PublishNotReadyException>(() => service.PublishAsync(1, null));

        Assert.Equal(TimetableStatus.Locked, entity.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Publish_ready_performs_one_SaveChanges_after_lifecycle_mutation()
    {
        var entity = LockedTimetable();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var readiness = new Mock<ITimetablePublishReadinessService>();
        readiness.Setup(r => r.EvaluatePublishReadinessAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimetablePublishReadinessResultDto
            {
                TimetableId = 1,
                IsReady = true,
                LifecycleState = TimetableStatus.Locked,
                BlockingFindingCount = 0,
                Findings = []
            });
        var timetableService = new Mock<ITimetableService>();
        timetableService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimetableDto { Id = 1, Status = TimetableStatus.Published, Name = "T", AcademicYearId = 10 });

        var service = CreateLifecycle(entity, unitOfWork, readiness, timetableService);
        await service.PublishAsync(1, null);

        Assert.Equal(TimetableStatus.Published, entity.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Readiness_service_and_GET_never_mutate()
    {
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetablePublishReadinessService.cs"));
        Assert.DoesNotContain("SaveChangesAsync", service);
        Assert.DoesNotContain("IUnitOfWork", service);
        Assert.DoesNotContain("entity.Status =", service);
        Assert.DoesNotContain("new TeachingGroup", service);
        Assert.DoesNotContain("new TimetableSection", service);
        Assert.DoesNotContain("StudentSection", service);
        Assert.DoesNotContain("Attendance", service);

        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs"));
        var getIdx = controller.IndexOf("publish-readiness", StringComparison.Ordinal);
        Assert.True(getIdx > 0);
        var slice = controller.Substring(getIdx, Math.Min(500, controller.Length - getIdx));
        Assert.Contains("EvaluatePublishReadinessAsync", slice);
        Assert.DoesNotContain("SaveChanges", slice);
        Assert.DoesNotContain("PublishAsync", slice);
    }

    [Fact]
    public void Conflict_and_readiness_evaluation_do_not_repair_stale_data()
    {
        var readiness = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TimetablePublishReadinessService.cs"));
        Assert.DoesNotContain("SyncTeachingGroup", readiness);
        Assert.DoesNotContain("ClearTimetableEntryProjection", readiness);
        Assert.DoesNotContain("ReplaceSections", readiness);
        Assert.DoesNotContain("AssignToTimetableEntry", readiness);

        var analyzer = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "Conflicts", "ConflictAnalyzer.cs"));
        Assert.DoesNotContain("SaveChanges", analyzer);
        Assert.DoesNotContain("IUnitOfWork", analyzer);
    }

    [Fact]
    public void Concurrent_membership_unique_violation_maps_to_ConcurrencyConflictException()
    {
        Assert.True(TeachingGroupMembershipPersistenceExceptionMapper.MatchesApprovedMembershipUniqueViolation(
            TeachingGroupMembershipPersistenceExceptionMapper.PostgresUniqueViolationSqlState,
            TeachingGroupMembershipPersistenceExceptionMapper.ApprovedPostgresConstraintName));
        Assert.Equal(
            "A conflicting membership change was detected. Reload and try again.",
            TeachingGroupMembershipPersistenceExceptionMapper.ConflictMessage);
    }

    [Fact]
    public void Concurrent_timetable_entity_concurrency_maps_to_scheduling_conflict()
    {
        Assert.Equal(
            ConcurrencyConflictException.ForSchedulingModule().Message,
            ConcurrencyExceptionHelper.ClassifyConcurrencyConflict(new TimetableEntry())!.Message);
        Assert.Equal(
            ConcurrencyConflictException.ForSchedulingModule().Message,
            ConcurrencyExceptionHelper.ClassifyConcurrencyConflict(new Timetable())!.Message);
        Assert.Equal(
            ConcurrencyConflictException.ForSchedulingModule().Message,
            ConcurrencyExceptionHelper.ClassifyConcurrencyConflict(new TeachingGroup())!.Message);
        Assert.Equal(
            ConcurrencyConflictException.ForSchedulingModule().Message,
            ConcurrencyExceptionHelper.ClassifyConcurrencyConflict(new TimetableSection())!.Message);
        Assert.Equal(
            ConcurrencyConflictException.ForSchedulingModule().Message,
            ConcurrencyExceptionHelper.ClassifyConcurrencyConflict(new TeachingGroupMembership())!.Message);
        Assert.Equal(
            ConcurrencyConflictException.ForAttendanceSession().Message,
            ConcurrencyExceptionHelper.ClassifyConcurrencyConflict(new AttendanceSession())!.Message);
    }

    [Fact]
    public async Task Tenant_isolation_rejects_cross_tenant_assign_without_projection()
    {
        var (db, assign, uow) = CreateAssignSut(tenantId: 1);
        var (_, entry, _, _, _) = await SeedAsync(db, tenantId: 1);
        var foreign = new TeachingGroup
        {
            TenantId = 2,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            SubjectAllocationId = 10,
            Type = TeachingGroupType.Custom,
            MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
            Status = TeachingGroupStatus.Active,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Code = "TG-X",
            Name = "Foreign",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TeachingGroup>().Add(foreign);
        await db.SaveChangesAsync();
        var savesBefore = uow.SaveCount;

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            assign.AssignToTimetableEntryAsync(entry.Id, foreign.Id));

        Assert.Equal(savesBefore, uow.SaveCount);
        Assert.Null((await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
        Assert.Empty(await ActiveProjectedSectionIdsAsync(db, entry.Id));
    }

    [Fact]
    public async Task Lifecycle_rejects_assign_on_non_draft_without_SaveChanges()
    {
        var (db, assign, uow) = CreateAssignSut();
        var (tt, entry, tg, _, _) = await SeedAsync(db);
        tt.Status = TimetableStatus.Published;
        await db.SaveChangesAsync();
        var savesBefore = uow.SaveCount;

        await Assert.ThrowsAsync<DomainException>(() =>
            assign.AssignToTimetableEntryAsync(entry.Id, tg.Id));

        Assert.Equal(savesBefore, uow.SaveCount);
        Assert.Null((await db.Set<TimetableEntry>().AsNoTracking().SingleAsync(e => e.Id == entry.Id)).TeachingGroupId);
        Assert.Empty(await ActiveProjectedSectionIdsAsync(db, entry.Id));
    }

    private static Timetable LockedTimetable() => new()
    {
        Id = 1,
        TenantId = 1,
        AcademicYearId = 10,
        DepartmentId = 3,
        Status = TimetableStatus.Locked,
        Name = "Locked",
        IsFrozen = false,
    };

    private static TimetableLifecycleService CreateLifecycle(
        Timetable entity,
        Mock<IUnitOfWork> unitOfWork,
        Mock<ITimetablePublishReadinessService> readiness,
        Mock<ITimetableService>? timetableService = null)
    {
        var repository = new Mock<ITimetableRepository>();
        repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var context = new Mock<IApplicationDbContext>();
        context.Setup(c => c.SchedulingTimetables).Returns(new[] { entity }.AsAsyncQueryable());
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(x => x.TenantId).Returns(1);
        currentUser.Setup(x => x.UserId).Returns(10);
        var history = new Mock<ITimetableChangeHistoryService>();
        var versions = new Mock<IScheduleVersionRepository>();
        var archiveReasons = new Mock<IArchiveReasonRepository>();
        timetableService ??= new Mock<ITimetableService>();

        return new TimetableLifecycleService(
            repository.Object,
            versions.Object,
            archiveReasons.Object,
            context.Object,
            unitOfWork.Object,
            currentUser.Object,
            history.Object,
            timetableService.Object,
            readiness.Object,
            Mock.Of<FluentValidation.IValidator<FreezeTimetableRequest>>(),
            Mock.Of<FluentValidation.IValidator<UnlockFrozenTimetableRequest>>());
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Infrastructure", "Abhyanvaya.Infrastructure.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}

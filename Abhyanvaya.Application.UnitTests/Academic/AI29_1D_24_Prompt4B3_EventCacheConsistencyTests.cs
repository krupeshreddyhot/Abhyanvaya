using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Validators;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Events;
using Abhyanvaya.Infrastructure.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24 Prompt 4B.3 — actual event dispatch + hierarchy/statistics cache invocation counts
/// via mocks/spies (not decision-flag-only assertions).
/// </summary>
public sealed class AI29_1D_24_Prompt4B3_EventCacheConsistencyTests
{
    private const int TenantId = 1;
    private const int CommerceId = 10;
    private const int ScienceId = 20;

    private sealed class SpyHarness
    {
        public List<Course> Courses { get; } = [];
        public List<Program> Programs { get; } = [];
        public Mock<IApplicationDbContext> Db { get; } = new();
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IAcademicHierarchyCache> HierarchyCache { get; } = new();
        public Mock<IAcademicStatisticsCache> StatisticsCache { get; } = new();
        public Mock<IDomainEventDispatcher> Dispatcher { get; } = new();
        public List<IDomainEvent> Dispatched { get; } = [];
        public int SaveChangesCalls { get; private set; }
        public AcademicCatalogService Catalog { get; }

        public SpyHarness(IDomainEventDispatcher? realDispatcher = null)
        {
            User.SetupGet(u => u.TenantId).Returns(TenantId);
            User.SetupGet(u => u.UserId).Returns(1);

            Programs.Add(new Program
            {
                Id = CommerceId,
                TenantId = TenantId,
                ProgramCode = "COM",
                ProgramName = "Commerce",
                IsActive = true,
                Status = "Active",
            });
            Programs.Add(new Program
            {
                Id = ScienceId,
                TenantId = TenantId,
                ProgramCode = "SCI",
                ProgramName = "Science",
                IsActive = true,
                Status = "Active",
            });

            Courses.Add(new Course
            {
                Id = 1,
                TenantId = TenantId,
                Code = "BCOM",
                Name = "B.Com",
                ProgramId = CommerceId,
            });

            var configs = new List<TenantAcademicConfiguration>
            {
                new() { Id = 1, TenantId = TenantId, EnablePrograms = true, CollegeId = 1 },
            };

            Db.Setup(d => d.Courses).Returns(Courses.AsAsyncQueryable());
            Db.Setup(d => d.Programs).Returns(Programs.AsAsyncQueryable());
            Db.Setup(d => d.TenantAcademicConfigurations).Returns(configs.AsAsyncQueryable());
            Db.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    SaveChangesCalls++;
                    return 1;
                });

            HierarchyCache.Setup(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            StatisticsCache.Setup(c => c.InvalidateAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            IDomainEventDispatcher dispatcher;
            if (realDispatcher is not null)
            {
                dispatcher = realDispatcher;
            }
            else
            {
                Dispatcher
                    .Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()))
                    .Returns<IReadOnlyCollection<IDomainEvent>, CancellationToken>((events, _) =>
                    {
                        Dispatched.AddRange(events);
                        return Task.CompletedTask;
                    });
                dispatcher = Dispatcher.Object;
            }

            Catalog = new AcademicCatalogService(
                Db.Object,
                User.Object,
                HierarchyCache.Object,
                StatisticsCache.Object,
                dispatcher,
                new CreateProgramRequestValidator(),
                new UpdateProgramRequestValidator(),
                new AssignCourseProgramRequestValidator(),
                new UpsertProgramPolicyRequestValidator());
        }

        public void RefreshCoursesQueryable()
            => Db.Setup(d => d.Courses).Returns(Courses.AsAsyncQueryable());
    }

    /// <summary>Throws after metrics — proves dispatcher swallow contract.</summary>
    private sealed class ThrowingCourseAssignedHandler : IDomainEventHandler<CourseAssigned>
    {
        public int Invocations { get; private set; }

        public Task HandleAsync(CourseAssigned domainEvent, CancellationToken cancellationToken = default)
        {
            Invocations++;
            throw new InvalidOperationException("simulated handler failure after persistence");
        }
    }

    [Fact]
    public async Task Commerce_To_Commerce_Zero_Events_Zero_Cache_Invalidations()
    {
        var h = new SpyHarness();
        var savesBefore = h.SaveChangesCalls;

        var outcome = await h.Catalog.AssignCourseToProgramAsync(
            new AssignCourseProgramRequest { CourseId = 1, ProgramId = CommerceId });

        Assert.True(outcome.IsNoOp);
        Assert.Equal(0, outcome.DomainEventsDispatched);
        Assert.Equal(0, outcome.HierarchyCacheInvalidations);
        Assert.Equal(0, outcome.StatisticsCacheInvalidations);

        // Actual spy counts — not decision flags alone.
        Assert.Empty(h.Dispatched.OfType<CourseAssigned>());
        Assert.Empty(h.Dispatched.OfType<CourseRemoved>());
        Assert.Empty(h.Dispatched);

        h.Dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal(savesBefore, h.SaveChangesCalls);
        Assert.Equal(CommerceId, h.Courses.Single().ProgramId);
    }

    [Fact]
    public async Task Commerce_To_Science_Exactly_One_Assigned_Event_And_One_Each_Cache()
    {
        var h = new SpyHarness();

        var outcome = await h.Catalog.AssignCourseToProgramAsync(
            new AssignCourseProgramRequest { CourseId = 1, ProgramId = ScienceId });

        Assert.False(outcome.IsNoOp);
        Assert.Equal(1, outcome.DomainEventsDispatched);
        Assert.Equal(1, outcome.HierarchyCacheInvalidations);
        Assert.Equal(1, outcome.StatisticsCacheInvalidations);

        Assert.Single(h.Dispatched);
        var assigned = Assert.IsType<CourseAssigned>(h.Dispatched.Single());
        Assert.Equal(1, assigned.CourseId);
        Assert.Equal(ScienceId, assigned.ProgramId);
        Assert.Empty(h.Dispatched.OfType<CourseRemoved>());

        h.Dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<IReadOnlyCollection<IDomainEvent>>(e =>
                    e.Count == 1 && e.OfType<CourseAssigned>().Count() == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(ScienceId, h.Courses.Single().ProgramId);
    }

    [Fact]
    public async Task Commerce_To_Null_Exactly_One_Removed_Event_And_One_Each_Cache()
    {
        var h = new SpyHarness();

        var outcome = await h.Catalog.AssignCourseToProgramAsync(
            new AssignCourseProgramRequest { CourseId = 1, ProgramId = null });

        Assert.Equal(1, outcome.DomainEventsDispatched);
        Assert.Equal(1, outcome.HierarchyCacheInvalidations);
        Assert.Equal(1, outcome.StatisticsCacheInvalidations);

        Assert.Single(h.Dispatched);
        var removed = Assert.IsType<CourseRemoved>(h.Dispatched.Single());
        Assert.Equal(1, removed.CourseId);
        Assert.Equal(CommerceId, removed.PreviousProgramId);
        Assert.Empty(h.Dispatched.OfType<CourseAssigned>());

        h.Dispatcher.Verify(
            d => d.DispatchAsync(
                It.Is<IReadOnlyCollection<IDomainEvent>>(e =>
                    e.Count == 1 && e.OfType<CourseRemoved>().Count() == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(h.Courses.Single().ProgramId);
    }

    [Fact]
    public async Task Handler_Failure_After_Persist_Does_Not_Fail_Assign_Or_Rollback_ProgramId()
    {
        var throwing = new ThrowingCourseAssignedHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<CourseAssigned>>(throwing);
        var sp = services.BuildServiceProvider();
        var realDispatcher = new DomainEventDispatcher(sp, NullLogger<DomainEventDispatcher>.Instance);

        var h = new SpyHarness(realDispatcher);
        var savesBefore = h.SaveChangesCalls;

        // Must not throw despite handler failure (dispatcher swallows handler exceptions).
        var outcome = await h.Catalog.AssignCourseToProgramAsync(
            new AssignCourseProgramRequest { CourseId = 1, ProgramId = ScienceId });

        Assert.True(outcome.ProgramIdChanged);
        Assert.Equal(ScienceId, h.Courses.Single().ProgramId);
        Assert.True(h.SaveChangesCalls > savesBefore);
        Assert.Equal(1, throwing.Invocations);

        // Cache invalidation still runs after dispatch (existing order in Assign).
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void DomainEventDispatcher_Source_Documents_Handler_Swallow_Contract()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Abhyanvaya.Infrastructure", "DomainEvents", "DomainEventDispatcher.cs"));
        var source = File.ReadAllText(path);
        Assert.Contains("must never fail the", source);
        Assert.Contains("catch (Exception ex)", source);
    }
}

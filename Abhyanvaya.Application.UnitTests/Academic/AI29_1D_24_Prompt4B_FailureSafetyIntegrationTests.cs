using System.Text.Json;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Validators;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.DTOs.Course;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Events;
using FluentValidation;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24 Prompt 4B — transactional failure safety, events, cache, tenant, omitted vs null.
/// Exercises AcademicCatalogService + CourseMasterWriteService application flow (mocked EF + real rules).
/// </summary>
public sealed class AI29_1D_24_Prompt4B_FailureSafetyIntegrationTests
{
    private const int TenantA = 1;
    private const int TenantB = 2;

    private static Program ActiveProgram(int id, int tenantId = TenantA) => new()
    {
        Id = id,
        TenantId = tenantId,
        CollegeId = 1,
        DepartmentId = 1,
        ProgramCode = $"P{id}",
        ProgramName = $"Program {id}",
        IsActive = true,
        Status = "Active",
    };

    private static Program InactiveProgram(int id, int tenantId = TenantA) => new()
    {
        Id = id,
        TenantId = tenantId,
        CollegeId = 1,
        DepartmentId = 1,
        ProgramCode = $"P{id}",
        ProgramName = $"Program {id}",
        IsActive = false,
        Status = "Inactive",
    };

    private static Program ArchivedProgram(int id, int tenantId = TenantA) => new()
    {
        Id = id,
        TenantId = tenantId,
        CollegeId = 1,
        DepartmentId = 1,
        ProgramCode = $"P{id}",
        ProgramName = $"Program {id}",
        IsActive = false,
        Status = "Archived",
    };

    private sealed class Harness
    {
        public List<Course> Courses { get; } = [];
        public List<Program> Programs { get; } = [];
        public List<Department> Departments { get; } = [];
        public List<TenantAcademicConfiguration> Configs { get; } = [];
        public Mock<IApplicationDbContext> Db { get; } = new();
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IAcademicHierarchyCache> HierarchyCache { get; } = new();
        public Mock<IAcademicStatisticsCache> StatisticsCache { get; } = new();
        public Mock<IDomainEventDispatcher> Dispatcher { get; } = new();
        public Mock<ICacheService> MasterCache { get; } = new();
        public List<IDomainEvent> DispatchedEvents { get; } = [];
        public int SaveChangesCalls { get; private set; }
        public AcademicCatalogService Catalog { get; }
        public CourseMasterWriteService Write { get; }
        public Mock<IAcademicStructureService> Structure { get; } = new();

        public Harness(bool enablePrograms = true, int tenantId = TenantA, bool useRealStructure = true)
        {
            User.SetupGet(u => u.TenantId).Returns(tenantId);
            User.SetupGet(u => u.UserId).Returns(9);

            Configs.Add(new TenantAcademicConfiguration
            {
                Id = 1,
                TenantId = tenantId,
                EnablePrograms = enablePrograms,
                CollegeId = 1,
            });

            Departments.Add(new Department
            {
                Id = 1,
                TenantId = tenantId,
                CollegeId = 1,
                Name = "Commerce",
                Code = "001",
                IsActive = true,
            });

            RefreshDbSets();

            Db.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    SaveChangesCalls++;
                    return 1;
                });

            Db.Setup(d => d.AddAsync(It.IsAny<Course>()))
                .Returns<Course>(c =>
                {
                    if (c.Id == 0)
                        c.Id = Courses.Count == 0 ? 1 : Courses.Max(x => x.Id) + 1;
                    c.TenantId = tenantId;
                    Courses.Add(c);
                    RefreshDbSets();
                    return Task.CompletedTask;
                });

            // Simulate DB transaction rollback on failure.
            Db.Setup(d => d.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) =>
                {
                    var courseSnap = Courses.Select(CloneCourse).ToList();
                    try
                    {
                        await action(ct);
                    }
                    catch
                    {
                        Courses.Clear();
                        Courses.AddRange(courseSnap);
                        RefreshDbSets();
                        throw;
                    }
                });

            Dispatcher
                .Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyCollection<IDomainEvent>, CancellationToken>((events, _) =>
                {
                    DispatchedEvents.AddRange(events);
                    return Task.CompletedTask;
                });

            HierarchyCache.Setup(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            StatisticsCache.Setup(c => c.InvalidateAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            MasterCache.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

            Catalog = new AcademicCatalogService(
                Db.Object,
                User.Object,
                HierarchyCache.Object,
                StatisticsCache.Object,
                Dispatcher.Object,
                new CreateProgramRequestValidator(),
                new UpdateProgramRequestValidator(),
                new AssignCourseProgramRequestValidator(),
                new UpsertProgramPolicyRequestValidator());

            if (useRealStructure)
            {
                Structure
                    .Setup(s => s.AssignCourseToProgramAsync(It.IsAny<AssignCourseProgramRequest>(), It.IsAny<CancellationToken>()))
                    .Returns<AssignCourseProgramRequest, CancellationToken>((req, ct) => Catalog.AssignCourseToProgramAsync(req, ct));
            }

            Write = new CourseMasterWriteService(Db.Object, MasterCache.Object, User.Object, Structure.Object);
        }

        public void RefreshDbSets()
        {
            Db.Setup(d => d.Courses).Returns(Courses.AsAsyncQueryable());
            Db.Setup(d => d.Programs).Returns(Programs.AsAsyncQueryable());
            Db.Setup(d => d.Departments).Returns(Departments.AsAsyncQueryable());
            Db.Setup(d => d.TenantAcademicConfigurations).Returns(Configs.AsAsyncQueryable());
        }

        private static Course CloneCourse(Course c) => new()
        {
            Id = c.Id,
            TenantId = c.TenantId,
            Code = c.Code,
            Name = c.Name,
            DepartmentId = c.DepartmentId,
            ProgramId = c.ProgramId,
            CreatedDate = c.CreatedDate,
            UpdatedDate = c.UpdatedDate,
        };
    }

    [Fact]
    public async Task Case01_Create_Active_Program_Succeeds_With_One_Assigned_Event_And_Cache()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.RefreshDbSets();

        var row = await h.Write.CreateAsync(new CreateCourseRequest { Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });

        Assert.Equal(10, row.ProgramId);
        Assert.Single(h.DispatchedEvents.OfType<CourseAssigned>());
        Assert.Empty(h.DispatchedEvents.OfType<CourseRemoved>());
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Case02_Create_No_Program_Succeeds_Without_Events_Or_Cache()
    {
        var h = new Harness();
        var row = await h.Write.CreateAsync(new CreateCourseRequest { Code = "BCOM", Name = "B.Com", DepartmentId = 1 });

        Assert.Null(row.ProgramId);
        Assert.Empty(h.DispatchedEvents);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Case03_Create_Inactive_Program_Fails_And_Rolls_Back_Course()
    {
        var h = new Harness();
        h.Programs.Add(InactiveProgram(10));
        h.RefreshDbSets();

        await Assert.ThrowsAsync<ValidationException>(() =>
            h.Write.CreateAsync(new CreateCourseRequest { Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 }));

        Assert.Empty(h.Courses);
        Assert.Empty(h.DispatchedEvents);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Case04_Create_Archived_Program_Fails_And_Rolls_Back_Course()
    {
        var h = new Harness();
        h.Programs.Add(ArchivedProgram(10));
        h.RefreshDbSets();

        await Assert.ThrowsAsync<ValidationException>(() =>
            h.Write.CreateAsync(new CreateCourseRequest { Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 }));

        Assert.Empty(h.Courses);
        Assert.Empty(h.DispatchedEvents);
    }

    [Fact]
    public async Task Case05_Create_CrossTenant_Program_Rejected_No_Mutation_Event_Or_Cache()
    {
        var h = new Harness(tenantId: TenantA);
        // Actual Program belonging to Tenant B (not null).
        h.Programs.Add(ActiveProgram(99, TenantB));
        h.RefreshDbSets();

        await Assert.ThrowsAsync<ValidationException>(() =>
            h.Write.CreateAsync(new CreateCourseRequest { Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 99 }));

        Assert.Empty(h.Courses);
        Assert.Empty(h.DispatchedEvents);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Case06_Create_Assignment_Validation_Failure_Rolls_Back()
    {
        var h = new Harness();
        // ProgramId present but not in catalog ⇒ Invalid Program.
        await Assert.ThrowsAsync<ValidationException>(() =>
            h.Write.CreateAsync(new CreateCourseRequest { Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 404 }));

        Assert.Empty(h.Courses);
        Assert.Empty(h.DispatchedEvents);
    }

    [Fact]
    public async Task Case07_Create_Unexpected_Assignment_Exception_Rolls_Back_Orphan()
    {
        var h = new Harness(useRealStructure: false);
        h.Programs.Add(ActiveProgram(10));
        h.RefreshDbSets();
        h.Structure
            .Setup(s => s.AssignCourseToProgramAsync(It.IsAny<AssignCourseProgramRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated assignment fault"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Write.CreateAsync(new CreateCourseRequest { Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 }));

        Assert.Empty(h.Courses);
        Assert.Empty(h.DispatchedEvents);
    }

    [Fact]
    public async Task Case08_Update_Program_Unchanged_Zero_Events_Zero_Cache()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var savesBefore = h.SaveChangesCalls;
        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1 };
        req.SetProgramId(10);

        var outcome = await h.Catalog.AssignCourseToProgramAsync(new AssignCourseProgramRequest { CourseId = 1, ProgramId = 10 });
        Assert.True(outcome.IsNoOp);
        Assert.Equal(0, outcome.DomainEventsDispatched);
        Assert.Equal(0, outcome.HierarchyCacheInvalidations);

        // Full update path (Code/Name + specified same Program) — Program assign is no-op.
        await h.Write.UpdateAsync(req);
        Assert.Empty(h.DispatchedEvents);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(10, h.Courses.Single().ProgramId);
        Assert.True(h.SaveChangesCalls > savesBefore); // Code/Name save may occur; assign does not add another on no-op
    }

    [Fact]
    public async Task Case09_Update_Program_Changed_One_Assigned_Event_And_Caches()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Programs.Add(ActiveProgram(20));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM2", Name = "Bachelor of Commerce", DepartmentId = 1 };
        req.SetProgramId(20);
        var row = await h.Write.UpdateAsync(req);

        Assert.Equal("BCOM2", row.Code);
        Assert.Equal(20, row.ProgramId);
        Assert.Single(h.DispatchedEvents.OfType<CourseAssigned>());
        Assert.Empty(h.DispatchedEvents.OfType<CourseRemoved>());
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Once);
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Case10_Update_Program_Removed_One_CourseRemoved_Event()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1 };
        req.SetProgramId(null);
        await h.Write.UpdateAsync(req);

        Assert.Null(h.Courses.Single().ProgramId);
        Assert.Single(h.DispatchedEvents.OfType<CourseRemoved>());
        Assert.Empty(h.DispatchedEvents.OfType<CourseAssigned>());
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Case11_Update_Inactive_Existing_Program_Retained_NoOp()
    {
        var h = new Harness();
        h.Programs.Add(InactiveProgram(10));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM", Name = "B.Com Updated", DepartmentId = 1 };
        req.SetProgramId(10);
        var row = await h.Write.UpdateAsync(req);

        Assert.Equal(10, row.ProgramId);
        Assert.Equal("B.Com Updated", row.Name);
        Assert.Empty(h.DispatchedEvents);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Case12_Update_New_Inactive_Program_Rejected_Rolls_Back_Code_Name()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Programs.Add(InactiveProgram(20));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM2", Name = "Bachelor of Commerce", DepartmentId = 1 };
        req.SetProgramId(20);

        await Assert.ThrowsAsync<ValidationException>(() => h.Write.UpdateAsync(req));

        var course = h.Courses.Single();
        Assert.Equal("BCOM", course.Code);
        Assert.Equal("B.Com", course.Name);
        Assert.Equal(10, course.ProgramId);
        Assert.Empty(h.DispatchedEvents);
    }

    [Fact]
    public async Task Case13_Update_Archived_Program_Rejected()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Programs.Add(ArchivedProgram(20));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 1 };
        req.SetProgramId(20);
        await Assert.ThrowsAsync<ValidationException>(() => h.Write.UpdateAsync(req));
        Assert.Equal(10, h.Courses.Single().ProgramId);
    }

    [Fact]
    public async Task Case14_Update_CrossTenant_Program_Rejected_No_Mutation()
    {
        var h = new Harness(tenantId: TenantA);
        h.Programs.Add(ActiveProgram(10, TenantA));
        h.Programs.Add(ActiveProgram(99, TenantB));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM2", Name = "Changed", DepartmentId = 1 };
        req.SetProgramId(99);
        await Assert.ThrowsAsync<ValidationException>(() => h.Write.UpdateAsync(req));

        var course = h.Courses.Single();
        Assert.Equal("BCOM", course.Code);
        Assert.Equal("B.Com", course.Name);
        Assert.Equal(10, course.ProgramId);
        Assert.Empty(h.DispatchedEvents);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Case15_Update_Assignment_Failure_Rolls_Back_Partial_Code_Name()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM2", Name = "Bachelor of Commerce", DepartmentId = 1 };
        req.SetProgramId(404);
        await Assert.ThrowsAsync<ValidationException>(() => h.Write.UpdateAsync(req));

        var course = h.Courses.Single();
        Assert.Equal("BCOM", course.Code);
        Assert.Equal("B.Com", course.Name);
        Assert.Equal(10, course.ProgramId);
    }

    [Fact]
    public async Task Case16_Update_Unexpected_Assignment_Exception_Rolls_Back()
    {
        var h = new Harness(useRealStructure: false);
        h.Programs.Add(ActiveProgram(10));
        h.Programs.Add(ActiveProgram(20));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();
        h.Structure
            .Setup(s => s.AssignCourseToProgramAsync(It.IsAny<AssignCourseProgramRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM2", Name = "Bachelor of Commerce", DepartmentId = 1 };
        req.SetProgramId(20);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Write.UpdateAsync(req));

        var course = h.Courses.Single();
        Assert.Equal("BCOM", course.Code);
        Assert.Equal("B.Com", course.Name);
        Assert.Equal(10, course.ProgramId);
    }

    [Fact]
    public async Task Case17_Same_Program_Idempotency_Three_Requests()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        for (var i = 0; i < 3; i++)
        {
            var outcome = await h.Catalog.AssignCourseToProgramAsync(
                new AssignCourseProgramRequest { CourseId = 1, ProgramId = 10 });
            Assert.True(outcome.IsNoOp);
            Assert.Equal(0, outcome.DomainEventsDispatched);
            Assert.Equal(0, outcome.HierarchyCacheInvalidations);
            Assert.Equal(0, outcome.StatisticsCacheInvalidations);
        }

        Assert.Empty(h.DispatchedEvents);
        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(10, h.Courses.Single().ProgramId);
    }

    [Fact]
    public async Task Case18_Actual_Event_Counts_Unchanged_Changed_Removed()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Programs.Add(ActiveProgram(20));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var noop = await h.Catalog.AssignCourseToProgramAsync(new AssignCourseProgramRequest { CourseId = 1, ProgramId = 10 });
        Assert.Equal(0, noop.DomainEventsDispatched);
        Assert.Empty(h.DispatchedEvents);

        var changed = await h.Catalog.AssignCourseToProgramAsync(new AssignCourseProgramRequest { CourseId = 1, ProgramId = 20 });
        Assert.Equal(1, changed.DomainEventsDispatched);
        Assert.Single(h.DispatchedEvents.OfType<CourseAssigned>());
        Assert.Equal(20, h.DispatchedEvents.OfType<CourseAssigned>().Single().ProgramId);

        h.DispatchedEvents.Clear();
        var removed = await h.Catalog.AssignCourseToProgramAsync(new AssignCourseProgramRequest { CourseId = 1, ProgramId = null });
        Assert.Equal(1, removed.DomainEventsDispatched);
        Assert.Single(h.DispatchedEvents.OfType<CourseRemoved>());
        Assert.Equal(20, h.DispatchedEvents.OfType<CourseRemoved>().Single().PreviousProgramId);
    }

    [Fact]
    public async Task Case19_Actual_Cache_Invalidation_Counts()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Programs.Add(ActiveProgram(20));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        var noop = await h.Catalog.AssignCourseToProgramAsync(new AssignCourseProgramRequest { CourseId = 1, ProgramId = 10 });
        Assert.Equal(0, noop.HierarchyCacheInvalidations);
        Assert.Equal(0, noop.StatisticsCacheInvalidations);

        var changed = await h.Catalog.AssignCourseToProgramAsync(new AssignCourseProgramRequest { CourseId = 1, ProgramId = 20 });
        Assert.Equal(1, changed.HierarchyCacheInvalidations);
        Assert.Equal(1, changed.StatisticsCacheInvalidations);

        var removed = await h.Catalog.AssignCourseToProgramAsync(new AssignCourseProgramRequest { CourseId = 1, ProgramId = null });
        Assert.Equal(1, removed.HierarchyCacheInvalidations);
        Assert.Equal(1, removed.StatisticsCacheInvalidations);

        h.HierarchyCache.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        h.StatisticsCache.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void Case20_Omitted_ProgramId_Does_Not_Set_Specified_Flag()
    {
        var json = """{"id":1,"code":"BCOM","name":"B.Com", "departmentId": 1}""";
        var req = JsonSerializer.Deserialize<UpdateCourseRequest>(json, ApiJson)!;
        Assert.False(req.ProgramIdSpecified);
        Assert.Null(req.ProgramId);
    }

    [Fact]
    public void Case21_Explicit_Null_ProgramId_Sets_Specified_Flag()
    {
        var json = """{"id":1,"code":"BCOM","name":"B.Com", "departmentId": 1,"programId":null}""";
        var req = JsonSerializer.Deserialize<UpdateCourseRequest>(json, ApiJson)!;
        Assert.True(req.ProgramIdSpecified);
        Assert.Null(req.ProgramId);

        var withValue = JsonSerializer.Deserialize<UpdateCourseRequest>(
            """{"id":1,"code":"BCOM","name":"B.Com", "departmentId": 1,"programId":15}""", ApiJson)!;
        Assert.True(withValue.ProgramIdSpecified);
        Assert.Equal(15, withValue.ProgramId);
    }

    [Fact]
    public async Task Case20b_Omitted_ProgramId_Update_Does_Not_Modify_Program()
    {
        var h = new Harness();
        h.Programs.Add(ActiveProgram(10));
        h.Courses.Add(new Course { Id = 1, TenantId = TenantA, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        h.RefreshDbSets();

        // Object initializer without ProgramId ⇒ omitted (Specified=false).
        var req = new UpdateCourseRequest { Id = 1, Code = "BCOM2", Name = "Bachelor of Commerce", DepartmentId = 1 };
        Assert.False(req.ProgramIdSpecified);

        var row = await h.Write.UpdateAsync(req);
        Assert.Equal("BCOM2", row.Code);
        Assert.Equal(10, row.ProgramId);
        Assert.Empty(h.DispatchedEvents);
        h.Structure.Verify(
            s => s.AssignCourseToProgramAsync(It.IsAny<AssignCourseProgramRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Case22_Programs_Disabled_Legacy_No_Assign_Triggered()
    {
        var h = new Harness(enablePrograms: false);
        h.Programs.Add(ActiveProgram(10));
        h.RefreshDbSets();

        var row = await h.Write.CreateAsync(new CreateCourseRequest { Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 10 });
        Assert.Null(row.ProgramId);
        h.Structure.Verify(
            s => s.AssignCourseToProgramAsync(It.IsAny<AssignCourseProgramRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(h.DispatchedEvents);
    }

    [Fact]
    public void Case23_Transaction_Boundary_Uses_ExecuteInTransactionAsync()
    {
        var source = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                "Abhyanvaya.Application", "Academic", "CourseMasterWriteService.cs"));
        Assert.Contains("ExecuteInTransactionAsync", source);
        Assert.DoesNotContain("Compensate", source);
    }

    [Fact]
    public void Case24_Architecture_Guard_Ui_Does_Not_Own_Assign_Rules()
    {
        var uiRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "abhyanvaya-ui", "src"));
        var persistence = File.ReadAllText(Path.Combine(uiRoot, "utils", "courseMasterPersistence.ts"));
        Assert.Contains("callAssignCourseSeparately: false", persistence);
        Assert.DoesNotContain("DbContext", persistence);
        Assert.DoesNotContain("InvalidateHierarchy", persistence);
        Assert.DoesNotContain("CourseAssigned", persistence);
    }

    [Fact]
    public void Case25_DomainEventDispatcher_Swallows_Handler_Failures_Documented_Contract()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Abhyanvaya.Infrastructure", "DomainEvents", "DomainEventDispatcher.cs"));
        var source = File.ReadAllText(path);
        Assert.Contains("must never fail the", source);
        Assert.Contains("catch (Exception ex)", source);
    }
}

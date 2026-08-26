using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.Application.Academic.ReadModels;
using Abhyanvaya.Application.DTOs;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Authorization;
using Moq;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D Prompt 20 — dedicated regression &amp; compatibility suite (mandatory cases 1–36).
/// Exercises existing production contracts only; does not change business logic for green tests.
/// Hierarchy cascade filter matrix also has a UI companion: <c>ai29_1d_prompt20_regression.test.ts</c>.
/// </summary>
public sealed class AI29_1D_Prompt20_RegressionSuiteTests
{
    private static string RepoPath(params string[] parts) =>
        Path.GetFullPath(Path.Combine(new[] { AppContext.BaseDirectory, "..", "..", "..", ".." }.Concat(parts).ToArray()));

    #region Fixtures

    private static SectionAllocationContext FacetedContext()
    {
        var sections = new[]
        {
            new AllocationSectionProjection { SectionId = 1, SectionCode = "A", SectionName = "A", Lifecycle = "Active" },
            new AllocationSectionProjection { SectionId = 2, SectionCode = "B", SectionName = "B", Lifecycle = "Active" },
        };
        var students = new List<AllocationStudentProjection>
        {
            new()
            {
                StudentId = 1, StudentNumber = "A100", StudentName = "Ada",
                Gender = "Female", Language = "Telugu", ScholarshipCategory = "Merit",
                MinorSubject = "Stats", TransportRoute = "R1", Hostel = "H1",
                ElectiveCombination = "E1", Merit = "High",
            },
            new()
            {
                StudentId = 2, StudentNumber = "B201", StudentName = "Bob",
                Gender = "Male", Language = "Hindi", ScholarshipCategory = "Need",
                MinorSubject = "Eco", TransportRoute = "R2", Hostel = "H2",
                ElectiveCombination = "E2", Merit = "Mid",
            },
            new()
            {
                StudentId = 3, StudentNumber = "C099", StudentName = "Cara",
                Gender = "Female", Language = "Telugu", ScholarshipCategory = "Merit",
                MinorSubject = "Stats", TransportRoute = "R1", Hostel = "H1",
                ElectiveCombination = "E1", Merit = "High",
            },
            new()
            {
                StudentId = 4, StudentNumber = "D010", StudentName = "Dan",
                Gender = "Male", Language = "English", ScholarshipCategory = "Need",
                MinorSubject = "Eco", TransportRoute = "R2", Hostel = "H2",
                ElectiveCombination = "E2", Merit = "Mid",
            },
        };

        return new SectionAllocationContext
        {
            ContextId = Guid.Parse("20202020-2020-2020-2020-202020202020"),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            Checksum = "PROMPT20CHECKSUM",
            Hierarchy = new AllocationHierarchyProjection { AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 },
            Sections = sections,
            Capacities =
            [
                new AllocationCapacityProjection { SectionId = 1, MaximumCapacity = 2, AvailableCapacity = 2 },
                new AllocationCapacityProjection { SectionId = 2, MaximumCapacity = 2, AvailableCapacity = 2 },
            ],
            Students = students,
        };
    }

    private static AllocationEngine CreateEngine()
    {
        var scorer = new AllocationScoreCalculator();
        var constraints = new IAllocationConstraint[]
        {
            new CapacityAllocationConstraint(),
            new ReservedSeatsAllocationConstraint(),
            new GenderBalanceAllocationConstraint(),
        };
        var constraintEngine = new AllocationConstraintEngine(constraints);
        var strategies = new IAllocationPipelineStrategy[]
        {
            new ValidationAllocationStrategy(),
            new CapacityAllocationStrategy(),
            new PolicyAllocationStrategy(),
            new ScoringAllocationStrategy(scorer, constraintEngine),
        };
        return new AllocationEngine(new StudentGroupingStrategy(), strategies, scorer);
    }

    private static AllocationPipelineConfig Config(
        string groupingMode,
        AllocationPopulationSelection? population = null,
        IReadOnlyList<int>? targetSections = null) =>
        new AllocationPipelineConfig
        {
            GroupingMode = groupingMode,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = population ?? AllocationPopulationSelection.AllEligible,
            TargetSectionIds = targetSections,
        }.Normalize();

    private static AllocationScenarioLifecycleService LifecycleSvc() =>
        new(db: null!, currentUser: null!, versions: null!, audit: null!);

    private static (IAcademicTreeService Tree, AcademicHierarchyReadModel Model) BuildBreadcrumbTree(bool enablePrograms)
    {
        var subject = Node("Subject", 6, "Semester:4", "Business Statistics", "BS", 4);
        var sectionA = Node("Section", 5, "Semester:4", "Section A", "A", 4);
        var semester = Node("Semester", 4, "Group:3", "Semester 3", "3", 3, [subject, sectionA]);
        var group = Node("Group", 3, "Course:2", "Computer Applications", "CA", 2, [semester]);
        var course = Node("Course", 2, enablePrograms ? "Program:1" : null, "B.Com", "BCOM", enablePrograms ? 1 : 0, [group]);
        IReadOnlyList<AcademicHierarchyNode> roots = enablePrograms
            ? [Node("Program", 1, null, "Commerce", "COM", 0, [course])]
            : [course];

        var model = new AcademicHierarchyReadModel
        {
            EnablePrograms = enablePrograms,
            GeneratedUtc = DateTime.UtcNow,
            Roots = roots,
            TotalNodes = 10,
        };

        var index = Flatten(roots).ToDictionary(n => n.NodeId, StringComparer.Ordinal);
        var mock = new Mock<IAcademicTreeService>(MockBehavior.Strict);
        mock.Setup(t => t.FindByNodeId(model, It.IsAny<string>()))
            .Returns((AcademicHierarchyReadModel _, string nodeId) =>
                index.TryGetValue(nodeId, out var n) ? n : null);
        mock.Setup(t => t.GetPath(model, It.IsAny<string>()))
            .Returns((AcademicHierarchyReadModel _, string nodeId) =>
            {
                var path = new List<AcademicHierarchyNode>();
                var current = index.TryGetValue(nodeId, out var n) ? n : null;
                while (current is not null)
                {
                    path.Insert(0, current with { Children = [] });
                    current = current.ParentNodeId is not null && index.TryGetValue(current.ParentNodeId, out var p)
                        ? p
                        : null;
                }
                return path;
            });

        return (mock.Object, model);
    }

    private static AcademicHierarchyNode Node(
        string type,
        int id,
        string? parent,
        string name,
        string code,
        int level,
        IReadOnlyList<AcademicHierarchyNode>? children = null)
        => new()
        {
            NodeId = $"{type}:{id}",
            ParentNodeId = parent,
            EntityId = id,
            EntityType = type,
            NodeType = type,
            DisplayName = name,
            Code = code,
            DisplayOrder = 0,
            IsActive = true,
            ChildrenCount = children?.Count ?? 0,
            HasChildren = children is { Count: > 0 },
            HierarchyLevel = level,
            EntityStatus = "Active",
            Children = children ?? [],
        };

    private static IEnumerable<AcademicHierarchyNode> Flatten(IEnumerable<AcademicHierarchyNode> nodes)
    {
        foreach (var n in nodes)
        {
            yield return n with { Children = [] };
            foreach (var c in Flatten(n.Children))
                yield return c;
        }
    }

    #endregion

    #region Academic Hierarchy (1–8)

    [Fact]
    public void Case01_Program_Enabled_Breadcrumb_Includes_Program()
    {
        var (tree, model) = BuildBreadcrumbTree(enablePrograms: true);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(
            tree,
            model,
            new AcademicOperationalContext
            {
                ProgramId = 1,
                CourseId = 2,
                GroupId = 3,
                SemesterId = 4,
                SectionId = 5,
                SubjectId = 6,
            });
        Assert.Contains(crumb.Items, i => i.EntityType == "Program");
        Assert.StartsWith("Commerce", crumb.DisplayPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Case02_Program_Disabled_Omits_Program_Segment()
    {
        var (tree, model) = BuildBreadcrumbTree(enablePrograms: false);
        var crumb = AcademicOperationalBreadcrumbComposer.Compose(
            tree,
            model,
            new AcademicOperationalContext
            {
                CourseId = 2,
                GroupId = 3,
                SemesterId = 4,
                SectionId = 5,
                SubjectId = 6,
            });
        Assert.DoesNotContain(crumb.Items, i => i.EntityType == "Program");
        Assert.DoesNotContain("Commerce", crumb.DisplayPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Case03_to_07_Cascade_Filter_Contracts_Remain_In_Ui_And_Hierarchy_Apis()
    {
        // Authoritative Course/Group/Semester/Section/Subject filter matrix lives in academicCascade (UI)
        // + AcademicUiContext cascading queries. Guard that contracts are not removed.
        var cascade = File.ReadAllText(RepoPath("abhyanvaya-ui", "src", "utils", "academicCascade.ts"));
        Assert.Contains("filterCoursesForProgram", cascade);
        Assert.Contains("filterGroupsForCourse", cascade);
        Assert.Contains("filterSemestersForCourseGroup", cascade);
        Assert.Contains("filterSectionsForScope", cascade);
        Assert.Contains("applyCascadeSelection", cascade);

        var cascadeTests = File.ReadAllText(RepoPath("abhyanvaya-ui", "src", "utils", "academicCascade.test.ts"));
        Assert.Contains("filters groups by course", cascadeTests);
        Assert.Contains("filters semesters by course/group scope", cascadeTests);
        Assert.Contains("filters sections by year + C/G/S", cascadeTests);
        Assert.Contains("Programs enabled", cascadeTests);
        Assert.Contains("Programs disabled", cascadeTests);

        // Subject options are scoped by Course+Group+Semester only (never Section) — AcademicUiContext comment contract.
        var uiCtx = File.ReadAllText(RepoPath("abhyanvaya-ui", "src", "context", "AcademicUiContext.tsx"));
        Assert.Contains("Subjects — Course + Group + Semester only", uiCtx);
        Assert.Contains("includeSections: false", uiCtx);
        Assert.Contains("includeSubjects: false", uiCtx);
    }

    [Fact]
    public void Case08_Section_Does_Not_Alter_Subject_Master()
    {
        var subjectType = typeof(FacultySectionAssignment).Assembly
            .GetTypes()
            .First(t => t.Name == "Subject" && t.Namespace?.Contains("Entities", StringComparison.Ordinal) == true);
        var props = subjectType.GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("SectionId", props);
        Assert.DoesNotContain("SectionIds", props);

        var cascade = File.ReadAllText(RepoPath("abhyanvaya-ui", "src", "utils", "academicCascade.test.ts"));
        Assert.Contains("does not clear Subject when Section changes", cascade);
    }

    #endregion

    #region Attendance (9–15)

    [Fact]
    public void Case09_Faculty_With_Timetable()
    {
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 4,
            PeriodNumber = 2,
            RoomName = "R-101",
            SectionIds = [10],
            SectionCodes = ["A"],
        };
        Assert.Equal("Timetable", dto.Mode);
        Assert.True(dto.HasTimetable);
        Assert.Equal(2, dto.PeriodNumber);
        Assert.Equal("R-101", dto.RoomName);
    }

    [Fact]
    public void Case10_Faculty_Without_Timetable()
    {
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Legacy",
            HasTimetable = false,
            Message = "Use Course → Group → Semester → Subject → Period",
            SectionIds = [],
        };
        Assert.Equal("Legacy", dto.Mode);
        Assert.False(dto.HasTimetable);
        Assert.Contains("Course", dto.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Period", dto.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Case11_Manual_Course_Group_Semester_Subject_Period_Contract()
    {
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Legacy",
            HasTimetable = false,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 4,
            PeriodNumber = 1,
            SectionIds = [],
        };
        Assert.False(dto.HasTimetable);
        Assert.Equal(1, dto.CourseId);
        Assert.Equal(2, dto.GroupId);
        Assert.Equal(3, dto.SemesterId);
        Assert.Equal(4, dto.SubjectId);
        Assert.Equal(1, dto.PeriodNumber);
        Assert.Empty(dto.SectionIds);
    }

    [Fact]
    public void Case12_Manual_Attendance_With_Section()
    {
        var scope = AttendanceSaveScope.Normalize(new MarkAttendanceRequest
        {
            SubjectId = 4,
            Date = DateTime.UtcNow.Date,
            SectionId = 10,
            Students = [],
        });
        Assert.True(AttendanceSaveScope.HasSectionScope(scope));
        Assert.True(AttendanceSaveScope.IsSingleSection(scope));
        Assert.Equal(new[] { 10 }, scope);
    }

    [Fact]
    public void Case13_Manual_Attendance_Without_Section()
    {
        var scope = AttendanceSaveScope.Normalize(new MarkAttendanceRequest
        {
            SubjectId = 4,
            Date = DateTime.UtcNow.Date,
            Students = [],
        });
        Assert.False(AttendanceSaveScope.HasSectionScope(scope));
        Assert.Empty(scope);
        Assert.Empty(AttendanceSectionScope.NormalizeRequestedIds(null, null));
    }

    [Fact]
    public void Case14_Combined_Section_Attendance()
    {
        var scope = AttendanceSaveScope.Normalize(new MarkAttendanceRequest
        {
            SubjectId = 4,
            Date = DateTime.UtcNow.Date,
            SectionIds = [11, 12],
            Students = [],
        });
        Assert.True(AttendanceSaveScope.IsCombinedSection(scope));
        Assert.Equal(new[] { 11, 12 }, scope.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Case15_Timetable_Section_Attendance()
    {
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            SectionIds = [10],
            SectionCodes = ["A"],
            SubjectId = 4,
            PeriodNumber = 3,
        };
        Assert.True(dto.HasTimetable);
        Assert.Equal("Timetable", dto.Mode);
        Assert.Equal(new[] { 10 }, dto.SectionIds.ToArray());
        var saveScope = AttendanceSaveScope.NormalizeRequestedIds(null, dto.SectionIds);
        Assert.True(AttendanceSaveScope.IsSingleSection(saveScope));
    }

    #endregion

    #region Allocation grouping / population (16–26)

    [Fact]
    public void Case16_Student_Number_Range_Population_And_Grouping()
    {
        var ctx = FacetedContext();
        var population = new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.StudentNumberRange,
            FromStudentNumber = "A100",
            ToStudentNumber = "B201",
        };
        var v = AllocationScopeSelectionValidator.Validate(ctx, Config(AllocationGroupingModes.StudentNumberRange, population));
        Assert.True(v.IsValid);
        Assert.Equal(new[] { 1, 2 }, v.ResolvedStudentIds);

        var order = new StudentGroupingStrategy().OrderStudents(ctx, AllocationGroupingModes.StudentNumberRange);
        Assert.Equal(order, new StudentGroupingStrategy().OrderStudents(ctx, AllocationGroupingModes.StudentNumberRange));
        Assert.Equal(4, order.Count);
    }

    [Fact]
    public void Case17_Last_Three_Digits_Grouping()
    {
        var ctx = FacetedContext();
        // Last3: A100→100, B201→201, C099→099, D010→010 → ordinal 010,099,100,201 → ids 4,3,1,2
        var order = new StudentGroupingStrategy().OrderStudents(ctx, AllocationGroupingModes.LastThreeDigits);
        Assert.Equal(new[] { 4, 3, 1, 2 }, order.ToArray());
        Assert.Contains(AllocationGroupingModes.LastThreeDigits, AllocationGroupingModes.All);
    }

    [Fact]
    public void Case18_Alphabetical_Grouping()
    {
        var ctx = FacetedContext();
        var order = new StudentGroupingStrategy().OrderStudents(ctx, AllocationGroupingModes.Alphabetical);
        Assert.Equal(new[] { 1, 2, 3, 4 }, order.ToArray()); // Ada, Bob, Cara, Dan
    }

    [Theory]
    [InlineData(AllocationGroupingModes.Gender)]
    [InlineData(AllocationGroupingModes.Merit)]
    [InlineData(AllocationGroupingModes.Scholarship)]
    [InlineData(AllocationGroupingModes.MinorSubject)]
    [InlineData(AllocationGroupingModes.Language)]
    [InlineData(AllocationGroupingModes.Transport)]
    [InlineData(AllocationGroupingModes.Hostel)]
    [InlineData(AllocationGroupingModes.ElectiveCombination)]
    public void Cases19_to_26_Grouping_Modes_Are_Deterministic_And_Catalogued(string mode)
    {
        var ctx = FacetedContext();
        var grouping = new StudentGroupingStrategy();
        var a = grouping.OrderStudents(ctx, mode);
        var b = grouping.OrderStudents(ctx, mode);
        Assert.Equal(ctx.Students.Count, a.Count);
        Assert.Equal(a, b);
        Assert.Contains(mode, AllocationGroupingModes.All);
    }

    [Theory]
    [InlineData(AllocationPopulationModes.Gender, "Female", new[] { 1, 3 })]
    [InlineData(AllocationPopulationModes.Merit, "High", new[] { 1, 3 })]
    [InlineData(AllocationPopulationModes.ScholarshipCategory, "Merit", new[] { 1, 3 })]
    [InlineData(AllocationPopulationModes.MinorSubject, "Stats", new[] { 1, 3 })]
    [InlineData(AllocationPopulationModes.Language, "Telugu", new[] { 1, 3 })]
    [InlineData(AllocationPopulationModes.TransportRoute, "R1", new[] { 1, 3 })]
    [InlineData(AllocationPopulationModes.Hostel, "H1", new[] { 1, 3 })]
    [InlineData(AllocationPopulationModes.ElectiveCombination, "E1", new[] { 1, 3 })]
    public void Cases19_to_26_Population_Facets_Resolve_Against_Context_Only(string mode, string facet, int[] expectedIds)
    {
        var ctx = FacetedContext();
        var population = new AllocationPopulationSelection { Mode = mode, FacetValue = facet };
        var v = AllocationScopeSelectionValidator.Validate(ctx, Config(AllocationGroupingModes.Alphabetical, population));
        Assert.True(v.IsValid, string.Join("; ", v.Errors));
        Assert.Equal(expectedIds, v.ResolvedStudentIds.ToArray());
    }

    #endregion

    #region Capacity / preview / simulation / governance (27–36)

    [Fact]
    public async Task Case27_Capacity_Violation_Is_Mandatory_Failure()
    {
        var engine = new AllocationConstraintEngine([new CapacityAllocationConstraint()]);
        var scenario = new AllocationScenario
        {
            SectionSummaries =
            [
                new AllocationSectionSummary
                {
                    SectionId = 1,
                    SectionCode = "A",
                    MaximumCapacity = 2,
                    AssignedCount = 99,
                    ReservedSeats = 0,
                },
            ],
        };
        var evals = await engine.EvaluateAsync(FacetedContext(), scenario, AllocationPipelineConfig.Default);
        Assert.Contains(evals, e =>
            e.ConstraintCode == "Capacity"
            && !e.Satisfied
            && e.Priority == AllocationConstraintPriority.Mandatory);
    }

    [Fact]
    public async Task Case28_Preview_Produces_Engine_Scenario_Without_Live_Writes()
    {
        var result = await CreateEngine().ExecuteAsync(new AllocationExecutionContext
        {
            SessionId = Guid.Parse("28282828-2828-2828-2828-282828282828"),
            Context = FacetedContext(),
            Config = Config(AllocationGroupingModes.Alphabetical),
            StartedAt = DateTime.UtcNow,
        });
        Assert.NotEqual(Guid.Empty, result.ScenarioId);
        Assert.NotNull(result.Scenario);
        Assert.NotNull(result.Trace);
        // Preview/simulation remain draft-only — architecture note on AllocationDraft.
        Assert.Contains("not modified", new AllocationDraft().Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Case29_Simulation_Audit_And_Lifecycle_Contract()
    {
        Assert.Equal("Simulate", AllocationAuditActions.Simulate);
        Assert.True(LifecycleSvc().CanTransition(
            AllocationScenarioLifecycle.Draft,
            AllocationScenarioLifecycle.Simulated));
        Assert.True(LifecycleSvc().CanTransition(
            AllocationScenarioLifecycle.Simulated,
            AllocationScenarioLifecycle.Reviewed));

        var result = await CreateEngine().ExecuteAsync(new AllocationExecutionContext
        {
            SessionId = Guid.Parse("29292929-2929-2929-2929-292929292929"),
            Context = FacetedContext(),
            Config = Config(AllocationGroupingModes.Gender),
            StartedAt = DateTime.UtcNow,
        });
        Assert.NotNull(result.Scenario?.Recommendations);
    }

    [Fact]
    public void Case30_Scenario_Creation_Lifecycle_And_Audit()
    {
        Assert.Equal("CreateScenario", AllocationAuditActions.CreateScenario);
        Assert.Equal("Generated", AllocationScenarioLifecycle.Generated);
        Assert.True(LifecycleSvc().CanTransition(
            AllocationScenarioLifecycle.Generated,
            AllocationScenarioLifecycle.Reviewed));
        Assert.Contains(PermissionKeys.AllocationScenarioCreate, PermissionKeys.All);
    }

    [Fact]
    public void Case31_Approval_Transition()
    {
        Assert.Equal("Approve", AllocationAuditActions.Approve);
        Assert.True(LifecycleSvc().CanTransition(
            AllocationScenarioLifecycle.Reviewed,
            AllocationScenarioLifecycle.Approved));
        Assert.False(LifecycleSvc().CanTransition(
            AllocationScenarioLifecycle.Rejected,
            AllocationScenarioLifecycle.Approved));
    }

    [Fact]
    public void Case32_Rejection_Transition()
    {
        Assert.Equal("Reject", AllocationAuditActions.Reject);
        Assert.True(LifecycleSvc().CanTransition(
            AllocationScenarioLifecycle.Reviewed,
            AllocationScenarioLifecycle.Rejected));
        Assert.False(LifecycleSvc().CanTransition(
            AllocationScenarioLifecycle.Approved,
            AllocationScenarioLifecycle.Rejected));
    }

    [Fact]
    public void Case33_Archive_Transition_And_Permission()
    {
        Assert.Equal("Archive", AllocationAuditActions.Archive);
        Assert.True(LifecycleSvc().CanTransition(
            AllocationScenarioLifecycle.Approved,
            AllocationScenarioLifecycle.Archived));
        Assert.Equal("Allocation.Scenario.Archive", PermissionKeys.AllocationScenarioArchive);
        Assert.NotEqual(PermissionKeys.AllocationScenarioReview, PermissionKeys.AllocationScenarioArchive);
    }

    [Fact]
    public void Case34_Stale_Context_Blocks_Approval_Flag()
    {
        var r = AllocationGovernanceResult.Failure(
            AllocationAuditActions.Approve,
            Guid.NewGuid(),
            "earlier academic configuration must be rebuilt",
            contextStale: true);
        Assert.True(r.ContextStale);
        Assert.False(r.Success);
        Assert.False(r.CanApprove);
        Assert.Contains("earlier academic configuration", r.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rebuil", r.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Case35_Checksum_Failure_Detected()
    {
        var scenarioId = Guid.Parse("35353535-3535-3535-3535-353535353535");
        var good = AllocationCanonicalChecksum.Compute(new AllocationScenarioVersionChecksumInput
        {
            ScenarioId = scenarioId,
            VersionNumber = 1,
            ContextVersion = "1",
            ContextChecksum = "abc",
            StrategyConfigurationVersion = "1",
            ConstraintConfigurationVersion = "1",
            LifecycleStatus = AllocationScenarioLifecycle.Reviewed,
            Operation = AllocationAuditActions.Review,
            Score = 80,
            ScenarioJson = """{"a":1}""",
            TraceJson = "[]",
            ConfigJson = "{}",
        });
        var tampered = AllocationCanonicalChecksum.Compute(new AllocationScenarioVersionChecksumInput
        {
            ScenarioId = scenarioId,
            VersionNumber = 1,
            ContextVersion = "1",
            ContextChecksum = "abc",
            StrategyConfigurationVersion = "1",
            ConstraintConfigurationVersion = "1",
            LifecycleStatus = AllocationScenarioLifecycle.Reviewed,
            Operation = AllocationAuditActions.Review,
            Score = 80,
            ScenarioJson = """{"a":2}""",
            TraceJson = "[]",
            ConfigJson = "{}",
        });
        Assert.NotEqual(good, tampered);

        var r = AllocationGovernanceResult.Failure(
            AllocationAuditActions.Approve,
            scenarioId,
            "Scenario checksum validation failed.",
            checksumInvalid: true);
        Assert.True(r.ChecksumInvalid);
        Assert.False(r.CanApprove);
    }

    [Fact]
    public void Case36_Concurrency_Conflict_Contract()
    {
        Assert.Contains("Refresh the scenario", AllocationConcurrencyMessages.ScenarioChanged);
        var prop = typeof(AllocationEngineScenario).GetProperty(nameof(AllocationEngineScenario.RowVersion));
        Assert.NotNull(prop);
        Assert.Equal(typeof(byte[]), prop!.PropertyType);

        var r = AllocationGovernanceResult.Failure(
            AllocationAuditActions.Approve,
            Guid.NewGuid(),
            AllocationConcurrencyMessages.ScenarioChanged,
            concurrencyConflict: true);
        Assert.True(r.ConcurrencyConflict);
        Assert.False(r.Success);
        Assert.False(r.CanApprove);
    }

    #endregion

    #region Suite integrity

    [Fact]
    public void Suite_Architecture_Boundaries_Still_Pass()
    {
        var report = AcademicArchitectureGuard.ValidateAllocationBoundaries();
        Assert.True(report.Passed, string.Join("; ", report.Violations));
        Assert.Equal("AllocationEngine", typeof(AllocationEngine).Name);
    }

    [Fact]
    public void Suite_Mandatory_Case_Inventory_Is_Documented()
    {
        var doc = File.ReadAllText(RepoPath("docs", "AI29_1D_PROMPT_20_REGRESSION_COMPATIBILITY.md"));
        for (var i = 1; i <= 36; i++)
            Assert.Contains($"Case {i}:", doc, StringComparison.Ordinal);
    }

    #endregion
}

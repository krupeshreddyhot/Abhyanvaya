using System.Reflection;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Domain.Entities.Academic;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class AI29_1B_7_AllocationPlatformTests
{
    [Fact]
    public void Section_allocation_context_is_init_only_immutable()
    {
        var mutable = typeof(SectionAllocationContext).GetProperties()
            .Where(p => p.SetMethod is { IsPublic: true })
            .Where(p => !p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit"))
            .Select(p => p.Name)
            .ToList();
        Assert.Empty(mutable);

        var ctx = new SectionAllocationContext
        {
            ContextId = Guid.NewGuid(),
            ContextVersion = "1",
            SchemaVersion = SectionAllocationContext.CurrentSchemaVersion,
            GeneratedAt = DateTime.UtcNow,
            Checksum = "ABC",
            OverallHealth = "Healthy",
            OverallReadiness = "Ready",
        };
        Assert.Equal("1.0.0", ctx.SchemaVersion);
        Assert.Equal("ABC", ctx.Checksum);
    }

    [Fact]
    public void Analysis_context_wraps_execution_context()
    {
        var analysis = new SectionAllocationAnalysisContext
        {
            Context = new SectionAllocationContext { ContextId = Guid.NewGuid(), Checksum = "x" },
            Forecast = ["deferred"],
            Recommendations = ["none"],
        };
        Assert.NotEqual(Guid.Empty, analysis.Context.ContextId);
        Assert.Contains("deferred", analysis.Forecast);
    }

    [Fact]
    public void Read_models_have_no_business_methods()
    {
        foreach (var type in new[]
                 {
                     typeof(AllocationStudentProjection),
                     typeof(AllocationSectionProjection),
                     typeof(AllocationCapacityProjection),
                     typeof(AllocationFacultyProjection),
                 })
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToList();
            Assert.True(methods.Count == 0, $"{type.Name} must not declare business methods.");
        }
    }

    [Fact]
    public void Snapshot_entity_stores_context_json()
    {
        var snap = new SectionAllocationSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            Checksum = "deadbeef",
            GeneratedDate = DateTime.UtcNow,
            ContextJson = "{\"contextId\":\"...\"}",
            AcademicYearId = 1,
            CourseId = 2,
            GroupId = 3,
            SemesterId = 4,
        };
        Assert.False(string.IsNullOrWhiteSpace(snap.ContextJson));
        Assert.Equal("1.0.0", snap.SchemaVersion);
    }

    [Fact]
    public void Constraint_registry_lists_enterprise_constraints_without_allocating()
    {
        Assert.Contains(AllocationConstraintRegistry.All, c => c.Code == "Capacity");
        Assert.Contains(AllocationConstraintRegistry.All, c => c.Code == "GenderBalance");
        Assert.Contains(AllocationConstraintRegistry.All, c => c.Code == "ElectiveCombination");
        Assert.Equal(9, AllocationConstraintRegistry.All.Count);
    }

    [Fact]
    public async Task NoOp_strategy_contracts_are_registered_shape()
    {
        var ctx = new SectionAllocationContext { ContextId = Guid.NewGuid(), Checksum = "n" };
        var strategy = new NoOpAllocationStrategy();
        var constraint = new NoOpAllocationConstraint();
        var scoring = new NoOpAllocationScoringProvider();
        var rec = new NoOpAllocationRecommendationProvider();

        Assert.True((await strategy.EvaluateAsync(ctx)).IsNoOp);
        Assert.True((await constraint.EvaluateAsync(ctx)).Satisfied);
        Assert.Equal(0, (await scoring.ScoreAsync(ctx)).Score);
        Assert.NotEmpty(await rec.RecommendAsync(ctx));
    }

    [Fact]
    public void Null_allocation_engine_has_no_operational_dependencies()
    {
        var ctorParams = typeof(NullAllocationEngine).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();
        Assert.DoesNotContain(typeof(ISectionCapacityEngine), ctorParams);
        Assert.DoesNotContain(typeof(ISectionAllocationContextBuilder), ctorParams);
        Assert.Equal("Null", new NullAllocationEngine().EngineCode);
    }

    [Fact]
    public void Validator_blocks_missing_sections_and_checksum()
    {
        var validator = new SectionAllocationContextValidator(new NoOpAcademicTelemetry());
        var report = validator.ValidateAsync(new SectionAllocationContext
        {
            Hierarchy = new AllocationHierarchyProjection { AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 },
            Sections = [],
            Checksum = "",
        }).GetAwaiter().GetResult();

        Assert.False(report.IsValid);
        Assert.Contains(report.Errors, e => e.Contains("Missing sections", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Errors, e => e.Contains("checksum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_passes_minimal_valid_context()
    {
        var validator = new SectionAllocationContextValidator(new NoOpAcademicTelemetry());
        var report = validator.ValidateAsync(new SectionAllocationContext
        {
            Hierarchy = new AllocationHierarchyProjection { AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 },
            Sections = [new AllocationSectionProjection { SectionId = 1, SectionCode = "A", Lifecycle = "Active" }],
            Capacities = [new AllocationCapacityProjection { SectionId = 1, MaximumCapacity = 60, CurrentStrength = 40 }],
            Policies = ["A: AllowMerge=True"],
            FacultyAssignments = [new AllocationFacultyProjection { FacultyId = 1, SectionId = 1, Role = "Mentor" }],
            SubjectAssignments = [new AllocationSubjectProjection { SubjectId = 1 }],
            Students = [new AllocationStudentProjection { StudentId = 1 }],
            TimetableStatus = "Mapped",
            Checksum = "OK",
        }).GetAwaiter().GetResult();

        Assert.True(report.IsValid, string.Join("; ", report.Errors));
    }

    [Fact]
    public void Readiness_and_health_report_shapes()
    {
        var ready = new AllocationReadinessReport
        {
            OverallStatus = "Warning",
            Checks = [new AllocationReadinessCheck { Area = "Faculty", Status = "Warning", Message = "None" }],
        };
        var health = new AllocationHealthReport
        {
            OverallStatus = "Critical",
            Dimensions = [new AllocationHealthDimension { Area = "Capacity", Status = "Critical", Message = "Over" }],
        };
        Assert.Equal("Warning", ready.OverallStatus);
        Assert.Equal("Critical", health.OverallStatus);
    }

    [Fact]
    public void Composition_report_captures_steps()
    {
        var report = new AllocationContextCompositionReport
        {
            ContextId = Guid.NewGuid(),
            GeneratedAt = DateTime.UtcNow,
            TotalDurationMs = 12.5,
            Steps =
            [
                new AllocationCompositionStep { Service = "Hierarchy", DurationMs = 2, Outcome = "Ok" },
                new AllocationCompositionStep { Service = "Capacity", DurationMs = 4, Outcome = "Ok" },
            ],
            Warnings = ["No sections found for allocation scope."],
        };
        Assert.Equal(2, report.Steps.Count);
        Assert.NotEmpty(report.Warnings);
    }

    [Fact]
    public void Cache_key_namespace_is_allocation_specific()
    {
        // Contract: AllocationContextCache uses allocation-context:{tenant}:...
        var src = typeof(AllocationContextCache).GetMethod("Key", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(src);
        // Ensure class exists and does not share hierarchy cache type name
        Assert.DoesNotContain("Hierarchy", typeof(AllocationContextCache).Name);
        Assert.DoesNotContain("Statistics", typeof(AllocationContextCache).Name);
    }

    [Fact]
    public void Observability_operations_include_allocation_platform_metrics()
    {
        Assert.Equal("allocation.context.build", AcademicOperations.AllocationContextBuild);
        Assert.Equal("allocation.context.refresh", AcademicOperations.AllocationContextRefresh);
        Assert.Equal("allocation.snapshot.generate", AcademicOperations.AllocationSnapshot);
        Assert.Equal("allocation.validation", AcademicOperations.AllocationValidation);
        Assert.Equal("allocation.readiness", AcademicOperations.AllocationReadiness);
        Assert.Equal("allocation.health", AcademicOperations.AllocationHealth);
        Assert.Equal("allocation.cache.hit", AcademicOperations.AllocationCacheHit);
        Assert.Equal("allocation.cache.miss", AcademicOperations.AllocationCacheMiss);
    }

    [Fact]
    public void Allocation_architecture_guard_passes()
    {
        var report = AcademicArchitectureGuard.ValidateAllocationBoundaries();
        Assert.True(report.Passed, string.Join("; ", report.Violations));
        Assert.Contains(report.Checks, c => c.Contains("SectionAllocationContext", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Builder_interface_exposes_required_operations()
    {
        var names = typeof(ISectionAllocationContextBuilder).GetMethods().Select(m => m.Name).ToHashSet();
        Assert.Contains("BuildAsync", names);
        Assert.Contains("RefreshAsync", names);
        Assert.Contains("SnapshotAsync", names);
        Assert.Contains("ValidateAsync", names);
        Assert.Contains("BuildAnalysisContextAsync", names);
    }

    [Fact]
    public void Scope_request_requires_hierarchy_ids()
    {
        var scope = new AllocationScopeRequest
        {
            AcademicYearId = 1,
            CourseId = 2,
            GroupId = 3,
            SemesterId = 4,
        };
        Assert.Equal(1, scope.AcademicYearId);
        Assert.Equal(4, scope.SemesterId);
    }

    private sealed class NoOpAcademicTelemetry : IAcademicTelemetryService
    {
        public Task<T> TrackAsync<T>(
            string operationName,
            string spanName,
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public Task TrackAsync(
            string operationName,
            string spanName,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
            => action(cancellationToken);

        public void RecordCacheHit(string cacheKind) { }
        public void RecordCacheMiss(string cacheKind) { }
        public void RecordDuration(string metricName, TimeSpan duration) { }
    }
}

using Abhyanvaya.Application.Academic.Allocation;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24B.2 Prompt 2 — server-authoritative targetSectionIds vs Allocation Context.</summary>
public sealed class AI29_1D_24B_2_TargetSectionScopeAuthorityTests
{
    private static SectionAllocationContext ContextForScope(
        int academicYearId,
        int courseId,
        int groupId,
        int semesterId,
        params (int Id, string Code)[] sections)
    {
        return new SectionAllocationContext
        {
            ContextId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            Checksum = "24B2SCOPE",
            Hierarchy = new AllocationHierarchyProjection
            {
                AcademicYearId = academicYearId,
                CourseId = courseId,
                GroupId = groupId,
                SemesterId = semesterId,
            },
            Sections = sections
                .Select(s => new AllocationSectionProjection
                {
                    SectionId = s.Id,
                    SectionCode = s.Code,
                    SectionName = s.Code,
                    Lifecycle = "Active",
                })
                .ToArray(),
            Capacities = sections
                .Select(s => new AllocationCapacityProjection
                {
                    SectionId = s.Id,
                    MaximumCapacity = 40,
                    AvailableCapacity = 40,
                })
                .ToArray(),
            Students =
            [
                new AllocationStudentProjection
                {
                    StudentId = 1,
                    StudentNumber = "1",
                    StudentName = "A",
                    Gender = "Female",
                },
            ],
        };
    }

    private static AllocationPipelineConfig WithTargets(params int[] ids) =>
        new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            TargetSectionIds = ids,
        }.Normalize();

    [Fact]
    public void Correct_course_group_semester_targets_accepted()
    {
        var ctx = ContextForScope(2026, 10, 2, 3, (11, "CA-A"), (12, "CA-B"));
        var v = AllocationScopeSelectionValidator.Validate(ctx, WithTargets(11, 12));
        Assert.True(v.IsValid);
        Assert.Equal(new[] { 11, 12 }, v.ResolvedSectionIds);
    }

    [Fact]
    public void Wrong_group_section_id_rejected()
    {
        // Context is Computer Applications (group 2); Finance section 99 is outside context.
        var ctx = ContextForScope(2026, 10, 2, 3, (11, "CA-A"), (12, "CA-B"));
        var v = AllocationScopeSelectionValidator.Validate(ctx, WithTargets(11, 99));
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("99") && e.Contains("not present"));
    }

    [Fact]
    public void Wrong_course_section_id_rejected()
    {
        var ctx = ContextForScope(2026, 10, 2, 3, (11, "CA-A"));
        var v = AllocationScopeSelectionValidator.Validate(ctx, WithTargets(77));
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("77"));
    }

    [Fact]
    public void Wrong_semester_section_id_rejected()
    {
        var ctx = ContextForScope(2026, 10, 2, 3, (11, "CA-A"));
        var v = AllocationScopeSelectionValidator.Validate(ctx, WithTargets(55));
        Assert.False(v.IsValid);
        Assert.DoesNotContain(55, v.ResolvedSectionIds);
    }

    [Fact]
    public void Wrong_academic_year_section_id_rejected()
    {
        var ctx = ContextForScope(2026, 10, 2, 3, (11, "CA-A"));
        var v = AllocationScopeSelectionValidator.Validate(ctx, WithTargets(88));
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("88"));
    }

    [Fact]
    public void Foreign_tenant_section_cannot_appear_in_context_so_id_is_rejected()
    {
        // Context is tenant-scoped at build time; a forged foreign section id is still rejected here.
        var ctx = ContextForScope(2026, 10, 2, 3, (11, "CA-A"));
        var v = AllocationScopeSelectionValidator.Validate(ctx, WithTargets(999001));
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("999001"));
    }

    [Fact]
    public void Invalid_section_id_rejected_fail_closed()
    {
        var ctx = ContextForScope(2026, 10, 2, 3, (11, "CA-A"));
        Assert.Throws<ArgumentException>(() =>
            AllocationScopeSelectionValidator.ValidateOrThrow(ctx, WithTargets(11, -1)));
    }

    [Fact]
    public void All_eligible_uses_only_context_sections()
    {
        var ctx = ContextForScope(2026, 10, 2, 3, (11, "CA-A"), (12, "CA-B"));
        var v = AllocationScopeSelectionValidator.Validate(ctx, AllocationPipelineConfig.Default);
        Assert.True(v.IsValid);
        Assert.Equal(new[] { 11, 12 }, v.ResolvedSectionIds);
        Assert.Equal("All eligible sections", v.TargetSectionsSummary);
    }

    [Fact]
    public void Occupancy_controller_exposes_additive_sectionIds_filter()
    {
        var controller = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Abhyanvaya.API", "Controllers", "SectionCapacityController.cs"));
        Assert.Contains("int[]? sectionIds", controller);
        Assert.Contains("GetOccupancyAsync(sectionIds, academicYearId, semesterId", controller);
    }

    [Fact]
    public void Context_builder_still_scopes_by_group()
    {
        var builder = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Abhyanvaya.Application", "Academic", "Allocation", "SectionAllocationContextBuilder.cs"));
        Assert.Contains("s.GroupId == scope.GroupId", builder);
        Assert.Contains("s.CourseId == scope.CourseId", builder);
    }
}

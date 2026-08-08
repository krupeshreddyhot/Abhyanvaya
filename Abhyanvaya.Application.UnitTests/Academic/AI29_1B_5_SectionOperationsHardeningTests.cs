using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Architecture;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

public sealed class AI29_1B_5_SectionOperationsHardeningTests
{
    [Fact]
    public void Version_operations_are_immutable_string_constants()
    {
        Assert.Contains(SectionVersionOperations.Create, SectionVersionOperations.All);
        Assert.Contains(SectionVersionOperations.CapacityChange, SectionVersionOperations.All);
        Assert.Equal(6, SectionVersionOperations.All.Count);
    }

    [Fact]
    public void Policy_scope_specificity_orders_hierarchy()
    {
        Assert.True(SectionPolicyScopeLevels.Specificity(SectionPolicyScopeLevels.SectionType)
                    > SectionPolicyScopeLevels.Specificity(SectionPolicyScopeLevels.Course));
        Assert.True(SectionPolicyScopeLevels.Specificity(SectionPolicyScopeLevels.Course)
                    > SectionPolicyScopeLevels.Specificity(SectionPolicyScopeLevels.Program));
        Assert.True(SectionPolicyScopeLevels.Specificity(SectionPolicyScopeLevels.Program)
                    > SectionPolicyScopeLevels.Specificity(SectionPolicyScopeLevels.Tenant));
    }

    [Fact]
    public void Capacity_history_dto_is_append_only_shape()
    {
        var dto = new SectionCapacityHistoryDto
        {
            SectionId = 1,
            MaximumCapacity = 60,
            CurrentStrength = 40,
            OccupancyPercent = 66.67,
            RecordedDate = DateTime.UtcNow,
            Reason = "CapacityChange",
        };
        Assert.Equal(60, dto.MaximumCapacity);
        Assert.Equal("CapacityChange", dto.Reason);
    }

    [Fact]
    public void Timeline_event_supports_audit_projection_fields()
    {
        var ev = new SectionTimelineEventDto
        {
            Timestamp = DateTime.UtcNow,
            Operation = SectionVersionOperations.LifecycleChange,
            EventKind = "Version",
            FromStatus = SectionLifecycleStates.Open,
            ToStatus = SectionLifecycleStates.Active,
            VersionNumber = 2,
        };
        Assert.Equal("Version", ev.EventKind);
        Assert.Equal(2, ev.VersionNumber);
    }

    [Fact]
    public void Merge_preview_engine_dto_is_read_model()
    {
        var dto = new MergePreviewEngineDto
        {
            IsValid = true,
            CombinedStudentCount = 80,
            MergedCapacity = 60,
            Warnings = ["Combined strength exceeds target capacity."],
        };
        Assert.True(dto.IsValid);
        Assert.NotEmpty(dto.Warnings);
    }

    [Fact]
    public void Split_preview_shows_expected_distribution()
    {
        var dto = new SplitPreviewEngineDto
        {
            IsValid = true,
            SourceSectionId = 1,
            ProposedChildren =
            [
                new SplitPreviewChildDto { ProposedCode = "A-A", ExpectedStudentCount = 20, ProposedCapacity = 30 },
                new SplitPreviewChildDto { ProposedCode = "A-B", ExpectedStudentCount = 20, ProposedCapacity = 30 },
            ]
        };
        Assert.Equal(2, dto.ProposedChildren.Count);
        Assert.Equal(40, dto.ProposedChildren.Sum(c => c.ExpectedStudentCount));
    }

    [Fact]
    public void Capacity_recommendations_are_advisory_literals()
    {
        var rec = new SectionCapacityRecommendationDto
        {
            Recommendation = "MergeCandidate",
            Rationale = "Low occupancy",
            OccupancyPercent = 20,
        };
        Assert.Equal("MergeCandidate", rec.Recommendation);
    }

    [Fact]
    public void Section_health_statuses_are_healthy_warning_critical()
    {
        var report = new SectionHealthReportDto
        {
            OverallStatus = "Critical",
            Dimensions =
            [
                new SectionHealthDimensionDto { Area = "Faculty", Status = "Critical", Message = "None" },
                new SectionHealthDimensionDto { Area = "Capacity", Status = "Healthy", Message = "Ok" },
            ]
        };
        Assert.Equal("Critical", report.OverallStatus);
    }

    [Fact]
    public void Section_architecture_guard_passes_current_boundaries()
    {
        var report = AcademicArchitectureGuard.ValidateSectionBoundaries();
        Assert.True(report.Passed, string.Join("; ", report.Violations));
        Assert.Contains(report.Checks, c => c.Contains("Section must not own Attendance", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Checks, c => c.Contains("Merge preview", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Section_version_entity_has_previous_link_for_lineage()
    {
        var v = new SectionVersion
        {
            SectionId = 1,
            VersionNumber = 2,
            PreviousVersionId = 10,
            Operation = SectionVersionOperations.Update,
            VersionDate = DateTime.UtcNow,
        };
        Assert.Equal(10, v.PreviousVersionId);
        Assert.Equal(SectionVersionOperations.Update, v.Operation);
    }

    [Fact]
    public void Resolved_policy_defaults_allow_merge_and_split()
    {
        var dto = new ResolvedSectionPolicyDto { SectionId = 1 };
        Assert.True(dto.AllowMerge);
        Assert.True(dto.AllowSplit);
    }
}

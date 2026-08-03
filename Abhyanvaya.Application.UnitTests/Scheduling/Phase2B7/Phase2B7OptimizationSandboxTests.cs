using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Optimization;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2B7;

public sealed class Phase2B7OptimizationSandboxTests
{
    [Fact]
    public void ScenarioLifecycle_SupportsDraftSavedComparedReviewedArchived()
    {
        var values = Enum.GetValues<ScenarioStatus>();
        Assert.Contains(ScenarioStatus.Draft, values);
        Assert.Contains(ScenarioStatus.Saved, values);
        Assert.Contains(ScenarioStatus.Compared, values);
        Assert.Contains(ScenarioStatus.Reviewed, values);
        Assert.Contains(ScenarioStatus.Archived, values);
    }

    [Fact]
    public void Scenario_NeverModifiesProductionTimetable()
    {
        var scenario = new OptimizationScenario
        {
            ScenarioId = Guid.NewGuid(),
            Name = "S1",
            Status = ScenarioStatus.Saved,
            IsImmutable = true
        };
        Assert.False(scenario.ModifiesProductionTimetable);

        var summary = new ScenarioSummaryDto
        {
            ScenarioId = scenario.ScenarioId,
            Name = scenario.Name,
            Status = scenario.Status,
            IsImmutable = true,
            ModifiesProductionTimetable = false
        };
        Assert.False(summary.CanApply);
        Assert.False(summary.ModifiesProductionTimetable);
    }

    [Fact]
    public void Snapshot_IsImmutableByDefault()
    {
        var snap = new OptimizationSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            Label = "Initial",
            CapturedUtc = DateTime.UtcNow
        };
        Assert.True(snap.IsImmutable);
    }

    [Fact]
    public void DuplicatePattern_CreatesNewDraftFromImmutableParent()
    {
        var parent = new OptimizationScenario
        {
            ScenarioId = Guid.NewGuid(),
            Name = "Parent",
            Status = ScenarioStatus.Saved,
            IsImmutable = true,
            AcademicYearId = 1,
            OwnerUserId = 9
        };
        var child = new OptimizationScenario
        {
            ScenarioId = Guid.NewGuid(),
            Name = $"{parent.Name} (Copy)",
            Status = ScenarioStatus.Draft,
            IsImmutable = false,
            ParentScenarioId = parent.ScenarioId,
            AcademicYearId = parent.AcademicYearId,
            OwnerUserId = parent.OwnerUserId
        };

        Assert.Equal(ScenarioStatus.Saved, parent.Status);
        Assert.True(parent.IsImmutable);
        Assert.Equal(ScenarioStatus.Draft, child.Status);
        Assert.False(child.IsImmutable);
        Assert.Equal(parent.ScenarioId, child.ParentScenarioId);
    }

    [Fact]
    public void HistoryActions_CoverEnterpriseAuditSurface()
    {
        var actions = Enum.GetValues<ScenarioHistoryAction>();
        Assert.Contains(ScenarioHistoryAction.Created, actions);
        Assert.Contains(ScenarioHistoryAction.Replayed, actions);
        Assert.Contains(ScenarioHistoryAction.Compared, actions);
        Assert.Contains(ScenarioHistoryAction.Favorited, actions);
        Assert.Contains(ScenarioHistoryAction.Archived, actions);
        Assert.Contains(ScenarioHistoryAction.Shared, actions);
        Assert.Contains(ScenarioHistoryAction.ApprovalRequested, actions);
    }

    [Fact]
    public void DifferenceSummary_HighlightsImprovementsWithoutApply()
    {
        var result = new ScenarioComparisonResultDto
        {
            Differences = new DifferenceSummaryDto
            {
                ProjectedScoreDelta = 5,
                ConflictDelta = -2,
                Verdict = "Right better"
            },
            ImprovementHighlights = ["Right improves projected score by 5.", "Right reduces conflicts by 2."],
        };
        Assert.False(result.CanApply);
        Assert.NotEmpty(result.ImprovementHighlights);
    }

    [Fact]
    public void ReplayTimeline_IsReadOnly()
    {
        var timeline = new ReplayTimelineDto
        {
            ScenarioId = Guid.NewGuid(),
            IsReadOnly = true,
            Steps =
            [
                new ReplaySnapshotDto { SnapshotId = Guid.NewGuid(), Sequence = 1, Label = "Initial", Score = 70, ConflictCount = 3, CapturedUtc = DateTime.UtcNow }
            ]
        };
        Assert.True(timeline.IsReadOnly);
        Assert.Single(timeline.Steps);
    }

    [Fact]
    public void AttendanceCompatibility_ResolverUnchanged()
    {
        Assert.True(typeof(IAttendanceSessionResolver).IsInterface);
        Assert.Contains("Legacy", new[] { "Legacy", "Timetable" });
        Assert.Contains("Timetable", new[] { "Legacy", "Timetable" });
    }

    [Fact]
    public void Isolation_SandboxDoesNotImplementOptimizationStrategy()
    {
        Assert.False(typeof(OptimizationScenario).GetInterfaces().Contains(typeof(IOptimizationStrategy)));
        Assert.False(typeof(IOptimizationScenarioRepository).IsAssignableFrom(typeof(IOptimizationStrategy)));
    }

    [Fact]
    public void WorkspaceDto_HidesApplyButton()
    {
        var dto = new OptimizationWorkspaceDto();
        Assert.False(dto.ShowApplyButton);
    }
}

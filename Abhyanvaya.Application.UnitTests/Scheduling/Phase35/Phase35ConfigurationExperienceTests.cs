using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Configuration;
using Abhyanvaya.Application.Scheduling.Conflicts;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase35;

/// <summary>AI30 Phase 3.5 — readiness catalog, next-step order, validator flags, resolver guard.</summary>
public class Phase35ConfigurationExperienceTests
{
    [Fact]
    public void ModuleCatalog_Has_Required_Minimum_Modules()
    {
        var keys = SchedulingModuleCatalog.Modules.Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[]
                 {
                     "academic-years", "working-days", "campuses", "rooms", "time-slots",
                     "subject-allocations", "schedule-versions", "timetable-designer"
                 })
        {
            Assert.Contains(required, keys);
        }
    }

    [Fact]
    public void ModuleCatalog_Paths_Unchanged_Contract()
    {
        Assert.Equal("/setup/scheduling/timetables",
            SchedulingModuleCatalog.Modules.First(m => m.Key == "timetable-designer").Path);
        Assert.Equal("/setup/scheduling/subject-allocations",
            SchedulingModuleCatalog.Modules.First(m => m.Key == "subject-allocations").Path);
        Assert.Equal("/setup/scheduling/governance/versions",
            SchedulingModuleCatalog.Modules.First(m => m.Key == "schedule-versions").Path);
    }

    [Fact]
    public void MinimumPathOrder_Starts_With_AcademicYear()
    {
        Assert.Equal("academic-years", SchedulingModuleCatalog.MinimumPathOrder[0]);
        Assert.Equal("working-days", SchedulingModuleCatalog.MinimumPathOrder[1]);
        Assert.Contains("timetable-designer", SchedulingModuleCatalog.MinimumPathOrder);
    }

    [Fact]
    public void SubjectAllocation_Requires_TimeSlots()
    {
        var alloc = SchedulingModuleCatalog.Modules.First(m => m.Key == "subject-allocations");
        Assert.Contains("time-slots", alloc.Requires);
        Assert.Contains("timetable-designer", alloc.UsedBy);
        Assert.Contains("conflict-dashboard", alloc.UsedBy);
    }

    [Fact]
    public void SetupValidationDto_NeverBlocks()
    {
        var dto = new SchedulingSetupValidationDto();
        Assert.True(dto.NeverBlocks);
        Assert.True(dto.SkipsConflictDetection);
    }

    [Fact]
    public void ReadinessSummary_Safety_Flags()
    {
        var dto = new SchedulingReadinessSummaryDto();
        Assert.True(dto.DoesNotModifyTimetableGeneration);
        Assert.True(dto.DoesNotModifyAttendanceApis);
    }

    [Fact]
    public void AttendanceSessionResolver_Unchanged()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Equal("Abhyanvaya.Application.Scheduling.Conflicts", type.Namespace);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
    }

    [Fact]
    public void DependencyEdges_From_Requires()
    {
        var edges = SchedulingModuleCatalog.Modules
            .SelectMany(m => m.Requires.Select(r => (r, m.Key)))
            .ToList();
        Assert.Contains(edges, e => e.r == "campuses" && e.Key == "rooms");
        Assert.Contains(edges, e => e.r == "time-slots" && e.Key == "subject-allocations");
    }
}

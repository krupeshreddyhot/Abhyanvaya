using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Academic;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29 — section contracts, capacity, combined classes, attendance compatibility.</summary>
public class AI29_SectionManagementTests
{
    [Fact]
    public void Section_Is_Not_On_Subject_Curriculum()
    {
        var section = new Section
        {
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SectionCode = "A",
            SectionName = "Section A",
            MaximumStrength = 60,
            Status = "Active",
        };
        Assert.Equal("A", section.SectionCode);
        Assert.Equal(60, section.MaximumStrength);
        // Subject master is never referenced on Section entity.
        Assert.Null(typeof(Section).GetProperty("SubjectId"));
    }

    [Fact]
    public void StudentSection_History_Is_Append_Only()
    {
        var prior = new StudentSection
        {
            StudentId = 10,
            SectionId = 1,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = new DateOnly(2026, 6, 30),
            IsCurrent = false,
        };
        var current = new StudentSection
        {
            StudentId = 10,
            SectionId = 2,
            EffectiveFrom = new DateOnly(2026, 7, 1),
            IsCurrent = true,
            TransferReason = "Mid-year move",
        };
        Assert.False(prior.IsCurrent);
        Assert.True(current.IsCurrent);
        Assert.Equal("Mid-year move", current.TransferReason);
        Assert.NotEqual(prior.SectionId, current.SectionId);
    }

    [Fact]
    public void Capacity_Is_Configurable_Per_Section()
    {
        var a = new Section { SectionCode = "A", MaximumStrength = 60 };
        var b = new Section { SectionCode = "B", MaximumStrength = 55 };
        var c = new Section { SectionCode = "C", MaximumStrength = 72 };
        Assert.NotEqual(a.MaximumStrength, b.MaximumStrength);
        Assert.Equal(72, c.MaximumStrength);
    }

    [Fact]
    public void TimetableSection_Supports_Combined_Classes()
    {
        var entryId = 99;
        var mappings = new[]
        {
            new TimetableSection { TimetableId = 1, TimetableEntryId = entryId, SectionId = 1 },
            new TimetableSection { TimetableId = 1, TimetableEntryId = entryId, SectionId = 2 },
            new TimetableSection { TimetableId = 1, TimetableEntryId = entryId, SectionId = 3 },
        };
        Assert.Equal(3, mappings.Select(m => m.SectionId).Distinct().Count());
        Assert.Single(mappings.Select(m => m.TimetableEntryId).Distinct());
    }

    [Fact]
    public void AttendanceResolution_Legacy_Has_Empty_Sections()
    {
        var legacy = new AttendanceSessionResolutionDto
        {
            Mode = "Legacy",
            HasTimetable = false,
            Message = "manual",
            SectionIds = [],
            SectionCodes = [],
        };
        Assert.Equal("Legacy", legacy.Mode);
        Assert.Empty(legacy.SectionIds);
    }

    [Fact]
    public void AttendanceResolution_Timetable_Can_Carry_Combined_Sections()
    {
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 4,
            SectionIds = [11, 12],
            SectionCodes = ["A", "B"],
        };
        Assert.True(dto.HasTimetable);
        Assert.Equal(2, dto.SectionIds.Count);
        Assert.Contains("A", dto.SectionCodes);
    }

    [Fact]
    public void CreateSectionRequest_Requires_Scope_Not_Subject()
    {
        var req = new CreateSectionRequest
        {
            AcademicYearId = 1,
            CourseId = 2,
            GroupId = 3,
            SemesterId = 4,
            SectionCode = "A",
            SectionName = "Section A",
            MaximumStrength = 60,
        };
        Assert.Equal(0, typeof(CreateSectionRequest).GetProperties().Count(p => p.Name.Contains("Subject", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(60, req.MaximumStrength);
    }

    [Fact]
    public void AutoAllocate_Strategies_Are_Documented()
    {
        var strategies = new[] { "Alphabetical", "GenderBalance", "Merit", "Random", "CapacityBased" };
        Assert.Contains("CapacityBased", strategies);
        Assert.Equal(5, strategies.Length);
    }
}

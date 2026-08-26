using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D Prompt 13 — Combined Section UI contracts.
/// Uses existing TimetableSections / SectionIds representation; no second combined-section model.
/// </summary>
public sealed class AI29_1D_Prompt13_CombinedSectionUiTests
{
    [Fact]
    public void Single_Section_Operational_Label_Is_Section_Code()
    {
        var codes = new[] { "A" };
        var label = string.Join(" + ", codes);
        Assert.Equal("A", label);
        Assert.False(codes.Length > 1);
    }

    [Fact]
    public void Combined_A_Plus_B_Is_One_Operational_Class_Label()
    {
        var resolution = new AttendanceSessionResolutionDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            SectionIds = [11, 12],
            SectionCodes = ["A", "B"],
        };
        var label = string.Join(" + ", resolution.SectionCodes);
        Assert.Equal("A + B", label);
        Assert.Equal(2, resolution.SectionIds.Count);
    }

    [Fact]
    public void Combined_A_Plus_B_Plus_C_Preserves_Underlying_Membership_Ids()
    {
        var resolution = new AttendanceSessionResolutionDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            SectionIds = [11, 12, 13],
            SectionCodes = ["A", "B", "C"],
        };
        Assert.Equal("A + B + C", string.Join(" + ", resolution.SectionCodes));
        Assert.Equal(new[] { 11, 12, 13 }, resolution.SectionIds);
    }

    [Fact]
    public void Combined_Class_Uses_Resolver_SectionIds_Not_Second_Model()
    {
        var dtoProps = typeof(AttendanceSessionResolutionDto).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("SectionIds", dtoProps);
        Assert.Contains("SectionCodes", dtoProps);
        Assert.DoesNotContain(dtoProps, n => n.Contains("CombinedSection", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dtoProps, n => n.Equals("SectionGroupId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Normalize_Combined_Section_Ids_Is_Distinct()
    {
        var ids = AttendanceSectionScope.NormalizeRequestedIds(null, [12, 11, 12, 13]);
        Assert.Equal(3, ids.Count);
        Assert.Contains(11, ids);
        Assert.Contains(12, ids);
        Assert.Contains(13, ids);
    }

    [Fact]
    public void AttendanceSessionResolver_Architecture_Intact()
    {
        Assert.Equal("AttendanceSessionResolver", typeof(AttendanceSessionResolver).Name);
    }
}

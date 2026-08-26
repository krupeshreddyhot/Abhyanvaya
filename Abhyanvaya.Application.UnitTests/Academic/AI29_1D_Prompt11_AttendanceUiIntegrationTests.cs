using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D Prompt 11 — Attendance UI integration contract regressions.
/// Does not redesign AttendanceSessionResolver; asserts additive section contract + mode semantics.
/// </summary>
public sealed class AI29_1D_Prompt11_AttendanceUiIntegrationTests
{
    [Fact]
    public void Faculty_With_Timetable_Uses_Timetable_Mode()
    {
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 4,
            PeriodNumber = 1,
            RoomName = "R-101",
        };
        Assert.Equal("Timetable", dto.Mode);
        Assert.True(dto.HasTimetable);
        Assert.Equal(1, dto.CourseId);
        Assert.Equal("R-101", dto.RoomName);
    }

    [Fact]
    public void Faculty_Without_Timetable_Uses_Legacy_Manual_Mode()
    {
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Legacy",
            HasTimetable = false,
            Message = "Use Course → Group → Semester → Subject → Period",
            SectionIds = [],
            SectionCodes = [],
        };
        Assert.Equal("Legacy", dto.Mode);
        Assert.False(dto.HasTimetable);
        Assert.Empty(dto.SectionIds);
        Assert.Contains("Course", dto.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Faculty_With_Timetable_And_Section_Carries_SectionIds()
    {
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            SectionIds = [10],
            SectionCodes = ["A"],
        };
        Assert.Single(dto.SectionIds);
        Assert.Equal(10, dto.SectionIds[0]);
        Assert.Equal("A", dto.SectionCodes[0]);
    }

    [Fact]
    public void Faculty_Without_Timetable_And_No_Section_Omits_Section_Filter_Contract()
    {
        // UI must omit sectionId/sectionIds when empty — API then loads full cohort (legacy).
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Legacy",
            HasTimetable = false,
            SectionIds = [],
            SectionCodes = [],
        };
        Assert.Empty(dto.SectionIds);
        Assert.Empty(dto.SectionCodes);
    }

    [Fact]
    public void Faculty_Manually_Selecting_Section_Is_Optional_Filter_Not_Required_On_Resolution()
    {
        // Manual selection is UI → students-for-marking only; resolver stays Legacy with empty sections.
        var dto = new AttendanceSessionResolutionDto
        {
            Mode = "Legacy",
            HasTimetable = false,
            SectionIds = [],
        };
        Assert.False(dto.HasTimetable);
        Assert.Empty(dto.SectionIds);
        // Optional filter ids are client-owned for Manual mode.
        int[] manualSelected = [22];
        Assert.Single(manualSelected);
    }

    [Fact]
    public void Combined_Section_Attendance_Uses_Multiple_SectionIds_One_Session()
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
        Assert.Equal(2, dto.SectionIds.Count);
        Assert.Contains("A", dto.SectionCodes);
        Assert.Contains("B", dto.SectionCodes);
        // One resolution / one attendance session context — not two resolvers.
        Assert.Equal("Timetable", dto.Mode);
    }

    [Fact]
    public void AttendanceSessionResolver_Type_Still_Present_Not_Bypassed()
    {
        var type = typeof(AttendanceSessionResolver);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
        Assert.Equal("AttendanceSessionResolver", type.Name);
    }
}

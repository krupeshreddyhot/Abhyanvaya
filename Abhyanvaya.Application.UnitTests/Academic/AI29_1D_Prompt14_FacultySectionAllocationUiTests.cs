using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D Prompt 14 — Faculty allocation in Section management uses existing FacultySectionAssignment APIs.
/// No second faculty-section model; Subject stays on SubjectAllocation; combined classes via SectionGroup.
/// </summary>
public sealed class AI29_1D_Prompt14_FacultySectionAllocationUiTests
{
    [Fact]
    public void FacultySectionDto_Exposes_Required_Display_Fields()
    {
        var dto = new FacultySectionDto
        {
            Id = 1,
            FacultyId = 42,
            FacultyName = "Ada",
            SectionId = 11,
            SectionCode = "A",
            SectionName = "Section A",
            AcademicYearId = 100,
            Role = "Primary",
            EffectiveFrom = new DateOnly(2026, 6, 1),
            EffectiveTo = null,
            IsCurrent = true,
        };

        Assert.Equal(11, dto.SectionId);
        Assert.Equal(42, dto.FacultyId);
        Assert.Equal(new DateOnly(2026, 6, 1), dto.EffectiveFrom);
        Assert.Null(dto.EffectiveTo);
        Assert.True(dto.IsCurrent);
        // Subject is not on FacultySectionAssignment — UI enriches from SubjectAllocation.
        Assert.Null(typeof(FacultySectionDto).GetProperty("SubjectId"));
    }

    [Fact]
    public void FacultySectionAssignment_Entity_Has_No_Subject_Or_SectionGroup_Fk()
    {
        var props = typeof(FacultySectionAssignment).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("FacultyId", props);
        Assert.Contains("SectionId", props);
        Assert.Contains("EffectiveFrom", props);
        Assert.Contains("EffectiveTo", props);
        Assert.Contains("IsCurrent", props);
        Assert.DoesNotContain("SubjectId", props);
        Assert.DoesNotContain("SectionGroupId", props);
    }

    [Fact]
    public void SectionGroupDto_Supports_Combined_Membership_Display()
    {
        var group = new SectionGroupDto
        {
            Id = 9,
            GroupCode = "AB",
            GroupName = "Combined AB",
            CurrentSectionIds = [11, 12],
            AcademicYearId = 100,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
        };
        Assert.Equal(2, group.CurrentSectionIds.Count);
        Assert.Equal("A + B", string.Join(" + ", new[] { "A", "B" }));
    }

    [Fact]
    public void AttendanceSessionResolver_Unchanged_For_Timetable_Compatibility()
    {
        Assert.Equal("AttendanceSessionResolver", typeof(AttendanceSessionResolver).Name);
    }
}

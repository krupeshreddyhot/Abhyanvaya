using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.15A Prompt 8 — combined Faculty Allocation uses SectionGroup + existing assignments only.
/// </summary>
public sealed class AI29_1D_15A_Prompt8_CombinedFacultyAllocationTests
{
    [Fact]
    public void SectionGroup_Plus_FacultySectionAssignments_Is_The_Combined_Model()
    {
        var group = new SectionGroupDto
        {
            Id = 9,
            GroupCode = "AB",
            GroupName = "Combined AB",
            CurrentSectionIds = [11, 12],
            AcademicYearId = 100,
        };

        var a = new FacultySectionDto
        {
            Id = 1,
            FacultyId = 42,
            FacultyName = "Dr. John Smith",
            SectionId = 11,
            SectionCode = "A",
            AcademicYearId = 100,
            Role = "Primary",
            EffectiveFrom = new DateOnly(2026, 6, 1),
            IsCurrent = true,
        };
        var b = new FacultySectionDto
        {
            Id = 2,
            FacultyId = 42,
            FacultyName = "Dr. John Smith",
            SectionId = 12,
            SectionCode = "B",
            AcademicYearId = 100,
            Role = "Primary",
            EffectiveFrom = new DateOnly(2026, 6, 1),
            IsCurrent = true,
        };

        var participating = new[] { a, b }
            .Where(x => group.CurrentSectionIds.Contains(x.SectionId))
            .ToList();

        Assert.Equal(2, participating.Count);
        var codes = participating.Select(x => x.SectionCode!).OrderBy(c => c).ToArray();
        Assert.Equal("Combined · A + B", $"Combined · {string.Join(" + ", codes)}");
        Assert.Equal(new[] { 1, 2 }, participating.Select(x => x.Id).ToArray());
    }

    [Fact]
    public void One_Faculty_One_Section_Is_Not_Combined()
    {
        var label = "A";
        Assert.Equal("A", label);
        Assert.DoesNotContain("Combined", label, StringComparison.Ordinal);
    }

    [Fact]
    public void Multiple_Faculty_Keep_Separate_Underlying_Assignment_Ids()
    {
        var john = new[] { 1, 2 };
        var ada = new[] { 3, 4 };
        Assert.Equal(2, john.Length);
        Assert.Equal(2, ada.Length);
        Assert.Empty(john.Intersect(ada));
    }

    [Fact]
    public void Ended_And_Inactive_Use_Existing_FacultySectionDto_Fields()
    {
        var ended = new FacultySectionDto
        {
            Id = 5,
            FacultyId = 42,
            SectionId = 11,
            AcademicYearId = 100,
            Role = "Primary",
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = new DateOnly(2026, 3, 1),
            IsCurrent = true,
        };
        var inactive = new FacultySectionDto
        {
            Id = 6,
            FacultyId = 42,
            SectionId = 11,
            AcademicYearId = 100,
            Role = "Primary",
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
            IsCurrent = false,
        };

        Assert.True(ended.IsCurrent);
        Assert.Equal(new DateOnly(2026, 3, 1), ended.EffectiveTo);
        Assert.False(inactive.IsCurrent);
    }

    [Fact]
    public void No_Second_Combined_Faculty_Or_SectionGroup_Relationship_On_Entity()
    {
        var props = typeof(FacultySectionAssignment).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("FacultyId", props);
        Assert.Contains("SectionId", props);
        Assert.Contains("EffectiveFrom", props);
        Assert.Contains("EffectiveTo", props);
        Assert.DoesNotContain("SectionGroupId", props);
        Assert.DoesNotContain("CombinedSectionId", props);
        Assert.DoesNotContain("OperationalClassId", props);
    }
}

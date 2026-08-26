using Abhyanvaya.Application.Academic.Allocation;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24B.4 Prompt 2 — Full Student Number vs Last 3 Digits population filter semantics.</summary>
public sealed class AI29_1D_24B4_PopulationFilterSemanticsTests
{
    private static SectionAllocationContext ContextWithRollNumbers()
    {
        var students = new List<AllocationStudentProjection>();
        // 12-digit style numbers ending 000..010 and 046..050
        for (var i = 0; i <= 10; i++)
        {
            students.Add(new AllocationStudentProjection
            {
                StudentId = 1000 + i,
                StudentNumber = $"105325405{i:D3}",
                StudentName = $"S{i:D3}",
            });
        }
        for (var i = 46; i <= 50; i++)
        {
            students.Add(new AllocationStudentProjection
            {
                StudentId = 2000 + i,
                StudentNumber = $"105325405{i:D3}",
                StudentName = $"S{i:D3}",
            });
        }
        students.Add(new AllocationStudentProjection
        {
            StudentId = 9999,
            StudentNumber = "105325405999",
            StudentName = "S999",
        });

        return new SectionAllocationContext
        {
            ContextId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            Checksum = "POP24B4",
            Hierarchy = new AllocationHierarchyProjection { AcademicYearId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 },
            Sections =
            [
                new AllocationSectionProjection { SectionId = 1, SectionCode = "A", SectionName = "A", Lifecycle = "Active" },
            ],
            Capacities = [new AllocationCapacityProjection { SectionId = 1, MaximumCapacity = 100, AvailableCapacity = 100 }],
            Students = students,
        };
    }

    private static AllocationPipelineConfig WithPopulation(AllocationPopulationSelection population) =>
        new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.LastThreeDigits,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            PopulationSelection = population,
        }.Normalize();

    [Fact]
    public void Full_student_number_range_preserves_ordinal_semantics()
    {
        var ctx = ContextWithRollNumbers();
        var config = WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.StudentNumberRange,
            FromStudentNumber = "105325405046",
            ToStudentNumber = "105325405050",
        });
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.True(v.IsValid);
        Assert.Equal(5, v.ResolvedStudentIds.Count);
        Assert.Contains("Student number range", v.PopulationSummary);
    }

    [Fact]
    public void Last3_range_046_050_matches_five_students()
    {
        var ctx = ContextWithRollNumbers();
        var config = WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.LastThreeDigitsRange,
            FromStudentNumber = "046",
            ToStudentNumber = "050",
        });
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.True(v.IsValid);
        Assert.Equal(5, v.ResolvedStudentIds.Count);
        Assert.Equal("Last 3 Digits range 046–050", v.PopulationSummary);
    }

    [Fact]
    public void Last3_range_001_005_normalizes_and_matches()
    {
        var ctx = ContextWithRollNumbers();
        var config = WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.LastThreeDigitsRange,
            FromStudentNumber = "1",
            ToStudentNumber = "5",
        });
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.True(v.IsValid);
        Assert.Equal(5, v.ResolvedStudentIds.Count); // 001..005
        Assert.Equal("Last 3 Digits range 001–005", v.PopulationSummary);
    }

    [Fact]
    public void Last3_range_000_and_999_accepted()
    {
        var ctx = ContextWithRollNumbers();
        var zero = AllocationScopeSelectionValidator.Validate(ctx, WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.LastThreeDigitsRange,
            FromStudentNumber = "000",
            ToStudentNumber = "000",
        }));
        Assert.True(zero.IsValid);
        Assert.Single(zero.ResolvedStudentIds);

        var nine = AllocationScopeSelectionValidator.Validate(ctx, WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.LastThreeDigitsRange,
            FromStudentNumber = "999",
            ToStudentNumber = "999",
        }));
        Assert.True(nine.IsValid);
        Assert.Single(nine.ResolvedStudentIds);
    }

    [Fact]
    public void Last3_range_from_greater_than_to_rejected()
    {
        var ctx = ContextWithRollNumbers();
        var v = AllocationScopeSelectionValidator.Validate(ctx, WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.LastThreeDigitsRange,
            FromStudentNumber = "050",
            ToStudentNumber = "046",
        }));
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("From must be less than or equal to To", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Last3_range_invalid_and_empty_rejected()
    {
        var ctx = ContextWithRollNumbers();
        var empty = AllocationScopeSelectionValidator.Validate(ctx, WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.LastThreeDigitsRange,
            FromStudentNumber = "",
            ToStudentNumber = "050",
        }));
        Assert.False(empty.IsValid);

        var invalid = AllocationScopeSelectionValidator.Validate(ctx, WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.LastThreeDigitsRange,
            FromStudentNumber = "12A",
            ToStudentNumber = "050",
        }));
        Assert.False(invalid.IsValid);

        var over = AllocationScopeSelectionValidator.Validate(ctx, WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.LastThreeDigitsRange,
            FromStudentNumber = "1000",
            ToStudentNumber = "1001",
        }));
        Assert.False(over.IsValid);
    }

    [Fact]
    public void Legacy_short_full_number_range_1_to_5_still_uses_ordinal_not_last3()
    {
        var ctx = ContextWithRollNumbers();
        var config = WithPopulation(new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.StudentNumberRange,
            FromStudentNumber = "1",
            ToStudentNumber = "5",
        });
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.True(v.IsValid);
        // Ordinal full-string: many 12-digit numbers sort between "1" and "5" — legacy behavior preserved.
        Assert.True(v.ResolvedStudentIds.Count > 5);
    }

    [Fact]
    public void Normalize_pads_last3_bounds_to_D3()
    {
        var n = new AllocationPopulationSelection
        {
            Mode = AllocationPopulationModes.LastThreeDigitsRange,
            FromStudentNumber = "46",
            ToStudentNumber = "50",
        }.Normalize();
        Assert.Equal("046", n.FromStudentNumber);
        Assert.Equal("050", n.ToStudentNumber);
    }

    [Fact]
    public void BandIndex_maps_college_style_bands()
    {
        Assert.Equal(0, LastThreeDigitsSemantics.BandIndex(1, 60));
        Assert.Equal(0, LastThreeDigitsSemantics.BandIndex(60, 60));
        Assert.Equal(1, LastThreeDigitsSemantics.BandIndex(61, 60));
        Assert.Equal(3, LastThreeDigitsSemantics.BandIndex(240, 60));
        Assert.Equal(4, LastThreeDigitsSemantics.BandIndex(241, 60));
        Assert.Equal(0, LastThreeDigitsSemantics.BandIndex(0, 60));
    }
}

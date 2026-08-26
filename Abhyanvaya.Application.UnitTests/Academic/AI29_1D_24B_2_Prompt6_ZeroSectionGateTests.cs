using Abhyanvaya.Application.Academic.Allocation;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24B.2 Prompt 6 — zero eligible section UI gate + 10A rejection preserved.</summary>
public sealed class AI29_1D_24B_2_Prompt6_ZeroSectionGateTests
{
    private static string UiRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "abhyanvaya-ui", "src"));

    [Fact]
    public void Ui_Zero_Eligible_Sections_Blocks_Continue_Even_When_Null_Targets()
    {
        var helper = File.ReadAllText(Path.Combine(UiRoot, "utils", "allocationTargetSectionSelection.ts"));
        Assert.Contains("eligibleSectionCount <= 0", helper);
        Assert.Contains("No eligible Sections are available for the selected academic scope.", helper);

        var workspace = File.ReadAllText(Path.Combine(UiRoot, "components", "allocation", "EnterpriseAllocationWorkspace.tsx"));
        Assert.Contains("canContinueWithTargetSections(targetSectionIds, context?.sections?.length ?? 0)", workspace);

        var panel = File.ReadAllText(Path.Combine(UiRoot, "components", "allocation", "AllocationCapacityPanel.tsx"));
        Assert.Contains("MSG_NO_ELIGIBLE_SECTIONS", panel);
    }

    [Fact]
    public void Ui_Fail_Closed_Clears_Context_On_Load_Failure()
    {
        var workspace = File.ReadAllText(Path.Combine(UiRoot, "components", "allocation", "EnterpriseAllocationWorkspace.tsx"));
        Assert.Contains("setEligibleSectionsError(true)", workspace);
        Assert.Contains("MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS", workspace);
        Assert.Contains("setTargetSectionIds(null)", workspace);
        var helper = File.ReadAllText(Path.Combine(UiRoot, "utils", "allocationTargetSectionSelection.ts"));
        Assert.Contains("Unable to load eligible Sections.", helper);
    }

    [Fact]
    public void Unauthorized_TargetSectionIds_Still_Rejected_By_10A()
    {
        var ctx = new SectionAllocationContext
        {
            ContextId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            ContextVersion = "1",
            SchemaVersion = "1.0.0",
            GeneratedAt = DateTime.UtcNow,
            Checksum = "P6",
            Hierarchy = new AllocationHierarchyProjection
            {
                AcademicYearId = 1,
                CourseId = 10,
                GroupId = 2,
                SemesterId = 3,
            },
            Sections =
            [
                new AllocationSectionProjection
                {
                    SectionId = 11,
                    SectionCode = "CA-A",
                    SectionName = "CA-A",
                    Lifecycle = "Active",
                },
            ],
            Capacities =
            [
                new AllocationCapacityProjection { SectionId = 11, MaximumCapacity = 40, AvailableCapacity = 40 },
            ],
            Students =
            [
                new AllocationStudentProjection { StudentId = 1, StudentNumber = "1", StudentName = "A" },
            ],
        };
        var config = new AllocationPipelineConfig
        {
            GroupingMode = AllocationGroupingModes.Alphabetical,
            EnabledStrategies = AllocationPipelineConfig.Default.EnabledStrategies,
            ConstraintPriorities = AllocationPipelineConfig.Default.ConstraintPriorities,
            TargetSectionIds = [11, 99],
        }.Normalize();
        var v = AllocationScopeSelectionValidator.Validate(ctx, config);
        Assert.False(v.IsValid);
        Assert.Contains(v.Errors, e => e.Contains("99"));
    }
}

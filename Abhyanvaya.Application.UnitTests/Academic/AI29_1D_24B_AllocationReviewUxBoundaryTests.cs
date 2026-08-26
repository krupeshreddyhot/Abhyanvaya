namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24B — UI terminology separation only; no new API/entity/DB.</summary>
public sealed class AI29_1D_24B_AllocationReviewUxBoundaryTests
{
    private static string UiRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "abhyanvaya-ui", "src"));

    [Fact]
    public void No_New_Allocation_Api_Or_Entity()
    {
        var domain = typeof(Abhyanvaya.Domain.Entities.Course).Assembly.GetTypes().Select(t => t.Name).ToHashSet();
        Assert.DoesNotContain("AllocationReviewUxEntity", domain);

        var helper = File.ReadAllText(Path.Combine(UiRoot, "utils", "allocationAdministratorCopy.ts"));
        Assert.DoesNotContain("fetch(", helper);
        Assert.DoesNotContain("api.", helper);
        Assert.Contains("presentAllocationIssue", helper);
    }

    [Fact]
    public void Workspace_Uses_Business_Workflow_Labels()
    {
        var workspace = File.ReadAllText(Path.Combine(UiRoot, "components", "allocation", "EnterpriseAllocationWorkspace.tsx"));
        Assert.Contains("Allocation Rules", workspace);
        Assert.Contains("Review Allocation", workspace);
        Assert.Contains("Approve Allocation", workspace);
        Assert.DoesNotContain("Guided AI29.1C", workspace);
        Assert.DoesNotContain("Review — Governance Lifecycle", workspace);
    }

    [Fact]
    public void Strategy_Panel_Hides_Engine_Payload_By_Default()
    {
        var strategy = File.ReadAllText(Path.Combine(UiRoot, "components", "allocation", "AllocationStrategyConfigPanel.tsx"));
        Assert.Contains("Student Order", strategy);
        Assert.Contains("Section Allocation Method", strategy);
        Assert.Contains("showTechnicalDetails", strategy);
        Assert.DoesNotContain("Engine payload preview", strategy);
        Assert.DoesNotContain("Pipeline strategies", strategy);
    }

    [Fact]
    public void Governance_Panel_Keeps_Server_Authority_And_Confirm_Dialog()
    {
        var governance = File.ReadAllText(Path.Combine(UiRoot, "components", "allocation", "AllocationGovernancePanel.tsx"));
        Assert.Contains("AcademicConfirmDialog", governance);
        Assert.Contains("canApprove", governance);
        Assert.Contains("LABEL_REVIEW_ACADEMIC_SCOPE", governance);
        Assert.Contains("LABEL_REPLAY_ALLOCATION", governance);
        Assert.DoesNotContain("Rebuild Allocation", governance);
        Assert.DoesNotContain("Regenerate Allocation", governance);
        Assert.DoesNotContain("Flag: stale context", governance);
        Assert.DoesNotContain("Re-run Allocation", governance);

        var copy = File.ReadAllText(Path.Combine(UiRoot, "utils", "allocationAdministratorCopy.ts"));
        Assert.Contains("Replay Allocation", copy);
        Assert.Contains("Review Academic Scope", copy);
        Assert.Contains("The academic information used for this allocation has changed", copy);
        Assert.Contains("Test allocation completed", copy);
        Assert.DoesNotContain("LABEL_REGENERATE_ALLOCATION", copy);
        Assert.DoesNotContain("StudentSection writes", copy);

        var workspace = File.ReadAllText(Path.Combine(UiRoot, "components", "allocation", "EnterpriseAllocationWorkspace.tsx"));
        Assert.Contains("setActiveStep(0)", workspace);
        Assert.Contains("MSG_REVIEW_SCOPE_THEN_GENERATE", workspace);
        Assert.Contains("replayAllocationScenario", workspace);
        Assert.DoesNotContain("/allocation/rebuild", workspace);

        var opsService = File.ReadAllText(Path.Combine(UiRoot, "services", "allocationOperationsService.ts"));
        Assert.Contains("/allocation/scenarios/${id}/replay", opsService);
        Assert.DoesNotContain("/allocation/rebuild", opsService);
    }
}

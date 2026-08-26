namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24B.2 Prompt 1 — discovery contract probes only (no production changes).
/// </summary>
public sealed class AI29_1D_24B_2_TargetSectionScopeDiscoveryTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string UiRoot => Path.Combine(RepoRoot, "abhyanvaya-ui", "src");

    [Fact]
    public void Discovery_Doc_Exists()
    {
        var path = Path.Combine(RepoRoot, "docs", "AI29_1D_24B_2_ARCHITECTURE_DISCOVERY.md");
        Assert.True(File.Exists(path), $"Missing discovery doc: {path}");
        var text = File.ReadAllText(path);
        Assert.Contains("Allocation Context", text);
        Assert.Contains("targetSectionIds", text);
        Assert.Contains("AllocationScopeSelectionValidator", text);
        Assert.Contains("No new endpoint required", text);
        Assert.Contains("None", text); // database
    }

    [Fact]
    public void Context_Builder_Scopes_Sections_By_Year_Course_Group_Semester()
    {
        var builder = File.ReadAllText(Path.Combine(
            RepoRoot, "Abhyanvaya.Application", "Academic", "Allocation", "SectionAllocationContextBuilder.cs"));
        Assert.Contains("s.AcademicYearId == scope.AcademicYearId", builder);
        Assert.Contains("s.CourseId == scope.CourseId", builder);
        Assert.Contains("s.GroupId == scope.GroupId", builder);
        Assert.Contains("s.SemesterId == scope.SemesterId", builder);
        Assert.Contains("s.TenantId == _currentUser.TenantId", builder);
    }

    [Fact]
    public void TenA_Rejects_Target_Section_Outside_Context()
    {
        var validator = File.ReadAllText(Path.Combine(
            RepoRoot, "Abhyanvaya.Application", "Academic", "Allocation", "AllocationScopeSelectionValidator.cs"));
        Assert.Contains("Target section id {id} is not present in the Allocation Context.", validator);
        Assert.Contains("All eligible sections", validator);
    }

    [Fact]
    public void Capacity_Panel_Fail_Closed_Scopes_Occupancy_By_Context_Section_Ids()
    {
        var panel = File.ReadAllText(Path.Combine(UiRoot, "components", "allocation", "AllocationCapacityPanel.tsx"));
        Assert.Contains("sectionIds", panel);
        Assert.Contains("filterOccupancyToContextSections", panel);
        Assert.DoesNotContain("targetIds.size > 0 ? all.filter((r) => targetIds.has(r.sectionId)) : all", panel);
        Assert.Contains("MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS", panel);
        Assert.Contains("Explicit selection", panel);
    }

    [Fact]
    public void Workspace_Clears_Targets_On_Scope_Key_Change()
    {
        var workspace = File.ReadAllText(Path.Combine(UiRoot, "components", "allocation", "EnterpriseAllocationWorkspace.tsx"));
        Assert.Contains("setTargetSectionIds(null)", workspace);
        Assert.Contains("getAllocationContext(scope", workspace);
        Assert.Contains("allocationScopeKey", workspace);
        Assert.Contains("scopeKey", workspace);
    }

    [Fact]
    public void No_New_CombinedSection_Entity_In_Domain()
    {
        var domain = typeof(Abhyanvaya.Domain.Entities.Course).Assembly.GetTypes().Select(t => t.Name).ToHashSet();
        Assert.DoesNotContain("CombinedSection", domain);
        Assert.Contains("SectionGroup", domain);
    }
}

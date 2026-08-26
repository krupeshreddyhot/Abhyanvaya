namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4A Prompt 3 — Architecture guards for TeachingGroupSection SoT boundary.</summary>
public sealed class TeachingGroupSectionArchitectureGuardTests
{
    [Fact]
    public void Section_application_service_does_not_project_or_infer()
    {
        var src = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupSectionApplicationService.cs");
        Assert.DoesNotContain("new TimetableSection", src);
        Assert.DoesNotContain(".IgnoreQueryFilters", src);
        Assert.DoesNotContain("CreateTeachingGroup", src);
        Assert.DoesNotContain("FirstOrDefaultAsync(x => x.SubjectAllocationId", src);
        Assert.Contains("TeachingGroupRules.EnsureSectionCompatibleWithTeachingGroup", src);
        Assert.Contains("ValidateSectionLinks", src);
    }

    [Fact]
    public void Projector_is_implemented_and_registered_in_Prompt4()
    {
        var iface = Read("Abhyanvaya.Application", "Scheduling", "ITeachingGroupSectionApplicationService.cs");
        Assert.Contains("interface ITimetableSectionProjector", iface);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntriesAsync", iface);
        Assert.Contains("ReplaceSectionsAndProjectAsync", iface);

        var impl = Read("Abhyanvaya.Application", "Scheduling", "TimetableSectionProjector.cs");
        Assert.Contains(": ITimetableSectionProjector", impl);
        Assert.Contains("new TimetableSection", impl);
        Assert.DoesNotContain("SaveChangesAsync", impl);
        Assert.DoesNotContain("CreateTeachingGroup", impl);
        Assert.DoesNotContain(".IgnoreQueryFilters", impl);
    }

    [Fact]
    public void SetTimetableSections_routes_through_TeachingGroup_boundary()
    {
        var service = Read("Abhyanvaya.Application", "Academic", "SectionManagementService.cs");
        var methodStart = service.IndexOf("SetTimetableSectionsAsync", StringComparison.Ordinal);
        var snippet = service.Substring(methodStart, Math.Min(2200, service.Length - methodStart));
        Assert.Contains("ReplaceSectionsAndProjectAsync", snippet);
        Assert.DoesNotContain("new TimetableSection", snippet);
        Assert.Contains("ITeachingGroupSectionApplicationService", service);
    }

    [Fact]
    public void Di_registers_TeachingGroupSection_and_projector()
    {
        var di = Read("Abhyanvaya.Application", "DependencyInjection.cs");
        Assert.Contains("ITeachingGroupSectionApplicationService", di);
        Assert.Contains("TeachingGroupSectionApplicationService", di);
        Assert.Contains("ITimetableSectionProjector", di);
        Assert.Contains("TimetableSectionProjector", di);
    }

    [Fact]
    public void Section_service_orchestrates_single_commit_projection_path()
    {
        var src = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupSectionApplicationService.cs");
        Assert.Contains("ReplaceSectionsAndProjectAsync", src);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntriesAsync", src);
        // SoT file still must not construct TimetableSection directly — projector owns that.
        Assert.DoesNotContain("new TimetableSection", src);
    }

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { FindRepoRoot() }.Concat(parts).ToArray()));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Infrastructure", "Abhyanvaya.Infrastructure.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}

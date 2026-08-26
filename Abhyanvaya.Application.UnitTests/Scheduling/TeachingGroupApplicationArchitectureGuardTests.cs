namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>
/// AI-SCHED-TG.4 Prompt 3 — Architecture guards against implicit TeachingGroup resolution,
/// tenant bypass, and side-effect mutations during TimetableEntry assignment.
/// </summary>
public sealed class TeachingGroupApplicationArchitectureGuardTests
{
    [Fact]
    public void Application_service_has_no_implicit_TeachingGroup_resolvers()
    {
        var src = Read("Abhyanvaya.Application", "Scheduling", "TeachingGroupApplicationService.cs");
        Assert.DoesNotContain("FindTeachingGroup", src);
        Assert.DoesNotContain("FindFirstTeachingGroup", src);
        Assert.DoesNotContain("FindSingleTeachingGroup", src);
        Assert.DoesNotContain("CreateTeachingGroupIfMissing", src);
        Assert.DoesNotContain("ResolveTeachingGroupFromSection", src);
        Assert.DoesNotContain("ResolveTeachingGroupFromTimetableSection", src);
        Assert.DoesNotContain(".IgnoreQueryFilters", src);
        // Prompt 21: may orchestrate ITimetableSectionProjector but must not construct TimetableSection.
        Assert.DoesNotContain("new TimetableSection", src);
        Assert.Contains("ITimetableSectionProjector", src);
        Assert.Contains("SyncTeachingGroupSectionsToTimetableEntryAsync", src);
        Assert.Contains("ClearTimetableEntryProjectionAsync", src);
        Assert.DoesNotContain("TeachingGroupMembership", src);
        Assert.DoesNotContain("AttendanceSessionResolver", src);
        Assert.Contains("ConcurrencyExceptionHelper.SaveChangesAsync", src);
        Assert.DoesNotContain("_projector.SaveChanges", src);
    }

    [Fact]
    public void TimetableService_does_not_infer_or_create_TeachingGroup()
    {
        var src = Read("Abhyanvaya.Application", "Scheduling", "TimetableService.cs");
        Assert.DoesNotContain("CreateTeachingGroup", src);
        Assert.DoesNotContain("FindTeachingGroup", src);
        Assert.DoesNotContain("ResolveTeachingGroup", src);
        Assert.DoesNotContain(".IgnoreQueryFilters", src);
        // ApplyAllocationDenormalization must not assign TeachingGroupId
        var denormStart = src.IndexOf("public static void ApplyAllocationDenormalization", StringComparison.Ordinal);
        Assert.True(denormStart >= 0);
        var brace = src.IndexOf('{', denormStart);
        var depth = 0;
        var denormEnd = brace;
        for (var i = brace; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    denormEnd = i;
                    break;
                }
            }
        }
        var denormBody = src.Substring(denormStart, denormEnd - denormStart + 1);
        Assert.DoesNotContain("TeachingGroupId", denormBody);
        Assert.Contains("EnsureProposedTeachingGroupCompatibleAsync", src);
        Assert.DoesNotContain("CreateTeachingGroupIfMissing", src);
        Assert.DoesNotContain("FindFirstTeachingGroup", src);
    }

    [Fact]
    public void Update_and_create_entry_DTOs_omit_TeachingGroupId_to_prevent_accidental_nulling()
    {
        var dtoSrc = Read("Abhyanvaya.Application", "DTOs", "Scheduling", "TimetableDtos.cs");
        Assert.Contains("class AssignTeachingGroupToTimetableEntryRequest", dtoSrc);
        Assert.Contains("class UpdateTimetableEntryRequest", dtoSrc);
        Assert.Contains("class CreateTimetableEntryRequest", dtoSrc);

        var updateBlock = ExtractClass(dtoSrc, "UpdateTimetableEntryRequest");
        var createBlock = ExtractClass(dtoSrc, "CreateTimetableEntryRequest");
        Assert.DoesNotContain("TeachingGroupId", updateBlock);
        Assert.DoesNotContain("TeachingGroupId", createBlock);
    }

    [Fact]
    public void Explicit_assignment_API_is_dedicated_and_authorized()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs");
        Assert.Contains("[HttpPut(\"entries/{entryId:int}/teaching-group\")]", controller);
        Assert.Contains("[HttpDelete(\"entries/{entryId:int}/teaching-group\")]", controller);
        Assert.Contains("CanManageSchedulingTimetable", controller);
        Assert.Contains("ITeachingGroupApplicationService", controller);
        Assert.DoesNotContain(".IgnoreQueryFilters", controller);
    }

    [Fact]
    public void Legacy_sections_bridge_and_Attendance_remain_untouched_by_Prompt3()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs");
        // SetTimetableSections still exists (legacy) but AssignTeachingGroup must not call it
        var assignBlock = ExtractMethod(controller, "AssignTeachingGroup");
        var clearBlock = ExtractMethod(controller, "ClearTeachingGroup");
        Assert.DoesNotContain("SetTimetableSections", assignBlock);
        Assert.DoesNotContain("SetTimetableSections", clearBlock);
        Assert.DoesNotContain("TimetableSection", assignBlock);
        Assert.DoesNotContain("TimetableSection", clearBlock);

        var resolver = Read("Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs");
        Assert.DoesNotContain("TeachingGroup", resolver);
    }

    [Fact]
    public void No_new_migration_introduced_by_Prompt3_application_boundary()
    {
        var migrationsDir = Path.Combine(FindRepoRoot(), "Abhyanvaya.Infrastructure", "Persistence", "Migrations");
        var prompt3Migrations = Directory.GetFiles(migrationsDir, "*TG_4*Prompt*3*")
            .Concat(Directory.GetFiles(migrationsDir, "*TeachingGroupApplication*"))
            .ToArray();
        Assert.Empty(prompt3Migrations);
    }

    private static string ExtractClass(string src, string className)
    {
        var marker = "class " + className;
        var start = src.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Class {className} not found");
        var brace = src.IndexOf('{', start);
        var depth = 0;
        for (var i = brace; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return src.Substring(start, i - start + 1);
            }
        }

        return src.Substring(start);
    }

    private static string ExtractMethod(string src, string methodName)
    {
        var start = src.IndexOf(methodName + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method {methodName} not found");
        // Walk back to attributes / signature start
        var attrStart = src.LastIndexOf('[', start);
        if (attrStart < 0 || start - attrStart > 400) attrStart = start;
        var brace = src.IndexOf('{', start);
        var depth = 0;
        for (var i = brace; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return src.Substring(attrStart, i - attrStart + 1);
            }
        }

        return src.Substring(attrStart, Math.Min(800, src.Length - attrStart));
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

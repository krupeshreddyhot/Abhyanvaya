namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.6 Prompt 4 / Prompt 2A — Architecture guards for compatible TG query.</summary>
public sealed class AiSchedTg6Prompt4Prompt2AArchitectureGuardTests
{
    [Fact]
    public void Compatible_query_endpoint_is_entry_scoped_read_only_and_authorized()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs");
        Assert.Contains("[HttpGet(\"entries/{entryId:int}/compatible-teaching-groups\")]", controller);
        Assert.Contains("GetCompatibleTeachingGroups", controller);
        Assert.Contains("CanViewSchedulingTimetable", controller);
        Assert.Contains("ICompatibleTeachingGroupQueryService", controller);

        var getBlock = ExtractMethod(controller, "GetCompatibleTeachingGroups");
        Assert.DoesNotContain("SaveChanges", getBlock);
        Assert.DoesNotContain("AssignToTimetableEntry", getBlock);
        Assert.DoesNotContain("ClearFromTimetableEntry", getBlock);
        Assert.DoesNotContain("SetTimetableSections", getBlock);
        Assert.DoesNotContain("TimetableSection", getBlock);
    }

    [Fact]
    public void Assign_and_clear_endpoints_remain_unchanged_owners_of_mutation()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling", "TimetableControllers.cs");
        Assert.Contains("[HttpPut(\"entries/{entryId:int}/teaching-group\")]", controller);
        Assert.Contains("[HttpDelete(\"entries/{entryId:int}/teaching-group\")]", controller);

        var assign = ExtractMethod(controller, "AssignTeachingGroup");
        var clear = ExtractMethod(controller, "ClearTeachingGroup");
        Assert.Contains("CanManageSchedulingTimetable", assign);
        Assert.Contains("CanManageSchedulingTimetable", clear);
    }

    [Fact]
    public void Query_service_has_no_tenant_bypass_or_side_effects()
    {
        var src = Read("Abhyanvaya.Application", "Scheduling", "CompatibleTeachingGroupQueryService.cs");
        Assert.DoesNotContain(".IgnoreQueryFilters", src);
        Assert.DoesNotContain("SaveChanges", src);
        Assert.DoesNotContain("CreateTeachingGroup", src);
        Assert.DoesNotContain("preferredRoomId", src);
        Assert.DoesNotContain("TimetableSection", src);
        Assert.DoesNotContain("StudentSection", src);
        Assert.DoesNotMatch(@"\bAttendance\b", src);
        Assert.Contains("AsNoTracking", src);
        Assert.Contains("EnsureCompatibleWithTimetableEntry", src); // documented alignment in comments
        Assert.Contains("Status != TeachingGroupStatus.Archived", src);
    }

    [Fact]
    public void Dto_matches_approved_selector_contract_without_membership_internals()
    {
        var dtoSrc = Read("Abhyanvaya.Application", "DTOs", "Scheduling", "TimetableDtos.cs");
        var block = ExtractClass(dtoSrc, "CompatibleTeachingGroupOptionDto");
        Assert.Contains("Id", block);
        Assert.Contains("Code", block);
        Assert.Contains("Name", block);
        Assert.Contains("Type", block);
        Assert.Contains("Status", block);
        Assert.Contains("ResolvedStudentCount", block);
        Assert.Contains("ExpectedStudentCount", block);
        Assert.Contains("MaxTeachingCapacity", block);
        Assert.Contains("IsAssignedToEntry", block);
        Assert.DoesNotContain("Memberships", block);
        Assert.DoesNotContain("ResolvedMembers", block);
        Assert.DoesNotContain("PlannedCapacity", block);
    }

    [Fact]
    public void Create_Update_Upsert_still_omit_TeachingGroupId()
    {
        var dtoSrc = Read("Abhyanvaya.Application", "DTOs", "Scheduling", "TimetableDtos.cs");
        Assert.DoesNotContain("TeachingGroupId", ExtractClass(dtoSrc, "CreateTimetableEntryRequest"));
        Assert.DoesNotContain("TeachingGroupId", ExtractClass(dtoSrc, "UpdateTimetableEntryRequest"));
        Assert.DoesNotContain("TeachingGroupId", ExtractClass(dtoSrc, "UpsertTimetableEntryRequest"));
    }

    [Fact]
    public void No_migration_introduced_by_Prompt_2A()
    {
        var migrationsDir = Path.Combine(FindRepoRoot(), "Abhyanvaya.Infrastructure", "Persistence", "Migrations");
        var hits = Directory.GetFiles(migrationsDir, "*CompatibleTeaching*")
            .Concat(Directory.GetFiles(migrationsDir, "*TG_6*Prompt*2A*"))
            .ToArray();
        Assert.Empty(hits);
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

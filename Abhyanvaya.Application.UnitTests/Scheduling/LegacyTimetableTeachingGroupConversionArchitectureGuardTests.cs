namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4A Prompt 7 — Guards: conversion is explicit-only; never from GET/read/startup.</summary>
public sealed class LegacyTimetableTeachingGroupConversionArchitectureGuardTests
{
    [Fact]
    public void Conversion_is_scoped_service_not_hosted_job()
    {
        var di = Read("Abhyanvaya.Application", "DependencyInjection.cs");
        Assert.Contains("ILegacyTimetableTeachingGroupConversionService", di);
        Assert.Contains("LegacyTimetableTeachingGroupConversionService", di);
        Assert.Contains("AddScoped<ILegacyTimetableTeachingGroupConversionService", di);
        Assert.DoesNotContain("AddHostedService<LegacyTimetableTeachingGroupConversionService>", di);
        Assert.DoesNotContain("AddHostedService<ILegacyTimetableTeachingGroupConversionService>", di);
    }

    [Fact]
    public void Get_and_Attendance_read_paths_do_not_invoke_conversion()
    {
        var get = ExtractMethod(
            Read("Abhyanvaya.Application", "Academic", "SectionManagementService.cs"),
            "GetTimetableSectionsAsync");
        var combined = ExtractMethod(
            Read("Abhyanvaya.Application", "Academic", "SectionManagementService.cs"),
            "GetCombinedSessionsAsync");
        var attendance = Read("Abhyanvaya.Application", "Scheduling", "Conflicts", "AttendanceSessionResolver.cs");

        foreach (var src in new[] { get, combined, attendance })
        {
            Assert.DoesNotContain("ILegacyTimetableTeachingGroupConversionService", src);
            Assert.DoesNotContain("LegacyTimetableTeachingGroupConversionService", src);
            Assert.DoesNotContain("ConvertAsync", src);
            Assert.DoesNotContain("ListEntriesWithoutTeachingGroupAsync", src);
        }
    }

    [Fact]
    public void Program_startup_does_not_invoke_conversion()
    {
        var program = Read("Abhyanvaya.API", "Program.cs");
        Assert.DoesNotContain("ILegacyTimetableTeachingGroupConversionService", program);
        Assert.DoesNotContain("LegacyTimetableTeachingGroupConversion", program);
        Assert.DoesNotContain("ConvertLegacyTimetableEntries", program);
    }

    [Fact]
    public void Conversion_controller_is_explicit_POST_with_manage_policy()
    {
        var controller = Read("Abhyanvaya.API", "Controllers", "Scheduling",
            "LegacyTimetableTeachingGroupConversionController.cs");
        Assert.Contains("[Route(\"api/scheduling/legacy-teaching-group-conversion\")]", controller);
        Assert.Contains("CanManageSchedulingTimetable", controller);
        Assert.Contains("[HttpPost]", controller);
        Assert.Contains("ConvertAsync", controller);
        Assert.DoesNotContain("[HttpGet]\r\n    public async Task<ActionResult<LegacyTimetableConversionReportDto>> Convert", controller);
    }

    [Fact]
    public void TimetableSections_GET_controller_does_not_reference_conversion()
    {
        var sections = Read("Abhyanvaya.API", "Controllers", "SectionsController.cs");
        Assert.DoesNotContain("ILegacyTimetableTeachingGroupConversionService", sections);
        Assert.DoesNotContain("LegacyTimetableTeachingGroupConversion", sections);
    }

    [Fact]
    public void Conversion_service_forbids_inference_and_implicit_TG_create()
    {
        var src = Read("Abhyanvaya.Application", "Scheduling", "LegacyTimetableTeachingGroupConversionService.cs");
        Assert.DoesNotContain("new TeachingGroup", src);
        Assert.DoesNotContain("SchedulingSubjectAllocations", src);
        Assert.DoesNotContain("FirstOrDefaultAsync(x => x.SubjectAllocationId", src);
        Assert.DoesNotContain(".IgnoreQueryFilters", src);
        Assert.DoesNotContain("StudentSection", src);
        Assert.DoesNotContain("Attendances", src);
        Assert.Contains("ReplaceSectionsAndProjectAsync", src);
        Assert.Contains("DryRun", src);
        Assert.Contains("never inferred", src, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractMethod(string src, string methodName)
    {
        var start = src.IndexOf(methodName + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, methodName + " not found");
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

        return src.Substring(start, Math.Min(2500, src.Length - start));
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

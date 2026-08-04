using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Configuration;

public interface ISchedulingConfigurationReadinessService
{
    Task<SchedulingReadinessSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public interface ISchedulingSetupValidator
{
    Task<SchedulingSetupValidationDto> ValidateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// AI30.3.5.4 / 3.5.6 — readiness + next recommended step. Read-only; no timetable/attendance mutation.
/// No caching (per prompt).
/// </summary>
public sealed class SchedulingConfigurationReadinessService : ISchedulingConfigurationReadinessService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SchedulingConfigurationReadinessService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SchedulingReadinessSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var counts = await LoadCountsAsync(tenantId, cancellationToken);
        var moduleStatus = BuildModuleStatuses(counts);
        var byKey = moduleStatus.ToDictionary(m => m.ModuleKey, StringComparer.OrdinalIgnoreCase);

        var sections = new[]
        {
            BuildSection("academic-calendar", "Academic Calendar", ["academic-years", "working-days", "holidays", "holiday-types"], byKey),
            BuildSection("infrastructure", "Infrastructure", ["campuses", "rooms", "room-features", "room-availability"], byKey),
            BuildSection("framework", "Scheduling Framework", ["time-slots", "time-slot-templates", "subject-categories", "subject-delivery", "room-rules"], byKey),
            BuildSection("faculty-planning", "Faculty Planning", ["faculty-availability", "faculty-preferences", "faculty-workloads", "subject-allocations"], byKey),
            BuildSection("timetable", "Timetable", ["schedule-versions", "timetable-designer", "faculty-timetable", "student-timetable", "room-timetable"], byKey),
            BuildSection("governance", "Governance", ["approval-queue", "publishing", "clone-wizard", "change-history", "governance-dashboard"], byKey),
            BuildSection("validation", "Validation", ["conflict-dashboard", "conflict-workspace", "conflict-analytics", "conflict-rules"], byKey),
            BuildSection("optimization", "Optimization", ["optimization-preview", "optimization-workspace", "optimization-dashboard"], byKey),
        };

        var requiredKeys = SchedulingModuleCatalog.Modules.Where(m => m.RequiredForMinimum).Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredStatuses = moduleStatus.Where(m => requiredKeys.Contains(m.ModuleKey)).ToList();
        var overall = requiredStatuses.Count == 0
            ? 0
            : 100.0 * requiredStatuses.Count(m => m.Status is "Complete" or "Optional") / requiredStatuses.Count;

        var completed = moduleStatus.Count(m => m.Status == "Complete");
        var blocked = moduleStatus.Count(m => m.Status == "Blocked");
        var pending = moduleStatus.Count(m => m.Status is "Missing" or "Partial" or "Required" or "Blocked");

        var next = ResolveNextStep(byKey, counts);

        var edges = SchedulingModuleCatalog.Modules
            .SelectMany(m => m.Requires.Select(r => new SchedulingDependencyEdgeDto { From = r, To = m.Key }))
            .DistinctBy(e => $"{e.From}->{e.To}")
            .Take(80)
            .ToList();

        return new SchedulingReadinessSummaryDto
        {
            OverallPercent = Math.Round(overall, 1),
            Sections = sections,
            Modules = moduleStatus,
            NextRecommendedStep = next,
            CompletedModules = completed,
            PendingModules = pending,
            BlockedModules = blocked,
            ProgressChart = sections.Select(s => new SchedulingChartPointDto
            {
                Label = s.Title,
                Value = (decimal)Math.Round(s.PercentComplete, 1)
            }).ToList(),
            DependencyTree = edges
        };
    }

    private async Task<Dictionary<string, int>> LoadCountsAsync(int tenantId, CancellationToken ct)
    {
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["academic-years"] = await _db.SchedulingAcademicYears.CountAsync(x => x.TenantId == tenantId, ct),
            ["working-days"] = await _db.SchedulingWorkingDays.CountAsync(x => x.TenantId == tenantId, ct),
            ["holidays"] = await _db.SchedulingHolidays.CountAsync(x => x.TenantId == tenantId, ct),
            ["holiday-types"] = await _db.SchedulingHolidayTypeCatalogs.CountAsync(x => x.TenantId == tenantId, ct),
            ["campuses"] = await _db.SchedulingCampuses.CountAsync(x => x.TenantId == tenantId, ct),
            ["rooms"] = await _db.SchedulingRooms.CountAsync(x => x.TenantId == tenantId, ct),
            ["room-features"] = await _db.SchedulingRoomFeatures.CountAsync(x => x.TenantId == tenantId, ct),
            ["room-availability"] = await _db.SchedulingRoomAvailabilities.CountAsync(x => x.TenantId == tenantId, ct),
            ["time-slots"] = await _db.SchedulingTimeSlots.CountAsync(x => x.TenantId == tenantId, ct),
            ["time-slot-templates"] = await _db.SchedulingTimeSlotTemplates.CountAsync(x => x.TenantId == tenantId, ct),
            ["subject-categories"] = await _db.SchedulingSubjectCategories.CountAsync(x => x.TenantId == tenantId, ct),
            ["subject-delivery"] = await _db.SchedulingSubjectDeliveryTypes.CountAsync(x => x.TenantId == tenantId, ct),
            ["room-rules"] = await _db.SchedulingRoomAllocationRules.CountAsync(x => x.TenantId == tenantId, ct),
            ["faculty-availability"] = await _db.SchedulingFacultyAvailabilities.CountAsync(x => x.TenantId == tenantId, ct),
            ["faculty-preferences"] = await _db.SchedulingFacultyTeachingPreferences.CountAsync(x => x.TenantId == tenantId, ct),
            ["faculty-workloads"] = await _db.SchedulingFacultyWorkloads.CountAsync(x => x.TenantId == tenantId, ct),
            ["subject-allocations"] = await _db.SchedulingSubjectAllocations.CountAsync(x => x.TenantId == tenantId, ct),
            ["faculty"] = await _db.StaffMembers.CountAsync(x => x.TenantId == tenantId, ct),
            ["departments"] = await _db.Departments.CountAsync(x => x.TenantId == tenantId, ct),
            ["subjects"] = await _db.Subjects.CountAsync(x => x.TenantId == tenantId, ct),
            ["schedule-versions"] = await _db.SchedulingScheduleVersions.CountAsync(x => x.TenantId == tenantId, ct),
            ["timetable-designer"] = await _db.SchedulingTimetables.CountAsync(x => x.TenantId == tenantId, ct),
            ["approval-queue"] = await _db.SchedulingTimetableApprovalRequests.CountAsync(x => x.TenantId == tenantId, ct),
            ["publishing"] = await _db.SchedulingTimetables.CountAsync(x => x.TenantId == tenantId, ct),
            ["clone-wizard"] = await _db.SchedulingTimetableCloneJobs.CountAsync(x => x.TenantId == tenantId, ct),
            ["change-history"] = await _db.SchedulingTimetableChangeHistories.CountAsync(x => x.TenantId == tenantId, ct),
            ["conflict-dashboard"] = await _db.SchedulingConflictFindings.CountAsync(x => x.TenantId == tenantId, ct),
            ["conflict-rules"] = await _db.SchedulingConflictRuleThresholdSettings.CountAsync(x => x.TenantId == tenantId, ct),
            ["optimization-preview"] = await _db.SchedulingOptimizationSimulationRuns.CountAsync(x => x.TenantId == tenantId, ct),
            ["optimization-workspace"] = await _db.SchedulingOptimizationScenarios.CountAsync(x => x.TenantId == tenantId, ct),
            ["optimization-dashboard"] = await _db.SchedulingOptimizationEngineRuns.CountAsync(x => x.TenantId == tenantId, ct),
        };
    }

    private static List<SchedulingModuleStatusDto> BuildModuleStatuses(Dictionary<string, int> counts)
    {
        var list = new List<SchedulingModuleStatusDto>();
        foreach (var mod in SchedulingModuleCatalog.Modules)
        {
            var missingReqs = mod.Requires
                .Where(r => counts.GetValueOrDefault(r) <= 0)
                .Select(r => SchedulingModuleCatalog.Modules.FirstOrDefault(m => m.Key == r)?.Title ?? r)
                .ToList();

            // Subject allocation also needs Catalog masters (faculty/subjects/depts) — advisory block
            if (mod.Key == "subject-allocations")
            {
                if (counts.GetValueOrDefault("faculty") <= 0) missingReqs.Add("Faculty (Catalog)");
                if (counts.GetValueOrDefault("subjects") <= 0) missingReqs.Add("Subjects (Catalog)");
                if (counts.GetValueOrDefault("departments") <= 0) missingReqs.Add("Departments (Catalog)");
            }

            var count = counts.GetValueOrDefault(mod.Key);
            string status;
            string tooltip;

            if (missingReqs.Count > 0 && count <= 0)
            {
                status = "Blocked";
                tooltip = $"Requires: {string.Join(", ", missingReqs)}";
            }
            else if (mod.RequiredForMinimum && count <= 0)
            {
                status = missingReqs.Count > 0 ? "Blocked" : "Required";
                tooltip = missingReqs.Count > 0
                    ? $"Requires: {string.Join(", ", missingReqs)}"
                    : "Required for minimum timetable configuration.";
            }
            else if (count > 0)
            {
                status = "Complete";
                tooltip = $"{count} record(s) configured.";
            }
            else if (!mod.RequiredForMinimum)
            {
                status = "Optional";
                tooltip = "Optional — improves quality but not required for first timetable.";
            }
            else
            {
                status = "Missing";
                tooltip = "Not configured yet.";
            }

            // View modules that depend on designer: Partial if designer exists
            if (mod.Key is "faculty-timetable" or "student-timetable" or "room-timetable" or "governance-dashboard"
                or "conflict-workspace" or "conflict-analytics")
            {
                if (counts.GetValueOrDefault("timetable-designer") > 0)
                {
                    status = "Complete";
                    tooltip = "Available — open module when needed.";
                }
                else if (status != "Blocked")
                {
                    status = "Optional";
                    tooltip = "Available after a timetable exists.";
                }
            }

            list.Add(new SchedulingModuleStatusDto
            {
                ModuleKey = mod.Key,
                Path = mod.Path,
                Title = mod.Title,
                Status = status,
                Tooltip = tooltip,
                Requires = mod.Requires.ToList(),
                UsedBy = mod.UsedBy.ToList(),
                RelatedModules = mod.Related.ToList(),
                HelpDocPath = mod.HelpDocPath
            });
        }

        return list;
    }

    private static SchedulingReadinessSectionDto BuildSection(
        string key,
        string title,
        string[] moduleKeys,
        Dictionary<string, SchedulingModuleStatusDto> byKey)
    {
        var mods = moduleKeys.Select(k => byKey.GetValueOrDefault(k)).Where(m => m is not null).Cast<SchedulingModuleStatusDto>().ToList();
        var complete = mods.Count(m => m.Status == "Complete");
        var blocked = mods.Where(m => m.Status == "Blocked").SelectMany(m => m.Requires).Distinct().ToList();
        var missing = mods.Where(m => m.Status is "Missing" or "Required").Select(m => m.Title).ToList();
        var percent = mods.Count == 0 ? 0 : 100.0 * complete / mods.Count;

        var status = blocked.Count > 0 && complete == 0 ? "Blocked"
            : complete == mods.Count ? "Complete"
            : complete > 0 ? "Partial"
            : "Missing";

        return new SchedulingReadinessSectionDto
        {
            Key = key,
            Title = title,
            Status = status,
            PercentComplete = Math.Round(percent, 1),
            Messages =
            [
                $"{complete}/{mods.Count} modules complete"
            ],
            MissingItems = missing,
            BlockedBy = blocked
        };
    }

    private static SchedulingNextStepDto? ResolveNextStep(
        Dictionary<string, SchedulingModuleStatusDto> byKey,
        Dictionary<string, int> counts)
    {
        foreach (var key in SchedulingModuleCatalog.MinimumPathOrder)
        {
            if (!byKey.TryGetValue(key, out var mod)) continue;
            if (mod.Status is "Complete" or "Optional") continue;
            var def = SchedulingModuleCatalog.Modules.First(m => m.Key == key);
            return new SchedulingNextStepDto
            {
                ModuleKey = key,
                Title = def.Title,
                Path = def.Path,
                Reason = mod.Tooltip
            };
        }

        // All minimum done — suggest publishing if timetables exist
        if (counts.GetValueOrDefault("timetable-designer") > 0)
        {
            var pub = SchedulingModuleCatalog.Modules.First(m => m.Key == "publishing");
            return new SchedulingNextStepDto
            {
                ModuleKey = pub.Key,
                Title = pub.Title,
                Path = pub.Path,
                Reason = "Minimum configuration complete — publish when timetable is ready."
            };
        }

        return null;
    }
}

/// <summary>AI30.3.5.9 — advisory setup validation. Never blocks; skips conflict detection.</summary>
public sealed class SchedulingSetupValidator : ISchedulingSetupValidator
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SchedulingSetupValidator(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<SchedulingSetupValidationDto> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var errors = new List<SchedulingSetupIssueDto>();
        var warnings = new List<SchedulingSetupIssueDto>();
        var suggestions = new List<SchedulingSetupIssueDto>();

        async Task Check(string code, string path, string title, Task<int> countTask, bool errorIfMissing)
        {
            var count = await countTask;
            if (count > 0) return;
            var issue = new SchedulingSetupIssueDto
            {
                Code = code,
                Severity = errorIfMissing ? "Error" : "Warning",
                Message = $"{title} is not configured.",
                Suggestion = $"Open {title} and add at least one record.",
                Path = path
            };
            if (errorIfMissing) errors.Add(issue);
            else warnings.Add(issue);
        }

        await Check("AY", "/setup/scheduling/academic-years", "Academic Year",
            _db.SchedulingAcademicYears.CountAsync(x => x.TenantId == tenantId, cancellationToken), true);
        await Check("WD", "/setup/scheduling/working-days", "Working Days",
            _db.SchedulingWorkingDays.CountAsync(x => x.TenantId == tenantId, cancellationToken), true);
        await Check("CAMPUS", "/setup/scheduling/campuses", "Campus",
            _db.SchedulingCampuses.CountAsync(x => x.TenantId == tenantId, cancellationToken), true);
        await Check("ROOM", "/setup/scheduling/rooms", "Rooms",
            _db.SchedulingRooms.CountAsync(x => x.TenantId == tenantId, cancellationToken), true);
        await Check("SLOT", "/setup/scheduling/time-slots", "Time Slots",
            _db.SchedulingTimeSlots.CountAsync(x => x.TenantId == tenantId, cancellationToken), true);
        await Check("FAC", "/setup/staff", "Faculty",
            _db.StaffMembers.CountAsync(x => x.TenantId == tenantId, cancellationToken), true);
        await Check("ALLOC", "/setup/scheduling/subject-allocations", "Subject Allocation",
            _db.SchedulingSubjectAllocations.CountAsync(x => x.TenantId == tenantId, cancellationToken), true);
        await Check("VER", "/setup/scheduling/governance/versions", "Schedule Version",
            _db.SchedulingScheduleVersions.CountAsync(x => x.TenantId == tenantId, cancellationToken), true);

        if (await _db.SchedulingTimetables.CountAsync(x => x.TenantId == tenantId, cancellationToken) == 0)
        {
            suggestions.Add(new SchedulingSetupIssueDto
            {
                Code = "TT",
                Severity = "Suggestion",
                Message = "No timetable drafts yet.",
                Suggestion = "After minimum configuration, open Timetable Designer.",
                Path = "/setup/scheduling/timetables"
            });
        }

        if (await _db.SchedulingHolidays.CountAsync(x => x.TenantId == tenantId, cancellationToken) == 0)
        {
            suggestions.Add(new SchedulingSetupIssueDto
            {
                Code = "HOL",
                Severity = "Suggestion",
                Message = "Holiday calendar is empty.",
                Suggestion = "Add holidays to avoid scheduling on closed days.",
                Path = "/setup/scheduling/holidays"
            });
        }

        return new SchedulingSetupValidationDto
        {
            Errors = errors,
            Warnings = warnings,
            Suggestions = suggestions
        };
    }
}

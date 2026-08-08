using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>Read-only merge preview engine — no writes, no lifecycle transitions.</summary>
public sealed class MergePreviewService : IMergePreviewService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionCapacityEngine _capacity;
    private readonly ISectionReadinessService _readiness;
    private readonly ISectionPolicyService _policies;
    private readonly IAcademicTelemetryService _telemetry;

    public MergePreviewService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionCapacityEngine capacity,
        ISectionReadinessService readiness,
        ISectionPolicyService policies,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _capacity = capacity;
        _readiness = readiness;
        _policies = policies;
        _telemetry = telemetry;
    }

    public Task<MergePreviewEngineDto> PreviewAsync(
        IReadOnlyList<int> sourceSectionIds,
        int targetSectionId,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.SectionMergePreview,
            "MergePreview.Preview",
            ct => BuildAsync(sourceSectionIds, targetSectionId, ct),
            cancellationToken);

    private async Task<MergePreviewEngineDto> BuildAsync(
        IReadOnlyList<int> sourceSectionIds,
        int targetSectionId,
        CancellationToken ct)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var sources = (sourceSectionIds ?? []).Where(id => id > 0).Distinct().ToList();
        if (sources.Count < 1) errors.Add("Select at least one source section.");

        var sourceEntities = await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && sources.Contains(s.Id))
            .ToListAsync(ct);
        if (sourceEntities.Count != sources.Count) errors.Add("One or more source sections were not found.");

        var target = await _db.Sections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == targetSectionId && s.TenantId == _currentUser.TenantId, ct);
        if (target is null) errors.Add("Target section not found.");
        else if (sources.Contains(target.Id)) errors.Add("Target cannot also be a source.");

        if (target is not null && sourceEntities.Count > 0)
        {
            if (sourceEntities.Any(s =>
                    s.AcademicYearId != target.AcademicYearId
                    || s.CourseId != target.CourseId
                    || s.GroupId != target.GroupId
                    || s.SemesterId != target.SemesterId))
                errors.Add("All sections must share academic year, course, group, and semester.");

            foreach (var s in sourceEntities)
            {
                var st = SectionLifecycleStates.Normalize(s.Status);
                if (st is SectionLifecycleStates.Merged or SectionLifecycleStates.Split or SectionLifecycleStates.Archived)
                    errors.Add($"Section {s.SectionCode} cannot be merged (status {st}).");
            }

            var policy = await _policies.ResolveForSectionAsync(target.Id, ct);
            if (!policy.AllowMerge) errors.Add("Resolved section policy disallows merge.");
            warnings.AddRange(policy.Warnings);
        }

        var allIds = sources.Concat(target is null ? [] : new[] { target.Id }).ToList();
        var studentCount = allIds.Count == 0
            ? 0
            : await _db.StudentSections.CountAsync(
                x => x.TenantId == _currentUser.TenantId && x.IsCurrent && allIds.Contains(x.SectionId), ct);
        var facultyCount = allIds.Count == 0
            ? 0
            : await _db.FacultySectionAssignments.CountAsync(
                x => x.TenantId == _currentUser.TenantId && x.IsCurrent && allIds.Contains(x.SectionId), ct);
        var timetableCount = allIds.Count == 0
            ? 0
            : await _db.TimetableSections.CountAsync(
                x => x.TenantId == _currentUser.TenantId && allIds.Contains(x.SectionId), ct);
        var attendanceLinks = allIds.Count == 0
            ? 0
            : await _db.AttendanceSessionSections.CountAsync(
                x => x.TenantId == _currentUser.TenantId && allIds.Contains(x.SectionId), ct);

        var currentCap = sourceEntities.Sum(s => s.MaximumStrength) + (target?.MaximumStrength ?? 0);
        var mergedCap = target?.MaximumStrength ?? 0;
        if (studentCount > mergedCap && mergedCap > 0)
            warnings.Add($"Combined strength ({studentCount}) exceeds target capacity ({mergedCap}).");
        if (attendanceLinks > 0)
            warnings.Add($"Attendance history links ({attendanceLinks}) are preserved; preview does not rewrite attendance.");
        if (timetableCount > 0)
            warnings.Add($"Timetable mappings ({timetableCount}) remain; no schedule duplication is created by preview.");

        string? readinessStatus = null;
        var readinessNotes = new List<string>();
        if (target is not null && errors.Count == 0)
        {
            var ready = await _readiness.EvaluateAsync(target.Id, ct);
            readinessStatus = ready.OverallStatus;
            readinessNotes.AddRange(ready.Checks.Select(c => $"{c.Area}:{c.Status} — {c.Message}"));
            _ = await _capacity.GetOccupancyAsync(target.Id, ct);
        }

        return new MergePreviewEngineDto
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            SourceSectionIds = sources,
            TargetSectionId = targetSectionId,
            CurrentCapacityTotal = currentCap,
            MergedCapacity = mergedCap,
            CombinedStudentCount = studentCount,
            CombinedFacultyCount = facultyCount,
            TimetableMappingCount = timetableCount,
            AttendanceSessionLinkCount = attendanceLinks,
            TargetReadiness = readinessStatus,
            ReadinessNotes = readinessNotes,
        };
    }
}

using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>Read-only split preview engine — no writes, no lifecycle transitions.</summary>
public sealed class SplitPreviewService : ISplitPreviewService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionPolicyService _policies;
    private readonly IAcademicTelemetryService _telemetry;

    public SplitPreviewService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionPolicyService policies,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _policies = policies;
        _telemetry = telemetry;
    }

    public Task<SplitPreviewEngineDto> PreviewAsync(
        int sourceSectionId,
        int childCount = 2,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.SectionSplitPreview,
            "SplitPreview.Preview",
            ct => BuildAsync(sourceSectionId, childCount, ct),
            cancellationToken);

    private async Task<SplitPreviewEngineDto> BuildAsync(int sourceSectionId, int childCount, CancellationToken ct)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var n = childCount < 2 ? 2 : Math.Min(childCount, 10);

        var source = await _db.Sections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sourceSectionId && s.TenantId == _currentUser.TenantId, ct);
        if (source is null)
        {
            return new SplitPreviewEngineDto
            {
                IsValid = false,
                Errors = ["Source section not found."],
                SourceSectionId = sourceSectionId,
            };
        }

        var status = SectionLifecycleStates.Normalize(source.Status);
        if (status is SectionLifecycleStates.Merged or SectionLifecycleStates.Split or SectionLifecycleStates.Archived or SectionLifecycleStates.Closed)
            errors.Add($"Section {source.SectionCode} cannot be split (status {status}).");

        var policy = await _policies.ResolveForSectionAsync(source.Id, ct);
        if (!policy.AllowSplit) errors.Add("Resolved section policy disallows split.");
        warnings.AddRange(policy.Warnings);

        var students = await _db.StudentSections.CountAsync(
            x => x.SectionId == source.Id && x.IsCurrent, ct);
        var faculty = await _db.FacultySectionAssignments.CountAsync(
            x => x.SectionId == source.Id && x.IsCurrent, ct);
        var timetable = await _db.TimetableSections.CountAsync(
            x => x.SectionId == source.Id && x.TenantId == _currentUser.TenantId, ct);

        if (timetable > 0)
            warnings.Add("Existing timetable mappings stay on the source until scheduling remaps children (preview only).");
        warnings.Add("Student redistribution is not executed by preview (AI29.1C allocation).");

        var per = n == 0 ? 0 : students / n;
        var rem = n == 0 ? 0 : students % n;
        var capEach = Math.Max(1, (int)Math.Ceiling(source.MaximumStrength / (double)n));
        var children = new List<SplitPreviewChildDto>();
        for (var i = 0; i < n; i++)
        {
            children.Add(new SplitPreviewChildDto
            {
                ProposedCode = $"{source.SectionCode}-{(char)('A' + i)}",
                ProposedName = $"{source.SectionName} ({(char)('A' + i)})",
                ProposedCapacity = capEach,
                ExpectedStudentCount = per + (i < rem ? 1 : 0),
                FacultyImpact = faculty == 0
                    ? "No faculty currently assigned on source"
                    : $"{faculty} faculty remain on source until reassignment",
                RoomImpact = timetable == 0
                    ? "No timetable/room linkage detected"
                    : "Room/slot remapping required after commit (not performed by preview)",
            });
        }

        return new SplitPreviewEngineDto
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            SourceSectionId = source.Id,
            SourceSectionCode = source.SectionCode,
            SourceStudentCount = students,
            SourceCapacity = source.MaximumStrength,
            SourceFacultyCount = faculty,
            TimetableMappingCount = timetable,
            ProposedChildren = children,
        };
    }
}

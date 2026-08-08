using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>Advisory readiness evaluator — never mutates operational data.</summary>
public sealed class SectionReadinessService : ISectionReadinessService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionCapacityEngine _capacity;

    public SectionReadinessService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionCapacityEngine capacity)
    {
        _db = db;
        _currentUser = currentUser;
        _capacity = capacity;
    }

    public async Task<SectionReadinessDto> EvaluateAsync(int sectionId, CancellationToken cancellationToken = default)
    {
        var section = await _db.Sections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        var checks = new List<SectionReadinessCheckDto>();
        var snap = await _capacity.GetOccupancyAsync(sectionId, cancellationToken);

        checks.Add(new SectionReadinessCheckDto
        {
            Area = "Capacity",
            Status = snap.IsHardLimitBreached ? "Blocked" : snap.HasWarning ? "Warning" : "Ready",
            Message = snap.HasWarning ? string.Join(" ", snap.Warnings) : $"Occupancy {snap.OccupancyPercent}%.",
        });

        var facultyCount = await _db.FacultySectionAssignments.CountAsync(
            x => x.SectionId == sectionId && x.IsCurrent && x.TenantId == _currentUser.TenantId, cancellationToken);
        checks.Add(new SectionReadinessCheckDto
        {
            Area = "Faculty",
            Status = facultyCount == 0 ? "Blocked" : "Ready",
            Message = facultyCount == 0 ? "No current faculty assignment." : $"{facultyCount} faculty assigned.",
        });

        var hasTimetable = await _db.TimetableSections.AnyAsync(
            x => x.SectionId == sectionId && x.TenantId == _currentUser.TenantId, cancellationToken);
        checks.Add(new SectionReadinessCheckDto
        {
            Area = "Timetable",
            Status = hasTimetable ? "Ready" : "Warning",
            Message = hasTimetable ? "Section mapped to at least one timetable entry." : "No timetable mapping (optional for non-timetable attendance).",
        });

        // Room readiness is advisory via timetable presence only (no scheduling engine changes).
        checks.Add(new SectionReadinessCheckDto
        {
            Area = "Room",
            Status = hasTimetable ? "Ready" : "Warning",
            Message = hasTimetable
                ? "Room/slot implied by timetable mapping (not modified by readiness)."
                : "No room/slot linkage detected via timetable sections.",
        });

        var subjectCount = await _db.Subjects.CountAsync(
            s => s.TenantId == _currentUser.TenantId
                 && s.CourseId == section.CourseId
                 && s.SemesterId == section.SemesterId, cancellationToken);
        checks.Add(new SectionReadinessCheckDto
        {
            Area = "Subjects",
            Status = subjectCount == 0 ? "Warning" : "Ready",
            Message = subjectCount == 0
                ? "No subjects found for course/semester scope (Subject Master unchanged)."
                : $"{subjectCount} subjects in scope.",
        });

        checks.Add(new SectionReadinessCheckDto
        {
            Area = "Students",
            Status = snap.CurrentStrength == 0 ? "Warning" : "Ready",
            Message = snap.CurrentStrength == 0 ? "No students currently allocated." : $"{snap.CurrentStrength} students allocated.",
        });

        var lifecycle = SectionLifecycleStates.Normalize(section.Status);
        checks.Add(new SectionReadinessCheckDto
        {
            Area = "Lifecycle",
            Status = lifecycle is SectionLifecycleStates.Active or SectionLifecycleStates.Open or SectionLifecycleStates.Locked
                ? "Ready"
                : lifecycle is SectionLifecycleStates.Merged or SectionLifecycleStates.Split or SectionLifecycleStates.Archived
                    ? "Blocked"
                    : "Warning",
            Message = $"Lifecycle status: {lifecycle}.",
        });

        var overall = checks.Any(c => c.Status == "Blocked") ? "Blocked"
            : checks.Any(c => c.Status == "Warning") ? "Warning"
            : "Ready";

        return new SectionReadinessDto
        {
            SectionId = section.Id,
            SectionCode = section.SectionCode,
            SectionName = section.SectionName,
            OverallStatus = overall,
            Checks = checks,
        };
    }

    public async Task<IReadOnlyList<SectionReadinessDto>> EvaluateManyAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Sections.AsNoTracking().Where(s => s.TenantId == _currentUser.TenantId);
        if (academicYearId is > 0) q = q.Where(s => s.AcademicYearId == academicYearId);
        if (semesterId is > 0) q = q.Where(s => s.SemesterId == semesterId);
        var ids = await q.Select(s => s.Id).ToListAsync(cancellationToken);
        var result = new List<SectionReadinessDto>();
        foreach (var id in ids)
            result.Add(await EvaluateAsync(id, cancellationToken));
        return result;
    }

    public Task<IReadOnlyList<SectionReadinessDto>> GetSectionHealthAsync(CancellationToken cancellationToken = default)
        => EvaluateManyAsync(cancellationToken: cancellationToken);
}

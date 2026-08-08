using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionHealthService : ISectionHealthService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionCapacityEngine _capacity;
    private readonly ISectionReadinessService _readiness;
    private readonly IAcademicTelemetryService _telemetry;

    public SectionHealthService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionCapacityEngine capacity,
        ISectionReadinessService readiness,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _capacity = capacity;
        _readiness = readiness;
        _telemetry = telemetry;
    }

    public Task<SectionHealthReportDto> EvaluateAsync(int sectionId, CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.SectionHealth,
            "SectionHealth.Evaluate",
            ct => EvaluateCoreAsync(sectionId, ct),
            cancellationToken);

    public async Task<IReadOnlyList<SectionHealthReportDto>> EvaluateManyAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Sections.AsNoTracking().Where(s => s.TenantId == _currentUser.TenantId);
        if (academicYearId is > 0) q = q.Where(s => s.AcademicYearId == academicYearId);
        if (semesterId is > 0) q = q.Where(s => s.SemesterId == semesterId);
        var ids = await q.Select(s => s.Id).ToListAsync(cancellationToken);
        var result = new List<SectionHealthReportDto>();
        foreach (var id in ids)
            result.Add(await EvaluateAsync(id, cancellationToken));
        return result;
    }

    private async Task<SectionHealthReportDto> EvaluateCoreAsync(int sectionId, CancellationToken ct)
    {
        var section = await _db.Sections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.TenantId == _currentUser.TenantId, ct)
            ?? throw new KeyNotFoundException("Section not found.");

        var dims = new List<SectionHealthDimensionDto>();
        var snap = await _capacity.GetOccupancyAsync(sectionId, ct);
        dims.Add(new SectionHealthDimensionDto
        {
            Area = "Capacity",
            Status = snap.IsHardLimitBreached ? "Critical" : snap.HasWarning ? "Warning" : "Healthy",
            Message = snap.HasWarning ? string.Join(" ", snap.Warnings) : $"Occupancy {snap.OccupancyPercent}%.",
        });

        var life = SectionLifecycleStates.Normalize(section.Status);
        dims.Add(new SectionHealthDimensionDto
        {
            Area = "Lifecycle",
            Status = life is SectionLifecycleStates.Merged or SectionLifecycleStates.Split or SectionLifecycleStates.Archived
                ? "Critical"
                : life is SectionLifecycleStates.Active or SectionLifecycleStates.Open or SectionLifecycleStates.Locked
                    ? "Healthy"
                    : "Warning",
            Message = $"Status {life}.",
        });

        var ready = await _readiness.EvaluateAsync(sectionId, ct);
        dims.Add(new SectionHealthDimensionDto
        {
            Area = "Readiness",
            Status = ready.OverallStatus switch
            {
                "Blocked" => "Critical",
                "Warning" => "Warning",
                _ => "Healthy"
            },
            Message = string.Join("; ", ready.Checks.Select(c => $"{c.Area}:{c.Status}")),
        });

        var faculty = await _db.FacultySectionAssignments.CountAsync(
            x => x.SectionId == sectionId && x.IsCurrent, ct);
        dims.Add(new SectionHealthDimensionDto
        {
            Area = "Faculty",
            Status = faculty == 0 ? "Critical" : "Healthy",
            Message = faculty == 0 ? "No faculty assigned." : $"{faculty} faculty assigned.",
        });

        dims.Add(new SectionHealthDimensionDto
        {
            Area = "Students",
            Status = snap.CurrentStrength == 0 ? "Warning" : "Healthy",
            Message = $"{snap.CurrentStrength} students.",
        });

        var tt = await _db.TimetableSections.AnyAsync(
            x => x.SectionId == sectionId && x.TenantId == _currentUser.TenantId, ct);
        dims.Add(new SectionHealthDimensionDto
        {
            Area = "Timetable",
            Status = tt ? "Healthy" : "Warning",
            Message = tt ? "Timetable mapping present." : "No timetable mapping.",
        });
        dims.Add(new SectionHealthDimensionDto
        {
            Area = "Room",
            Status = tt ? "Healthy" : "Warning",
            Message = tt ? "Room/slot implied by timetable." : "No room linkage via timetable.",
        });

        var overall = dims.Any(d => d.Status == "Critical") ? "Critical"
            : dims.Any(d => d.Status == "Warning") ? "Warning"
            : "Healthy";

        return new SectionHealthReportDto
        {
            SectionId = section.Id,
            SectionCode = section.SectionCode,
            OverallStatus = overall,
            Dimensions = dims,
        };
    }
}

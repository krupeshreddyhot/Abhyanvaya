using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public sealed class AllocationReadinessService : IAllocationReadinessService
{
    private readonly ISectionAllocationContextBuilder _builder;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicTelemetryService _telemetry;

    public AllocationReadinessService(
        ISectionAllocationContextBuilder builder,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAcademicTelemetryService telemetry)
    {
        _builder = builder;
        _db = db;
        _currentUser = currentUser;
        _telemetry = telemetry;
    }

    public Task<AllocationReadinessReport> EvaluateAsync(
        AllocationScopeRequest scope,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.AllocationReadiness,
            "AllocationReadiness.Evaluate",
            ct => EvaluateCoreAsync(scope, ct),
            cancellationToken);

    private async Task<AllocationReadinessReport> EvaluateCoreAsync(AllocationScopeRequest scope, CancellationToken ct)
    {
        var ctx = await _builder.BuildAsync(scope, ct);
        var checks = new List<AllocationReadinessCheck>
        {
            Check("Hierarchy", ctx.Hierarchy.CourseId > 0 && ctx.Hierarchy.SemesterId > 0,
                "Hierarchy scope resolved.", "Hierarchy incomplete."),
            Check("Sections", ctx.Sections.Count > 0, $"{ctx.Sections.Count} sections.", "Missing sections."),
            Check("Capacity", ctx.Capacities.All(c => !string.Equals(c.CapacityStatus, "OverCapacity", StringComparison.OrdinalIgnoreCase)),
                "Capacity acceptable.", "Over-capacity sections present.", warningOnly: true),
            Check("Policies", ctx.Policies.Count > 0 || ctx.Sections.Count == 0,
                "Policies resolved.", "No policy summary."),
            Check("Faculty", ctx.FacultyAssignments.Count > 0 || ctx.Sections.Count == 0,
                $"{ctx.FacultyAssignments.Count} faculty links.", "No faculty assignments.", warningOnly: true),
            Check("Subjects", ctx.SubjectAssignments.Count > 0,
                $"{ctx.SubjectAssignments.Count} subjects.", "No subjects in scope.", warningOnly: true),
            Check("Students", ctx.Students.Count > 0,
                $"{ctx.Students.Count} students.", "No students in scope.", warningOnly: true),
            Check("Rooms", ctx.TimetableStatus != "Unknown",
                $"Timetable/rooms: {ctx.TimetableStatus}.", "Room/timetable status unknown.", warningOnly: true),
            Check("Timetable", ctx.TimetableStatus == "Mapped",
                "Timetable mappings present.", "No timetable mappings.", warningOnly: true),
            Check("Lifecycle", ctx.Sections.All(s => s.Lifecycle is not ("Merged" or "Split" or "Archived")),
                "Lifecycle eligible.", "Inactive/terminal lifecycle sections present."),
        };

        // Confirm academic year exists
        var yearOk = await _db.SchedulingAcademicYears.AsNoTracking()
            .AnyAsync(y => y.Id == scope.AcademicYearId && y.TenantId == _currentUser.TenantId, ct);
        checks.Insert(0, Check("AcademicYear", yearOk, "Academic year valid.", "Academic year missing."));

        var overall = checks.Any(c => c.Status == "Blocked") ? "Blocked"
            : checks.Any(c => c.Status == "Warning") ? "Warning"
            : "Ready";

        return new AllocationReadinessReport { OverallStatus = overall, Checks = checks };
    }

    private static AllocationReadinessCheck Check(string area, bool ok, string okMsg, string badMsg, bool warningOnly = false)
        => new()
        {
            Area = area,
            Status = ok ? "Ready" : warningOnly ? "Warning" : "Blocked",
            Message = ok ? okMsg : badMsg,
        };
}

using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

/// <summary>Evaluates who/what is affected by a conflict or proposed manual change. Never edits timetables.</summary>
public sealed class ImpactAnalyzer : IImpactAnalyzer
{
    private readonly IApplicationDbContext _db;

    public ImpactAnalyzer(IApplicationDbContext db) => _db = db;

    public Task<ImpactGraph> AnalyzeAsync(
        ConflictResult conflict,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default) =>
        AnalyzeProposedChangeAsync(conflict, null, context, cancellationToken);

    public async Task<ImpactGraph> AnalyzeProposedChangeAsync(
        ConflictResult conflict,
        ResolutionOption? proposedOption,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        var nodes = new List<ImpactNode>();
        var edges = new List<ImpactEdge>();
        var rootId = $"conflict:{conflict.RuleCode}:{conflict.TimetableEntryId}";

        nodes.Add(new ImpactNode
        {
            NodeId = rootId,
            Category = ImpactCategory.Other,
            Label = conflict.RuleName,
            Severity = conflict.Severity,
            Detail = conflict.Description,
            EntityId = conflict.TimetableEntryId
        });

        if (conflict.StaffId.HasValue)
        {
            var id = $"faculty:{conflict.StaffId}";
            var name = context.StaffNames.GetValueOrDefault(conflict.StaffId.Value, $"Staff {conflict.StaffId}");
            nodes.Add(new ImpactNode
            {
                NodeId = id,
                Category = ImpactCategory.Faculty,
                Label = name,
                EntityId = conflict.StaffId,
                Severity = conflict.Severity,
                Detail = "Faculty workload / availability may need manual review."
            });
            edges.Add(new ImpactEdge { FromNodeId = rootId, ToNodeId = id, Relation = "affects" });

            nodes.Add(new ImpactNode
            {
                NodeId = $"workload:{conflict.StaffId}",
                Category = ImpactCategory.Workload,
                Label = $"{name} workload",
                EntityId = conflict.StaffId,
                Detail = $"Daily entries for staff: {context.Entries.Count(e => e.StaffId == conflict.StaffId && e.DayOfWeek == conflict.DayOfWeek)}"
            });
            edges.Add(new ImpactEdge { FromNodeId = id, ToNodeId = $"workload:{conflict.StaffId}", Relation = "impacts" });

            var availHits = context.FacultyAvailabilities.Count(a => a.StaffId == conflict.StaffId.Value);
            if (availHits > 0)
            {
                nodes.Add(new ImpactNode
                {
                    NodeId = $"availability:{conflict.StaffId}",
                    Category = ImpactCategory.Availability,
                    Label = $"{name} availability records",
                    EntityId = conflict.StaffId,
                    Detail = $"{availHits} availability record(s) in context"
                });
                edges.Add(new ImpactEdge { FromNodeId = id, ToNodeId = $"availability:{conflict.StaffId}", Relation = "constrained-by" });
            }
        }

        if (conflict.RoomId.HasValue)
        {
            var roomName = context.Rooms.TryGetValue(conflict.RoomId.Value, out var room) ? room.Name : $"Room {conflict.RoomId}";
            var id = $"room:{conflict.RoomId}";
            nodes.Add(new ImpactNode
            {
                NodeId = id,
                Category = ImpactCategory.Room,
                Label = roomName,
                EntityId = conflict.RoomId,
                Severity = conflict.Severity
            });
            edges.Add(new ImpactEdge { FromNodeId = rootId, ToNodeId = id, Relation = "affects" });
        }

        if (conflict.DepartmentId.HasValue)
        {
            var id = $"dept:{conflict.DepartmentId}";
            nodes.Add(new ImpactNode
            {
                NodeId = id,
                Category = ImpactCategory.Department,
                Label = $"Department {conflict.DepartmentId}",
                EntityId = conflict.DepartmentId
            });
            edges.Add(new ImpactEdge { FromNodeId = rootId, ToNodeId = id, Relation = "affects" });
        }

        if (conflict.GroupId.HasValue || conflict.SemesterId.HasValue)
        {
            var id = $"students:{conflict.GroupId ?? 0}:{conflict.SemesterId ?? 0}";
            nodes.Add(new ImpactNode
            {
                NodeId = id,
                Category = ImpactCategory.Students,
                Label = $"Group {conflict.GroupId} / Semester {conflict.SemesterId}",
                Detail = "Student cohort may experience schedule change if resolved manually."
            });
            edges.Add(new ImpactEdge { FromNodeId = rootId, ToNodeId = id, Relation = "affects" });
        }

        var publishedCount = 0;
        if (conflict.TimetableId.HasValue)
        {
            var timetable = await _db.SchedulingTimetables.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == conflict.TimetableId.Value && t.TenantId == context.TenantId, cancellationToken);
            if (timetable?.ScheduleVersionId is int versionId)
            {
                publishedCount = await _db.SchedulingScheduleVersions.CountAsync(v =>
                    v.Id == versionId &&
                    v.TenantId == context.TenantId &&
                    !v.IsDeleted &&
                    (v.Status == ScheduleVersionStatus.Published || v.Status == ScheduleVersionStatus.Approved),
                    cancellationToken);
            }

            if (publishedCount > 0 || timetable?.Status == TimetableStatus.Published)
            {
                publishedCount = Math.Max(publishedCount, timetable?.Status == TimetableStatus.Published ? 1 : 0);
                var id = $"published:{conflict.TimetableId}";
                nodes.Add(new ImpactNode
                {
                    NodeId = id,
                    Category = ImpactCategory.PublishedVersion,
                    Label = $"Published/versioned schedules ({publishedCount})",
                    EntityId = conflict.TimetableId,
                    Severity = ConflictSeverity.Warning,
                    Detail = "Manual edits may require republication / governance review."
                });
                edges.Add(new ImpactEdge { FromNodeId = rootId, ToNodeId = id, Relation = "risks" });
            }
        }

        nodes.Add(new ImpactNode
        {
            NodeId = $"attendance:{conflict.TimetableEntryId}",
            Category = ImpactCategory.Attendance,
            Label = "Attendance compatibility",
            Detail = "Legacy Course→Group→Semester→Subject→Period flow remains intact. Timetable-assisted attendance remains optional.",
            Severity = ConflictSeverity.Information
        });
        edges.Add(new ImpactEdge { FromNodeId = rootId, ToNodeId = $"attendance:{conflict.TimetableEntryId}", Relation = "considers" });

        if (proposedOption is not null)
        {
            var optId = $"option:{proposedOption.OptionCode}";
            nodes.Add(new ImpactNode
            {
                NodeId = optId,
                Category = ImpactCategory.Other,
                Label = proposedOption.Label,
                Detail = proposedOption.Description + " (proposed — not applied)"
            });
            edges.Add(new ImpactEdge { FromNodeId = rootId, ToNodeId = optId, Relation = "proposes" });
        }

        var summary = new ImpactSummary
        {
            FacultyAffected = nodes.Count(n => n.Category == ImpactCategory.Faculty),
            StudentsAffected = nodes.Count(n => n.Category == ImpactCategory.Students),
            RoomsAffected = nodes.Count(n => n.Category == ImpactCategory.Room),
            DepartmentsAffected = nodes.Count(n => n.Category == ImpactCategory.Department),
            PublishedVersionsAffected = publishedCount,
            WorkloadSignals = nodes.Count(n => n.Category == ImpactCategory.Workload),
            AvailabilitySignals = nodes.Count(n => n.Category == ImpactCategory.Availability),
            AttendanceSignals = nodes.Count(n => n.Category == ImpactCategory.Attendance),
            MaxSeverity = nodes.Max(n => n.Severity),
            RiskLevel = conflict.Severity >= ConflictSeverity.Error ? "High"
                : conflict.Severity == ConflictSeverity.Warning ? "Medium" : "Low"
        };

        return new ImpactGraph
        {
            Summary = summary,
            Nodes = nodes,
            Edges = edges,
            NavigationPath = conflict.Recommendation.NavigationPath
        };
    }
}

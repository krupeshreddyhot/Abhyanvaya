namespace Abhyanvaya.Application.Scheduling.Configuration;

/// <summary>
/// AI30 Phase 3.5 — dependency metadata for guided configuration (service-driven; UI must not hardcode next steps).
/// Paths match existing routes — never rename.
/// </summary>
public static class SchedulingModuleCatalog
{
    public sealed record ModuleDef(
        string Key,
        string Title,
        string Path,
        string SectionKey,
        bool RequiredForMinimum,
        string[] Requires,
        string[] UsedBy,
        string[] Related,
        string HelpDocPath);

    public static readonly IReadOnlyList<ModuleDef> Modules =
    [
        new("academic-years", "Academic Years", "/setup/scheduling/academic-years", "academic-calendar", true, [], ["working-days", "holidays", "schedule-versions"], ["working-days"], "/docs/scheduling/modules/academic-years.md"),
        new("working-days", "Working Days", "/setup/scheduling/working-days", "academic-calendar", true, ["academic-years"], ["time-slots", "timetable-designer"], ["academic-years"], "/docs/scheduling/modules/working-days.md"),
        new("holidays", "Holiday Calendar", "/setup/scheduling/holidays", "academic-calendar", false, ["academic-years"], ["timetable-designer"], ["holiday-types"], "/docs/scheduling/modules/holidays.md"),
        new("holiday-types", "Holiday Types", "/setup/scheduling/holiday-types", "academic-calendar", false, [], ["holidays"], ["holidays"], "/docs/scheduling/modules/holiday-types.md"),

        new("campuses", "Campus Facilities", "/setup/scheduling/campuses", "infrastructure", true, [], ["rooms"], ["rooms"], "/docs/scheduling/modules/campuses.md"),
        new("rooms", "Rooms", "/setup/scheduling/rooms", "infrastructure", true, ["campuses"], ["room-availability", "room-rules", "timetable-designer"], ["room-features"], "/docs/scheduling/modules/rooms.md"),
        new("room-features", "Room Features", "/setup/scheduling/room-features", "infrastructure", false, ["rooms"], ["optimization-preview"], ["rooms"], "/docs/scheduling/modules/room-features.md"),
        new("room-availability", "Room Availability", "/setup/scheduling/room-availability", "infrastructure", false, ["rooms"], ["conflict-dashboard", "timetable-designer"], ["rooms"], "/docs/scheduling/modules/room-availability.md"),

        new("time-slots", "Time Slots", "/setup/scheduling/time-slots", "framework", true, ["working-days"], ["subject-allocations", "timetable-designer"], ["time-slot-templates"], "/docs/scheduling/modules/time-slots.md"),
        new("time-slot-templates", "Time Slot Templates", "/setup/scheduling/time-slot-templates", "framework", false, [], ["time-slots"], ["time-slots"], "/docs/scheduling/modules/time-slot-templates.md"),
        new("subject-categories", "Subject Categories", "/setup/scheduling/subject-categories", "framework", false, [], ["subject-allocations"], ["subject-delivery"], "/docs/scheduling/modules/subject-categories.md"),
        new("subject-delivery", "Subject Delivery", "/setup/scheduling/subject-delivery", "framework", false, [], ["timetable-designer"], ["subject-categories"], "/docs/scheduling/modules/subject-delivery.md"),
        new("room-rules", "Room Rules", "/setup/scheduling/room-rules", "framework", false, ["rooms"], ["timetable-designer", "optimization-preview"], ["rooms"], "/docs/scheduling/modules/room-rules.md"),

        new("faculty-availability", "Faculty Availability", "/setup/scheduling/faculty-availability", "faculty-planning", false, [], ["timetable-designer", "conflict-dashboard"], ["faculty-preferences"], "/docs/scheduling/modules/faculty-availability.md"),
        new("faculty-preferences", "Faculty Preferences", "/setup/scheduling/faculty-preferences", "faculty-planning", false, [], ["optimization-preview"], ["faculty-workloads"], "/docs/scheduling/modules/faculty-preferences.md"),
        new("faculty-workloads", "Faculty Workloads", "/setup/scheduling/faculty-workloads", "faculty-planning", false, [], ["subject-allocations", "optimization-preview"], ["faculty-preferences"], "/docs/scheduling/modules/faculty-workloads.md"),
        new("subject-allocations", "Subject Allocation", "/setup/scheduling/subject-allocations", "faculty-planning", true, ["time-slots"], ["timetable-designer", "conflict-dashboard", "optimization-preview"], ["faculty-workloads"], "/docs/scheduling/modules/subject-allocations.md"),

        new("schedule-versions", "Schedule Versions", "/setup/scheduling/governance/versions", "timetable", true, ["academic-years"], ["timetable-designer", "publishing"], ["governance-dashboard"], "/docs/scheduling/modules/schedule-versions.md"),
        new("timetable-designer", "Timetable Designer", "/setup/scheduling/timetables", "timetable", true, ["subject-allocations", "time-slots", "rooms", "schedule-versions"], ["publishing", "conflict-dashboard", "optimization-preview"], ["faculty-timetable"], "/docs/scheduling/modules/timetable-designer.md"),
        new("faculty-timetable", "Faculty Timetable", "/setup/scheduling/timetable-faculty", "timetable", false, ["timetable-designer"], [], ["student-timetable"], "/docs/scheduling/modules/faculty-timetable.md"),
        new("student-timetable", "Student Timetable", "/setup/scheduling/timetable-student", "timetable", false, ["timetable-designer"], [], ["room-timetable"], "/docs/scheduling/modules/student-timetable.md"),
        new("room-timetable", "Room Timetable", "/setup/scheduling/timetable-room", "timetable", false, ["timetable-designer"], [], ["faculty-timetable"], "/docs/scheduling/modules/room-timetable.md"),

        new("approval-queue", "Approval Queue", "/setup/scheduling/governance/approvals", "governance", false, ["timetable-designer"], ["publishing"], ["governance-dashboard"], "/docs/scheduling/modules/approval-queue.md"),
        new("publishing", "Publishing", "/setup/scheduling/governance/publishing", "governance", false, ["timetable-designer"], [], ["schedule-versions"], "/docs/scheduling/modules/publishing.md"),
        new("clone-wizard", "Clone Wizard", "/setup/scheduling/governance/clone", "governance", false, ["timetable-designer"], [], ["change-history"], "/docs/scheduling/modules/clone-wizard.md"),
        new("change-history", "Change History", "/setup/scheduling/governance/history", "governance", false, ["timetable-designer"], [], ["governance-dashboard"], "/docs/scheduling/modules/change-history.md"),
        new("governance-dashboard", "Governance Dashboard", "/setup/scheduling/governance/dashboard", "governance", false, [], [], ["approval-queue"], "/docs/scheduling/modules/governance-dashboard.md"),

        new("conflict-dashboard", "Conflict Dashboard", "/setup/scheduling/conflicts/dashboard", "validation", false, ["timetable-designer"], ["optimization-preview"], ["conflict-workspace"], "/docs/scheduling/modules/conflict-dashboard.md"),
        new("conflict-workspace", "Conflict Workspace", "/setup/scheduling/conflicts/workspace", "validation", false, ["timetable-designer"], [], ["conflict-analytics"], "/docs/scheduling/modules/conflict-workspace.md"),
        new("conflict-analytics", "Conflict Analytics", "/setup/scheduling/conflicts/analytics", "validation", false, ["conflict-dashboard"], [], ["conflict-rules"], "/docs/scheduling/modules/conflict-analytics.md"),
        new("conflict-rules", "Conflict Rule Thresholds", "/setup/scheduling/conflicts/rules", "validation", false, [], ["conflict-dashboard"], ["conflict-workspace"], "/docs/scheduling/modules/conflict-rules.md"),

        new("optimization-preview", "Optimization Preview", "/setup/scheduling/optimization/preview", "optimization", false, ["timetable-designer", "subject-allocations"], ["optimization-workspace"], ["optimization-dashboard"], "/docs/scheduling/modules/optimization-preview.md"),
        new("optimization-workspace", "Optimization Workspace", "/setup/scheduling/optimization/workspace", "optimization", false, ["optimization-preview"], ["optimization-dashboard"], ["optimization-preview"], "/docs/scheduling/modules/optimization-workspace.md"),
        new("optimization-dashboard", "Optimization Dashboard", "/setup/scheduling/optimization/dashboard", "optimization", false, ["optimization-preview"], [], ["optimization-workspace"], "/docs/scheduling/modules/optimization-dashboard.md"),
    ];

    /// <summary>Ordered minimum configuration path for next-step engine.</summary>
    public static readonly string[] MinimumPathOrder =
    [
        "academic-years",
        "working-days",
        "campuses",
        "rooms",
        "time-slots",
        "subject-allocations",
        "schedule-versions",
        "timetable-designer",
        "conflict-dashboard",
        "optimization-preview"
    ];
}

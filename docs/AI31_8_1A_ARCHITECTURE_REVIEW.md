# AI31.8.1A — Architecture Review

## Verdict

**Compliant** with ADL intent and AI31.8 / AI31.8.1 constraints: presentation-only refinement of the Admin Enterprise Dashboard. No engine, API, or attendance workflow changes.

## Constraints Checked

| Constraint | Result |
|------------|--------|
| Must not modify AttendanceSessionResolver | Not touched |
| Must not modify Attendance Controllers / APIs | Not touched |
| Must not modify Scheduling / Conflict / Optimization / AI / Timetable / Recovery engines | Not touched |
| Must not redesign DB or APIs | Not touched |
| Must reuse existing dashboard APIs, SignalR, permissions, KPI/export services | Reused |
| Faculty without timetable: Course→Group→Semester→Subject→Period | Unchanged |
| Faculty with timetable: Today's Timetable→Attendance | Unchanged |

## Composition Boundaries

- **Server:** `EnterpriseDashboardExcellenceService` and `OperationsCommandCenterService` remain the composition sources.
- **Client:** Filters hero KPIs, sorts attention cards, remaps timeline presentation, and regroups quick actions — all without new endpoints.
- **AttendanceSessionResolver** remains the sole attendance-mode selector (out of scope).

## Decision Notes

1. **Hero filter is client-side** (`HERO_SUMMARY_CODES`) so backend card lists stay complete for export/help and non-hero cards relocate to Institutional KPIs.
2. **Severity sort is client-side** (`severityRank`) reusing existing widget `status` values — no new priority service.
3. **Sticky toolbar** is CSS `position: sticky` only; refresh/filter/export/preferences behavior unchanged.
4. **Timeline stages** are a presentation overlay; period data still comes from `academicTimeline`.

## Risks / Residual

- Screenshots for before/after were not captured in-repo; validation is layout/build based.
- Tablet toolbar collapse uses `md` breakpoint; verify on target devices during UAT.

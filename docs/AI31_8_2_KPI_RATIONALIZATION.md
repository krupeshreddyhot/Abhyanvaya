# AI31.8.2 — KPI Rationalization

## Removed from Executive Summary (static / identity / configuration)

| Code | Former Title | Disposition | Rationale |
|------|--------------|-------------|-----------|
| `exec-college` | College Name | Executive Context Header | Identity, not operational |
| `exec-academic-year` | Academic Year | Executive Context Header | Configuration / catalog context |
| `exec-semester` | Current Semester | Context (available on details / scheduling) | Static term label |
| `exec-working-day` | Current Working Day | Context Date (weekday implied) | Calendar metadata |
| `exec-date` | Today's Date | Executive Context Header (Date) | Clock/calendar, not KPI |
| `exec-faculty` | Faculty Available Today | Lower sections / Staff module | Low-signal catalog count |
| `exec-students` | Active Students | Students module / Reports | Placeholder / non-live |

Backend excellence payload may still return these cards for export/help; the UI no longer renders them as Executive Summary KPI cards.

## Operational Executive Summary KPIs (kept / composed)

Priority order:

| # | Code | Title | Source composition |
|---|------|-------|--------------------|
| 1 | `exec-critical-alerts` | Critical Alerts | `exec-alerts` / attention alerts |
| 2 | `exec-classes-running` | Classes Running | `classes-running-now` / `sessions-running` |
| 3 | `exec-attendance-completion` | Attendance Completion | `attendance-completion-today` / `exec-attendance` |
| 4 | `exec-pending-reviews` | Pending Reviews | `attendance-review-queue` / `ai-recognition-queue` |
| 5 | `exec-faculty-teaching` | Faculty Teaching | `faculty-teaching-now` |
| 6 | `exec-scheduled-classes` | Scheduled Classes | `exec-classes` |
| 7 | `exec-platform-health` | Platform Health | `exec-health` |
| 8 | `exec-attendance-recorded` | Attendance Recorded | `completed-today` |

## Low-value KPIs retained in lower sections

Examples that remain under Today's / Attendance / Timetable / Academic sections (not Executive Summary):

- Current Time, Current Academic Period (→ Context header clocks/timeline)
- Remaining Classes Today, Rooms Occupied
- Recognition success %, Average Processing Time, SLA
- Active Academic Year / Draft Versions (Timetable Operations)
- Catalog resource counts (Academic Resources)

## No information loss

Identity and filters remain visible in the Context Header and Active Filter panel. Detailed operational KPIs remain in their domain sections.

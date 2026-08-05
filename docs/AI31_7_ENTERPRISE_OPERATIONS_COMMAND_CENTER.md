# AI31.7 — Enterprise Operations Command Center

## Architecture

Composition-only Admin morning dashboard:

```
Existing Services (AI22 / AI22.8 / AI30 / AI31.6)
        ↓
OperationsCommandCenterService
        ↓
EnterpriseOperationsCommandCenterDto
        ↓
Role-aware Admin UI (/dashboard)
```

- No new repositories
- No Attendance API / Timetable / Scheduling engine changes
- `AttendanceSessionResolver` unchanged
- Legacy Course → Group → Semester → Subject → Period attendance preserved

## Section hierarchy

1. Attention Required  
2. Today's Operations  
3. Scheduling Operations  
4. Attendance Operations  
5. Academic Resources  
6. System Health  
7. Quick Actions  

## Widget hierarchy

Reusable `DashboardWidgetDto` (KPI / Status / Action) with:

- Count / display value  
- Severity (Green / Yellow / Orange / Red)  
- Last updated  
- Tooltip  
- Click path (drill-down)  
- Optional trend  

## Navigation flow

| Card | Destination |
|------|-------------|
| Attendance Review / Recovery / AI Recognition | `/setup/attendance-recovery` |
| Timetable Approval Queue | `/setup/scheduling/governance/approvals` |
| Scheduling Issues | `/setup/scheduling/conflicts/workspace` |
| Optimization Suggestions | `/setup/scheduling/optimization/workspace` |
| Rooms | `/setup/scheduling/rooms` |
| Faculty | `/setup/staff` |
| System Health cards | `/dashboard/health` |

## Role visibility matrix

| Role | Command Center |
|------|----------------|
| Admin / SuperAdmin | Full Command Center (`AdminOnly` API) |
| Faculty | Faculty Command Center (AI31.6) — not this surface |
| Student / Parent | Future |

Quick Actions filtered by JWT permission claims in UI.

## Service reuse

| Section | Services |
|---------|----------|
| Attention | Recovery dashboard, Governance dashboard, Conflicts, Health alerts, Notifications |
| Today | Recovery, Operational analytics, Timetable dashboard, Scheduling dashboard |
| Scheduling | Scheduling dashboard, Timetable, Governance, Conflicts, Readiness |
| Attendance | Recovery, Operational analytics, Enterprise ops |
| Academic | Scheduling dashboard counts → Catalog routes |
| Health | `IEnterpriseHealthCenterService` (AI31.6) |
| Quick Actions | Permission keys only (no business logic) |

## Business terminology mapping

| Old (dev) | New (business) |
|-----------|----------------|
| Pending Recovery | Attendance Recovery Queue |
| Conflict Count | Scheduling Issues |
| Recognition Queue | AI Attendance Recognition Queue |
| Optimization Queue | Optimization Suggestions |
| Approval Queue | Timetable Approval Queue |
| Students Below Threshold | Students Below Attendance Requirement |
| Platform Health | System Health |

## Legacy / attendance compatibility

Faculty without timetable continue Legacy selectors via unchanged `AttendanceSessionResolver`.  
Faculty with timetable continue Timetable → Attendance.  
No Attendance APIs modified in AI31.7.

## API

`GET /api/enterprise-dashboards/admin/command-center` (AdminOnly)

Legacy `admin/operations` remains for backward compatibility.

## Migration notes

1. Deploy API + UI.  
2. Optional: apply AI31.6 preference schema if not already applied.  
3. Admin `/dashboard` now loads Command Center layout (collapsible sections; collapse state in localStorage).  

## Architecture Review

**Pass** — composition / UX modernization only; ADL-aligned; attendance modes preserved.

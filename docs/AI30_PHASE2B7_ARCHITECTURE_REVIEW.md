# AI30 Phase 2B.7 — Architecture Review

## Verdict

Optimization Sandbox delivered as a **read-only experiment repository**. No optimizer, no AI scheduling, no timetable edits, no attendance API changes.

## Isolation

| Layer | Role |
|-------|------|
| Conflict Engine / Advisor / Impact / Dependency | Unchanged (2B / 2B.5) |
| Optimization Readiness | Unchanged contracts (2B.6) |
| Optimization Sandbox | Scenario store, replay, compare, collaborate |

## Principles enforced

1. Production isolation — sandbox never writes production timetables  
2. Scenario lifecycle separate from timetable governance  
3. Read-only / immutable snapshots after Save  
4. Attendance compatibility via unchanged `AttendanceSessionResolver`  
5. Phase boundary — no algorithms / auto-fix / auto-publish  

## Permissions

Reuses `Scheduling.Conflict.View` / `Manage` for sandbox APIs.

## Audit

`OptimizationScenarioHistory` records Created/Modified/Viewed/Compared/Favorited/Archived/Replayed/Shared/etc.

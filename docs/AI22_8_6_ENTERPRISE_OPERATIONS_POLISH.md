# AI22.8.6 — Enterprise Attendance Operations Polish

## Architecture Review

AI22.8.6 adds **operational visibility only** on top of AI22.8 / AI22.8.5 recovery.

Must not redesign:

- Attendance marking (Legacy or Timetable)
- Recovery workflow
- AI recognition / review engines
- Faculty Workspace pages
- Scheduling engines

`AttendanceSessionResolver` remains the only Legacy vs Timetable selector.  
Attendance APIs / controllers remain unchanged.

```text
Faculty Workspace
        │
        ├── Attendance
        ├── Recovery
        ├── SLA
        ├── Timeline
        ├── Notifications
        └── Analytics
```

Compatibility preserved:

```text
Timetable: Faculty → Today's Timetable → Attendance
Legacy: Faculty → Course → Group → Semester → Subject → Period → Attendance
```

## Implementation Summary

| Prompt | Deliverable |
|--------|-------------|
| 8.6.1 | `AttendanceSlaCalculator` + SLA fields on pending DTOs/cards |
| 8.6.2 | `DepartmentOperationsService` (Catalog Department via StaffDepartment) |
| 8.6.3 | `SessionTimelineService` + `SessionTimeline` UI (retry history + lifecycle) |
| 8.6.4 | `BulkOperationService` + `AttendanceBulkOperationHistory` (admin assist) |
| 8.6.5 | Enterprise ops dashboard widgets (Recharts) |
| 8.6.6 | Faculty workspace summary SLA / priority chips (no new pages) |
| 8.6.7 | SignalR codes: SLA breach, recognition delayed, bulk completed, reminders |
| 8.6.8 | MemoryCache for department/enterprise ops aggregates; paginated timeline |
| 8.6.9 | `AI2286EnterpriseOperationsPolishTests` |
| 8.6.10 | This guide + SLA/Timeline/Department/Bulk/Migration docs |

## SLA Guide

See `docs/AI22_8_6_SLA.md`.

## Timeline Guide

- Endpoint: `GET /api/attendance-recovery/sessions/{id}/timeline` (faculty)  
  and admin twin under `/api/admin/attendance-recovery/sessions/{id}/timeline`
- Sources: session lifecycle timestamps + `AttendanceRetryHistory`
- No second audit table

## Department Dashboard Guide

- Endpoint: `GET /api/admin/attendance-recovery/department-operations`
- Joins `StaffDepartment` → Catalog `Department`
- Cards: pending, completed, failed, recognition running, needs review, averages, faculty count
- Charts reuse Recharts on `/setup/attendance-recovery`

## Bulk Operations Guide

Administrator only (`AdminOnly`):

| Operation | Behavior |
|-----------|----------|
| NotifyFaculty | SignalR `FacultyReminder` |
| ArchiveExpired | Existing admin archive action |
| ExportSessions | Marks rows included (use Export Excel for file) |
| RetryFailedRecognition | Retries **failed** sessions only |
| MarkReviewed | Touch LastActivity / review-ready — **does not finalize** |
| CloseCompleted | Only already-completed sessions |

Never:

- Finalize attendance automatically
- Retry successful sessions
- Override faculty review governance

## Notifications

Reuse `IAttendanceRecoveryNotifier` / FacultyHub. Codes in `AttendanceOpsNotificationCodes`. No polling framework.

## Migration Guide

Live DB (preferred, idempotent):

```powershell
psql ... -f scripts\Apply_AI22_8_6_PolishSchema.sql
```

Creates `AttendanceBulkOperationHistory` and records EF history id  
`20260804090000_AI22_8_6_AttendanceBulkOperationHistory`.

Also ensure AI22.8 / AI22.8.5 schema is applied (`Apply_AI22_8_RecoverySchema.sql`).

## Deployment Checklist

- [ ] Apply AI22.8 recovery schema
- [ ] Apply AI22.8.6 polish schema
- [ ] Deploy API + UI
- [ ] Confirm `/setup/attendance-recovery` loads enterprise widgets
- [ ] Confirm faculty pending cards show SLA badges
- [ ] Confirm bulk ops require Admin
- [ ] Confirm SignalR FacultyHub still receives recovery/SLA events
- [ ] Confirm `AttendanceSessionResolver` unchanged
- [ ] Smoke Legacy + Timetable attendance entry paths

## Test Report

Unit coverage: `AI2286EnterpriseOperationsPolishTests` (SLA bands, enricher SLA fields, bulk safety flags, department/timeline reuse flags, resolver type guard, notification codes).

Regression intent: Legacy attendance, Timetable attendance, recovery queue, SignalR notifier contract.

## Desktop Copy

```powershell
powershell -File scripts\AI22_8_6_Copy.ps1 -Prompt All
```

Destination: `D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI22.8.6`

# AI22.8 — Enterprise Attendance Recovery & Session Management

## Objective

Resume **existing** `AttendanceSession` work. Never create duplicate attendance or recognition sessions. Compose recognition, review, retry, notifications, and finalization.

## Architecture

```
Attendance Session
        │
        ├── Recognition (existing pipeline)
        ├── Review (existing workspace)
        ├── Recovery (AI22.8 composition)
        ├── Retry (stage-aware requeue)
        ├── Notifications (FacultyHub SignalR)
        └── Finalization (existing finalizer)
```

## Surfaces

| Surface | Route / API |
|---------|-------------|
| Faculty pending | `/faculty?tab=pending` · `GET /api/attendance-recovery/pending` |
| Resume checkpoint | `GET/PUT .../sessions/{id}/resume|checkpoint` |
| Retry | `POST .../sessions/{id}/retry` |
| Auto-resume prompt | `GET/POST .../auto-resume*` |
| Admin dashboard | `/setup/attendance-recovery` · `/api/admin/attendance-recovery/*` |

## Retry rules

Retry only failed stages via `IClassroomPhotoService.Requeue*` / finalizer.  
Never restart successfully processed images, completed review, or finalized attendance.

## Expiration

Configurable 24 / 48 / 72 hours (`AttendanceRecovery` options). Expired sessions cannot finalize until admin restore.

## Notifications

Reuses `FacultyHub` event `AttendanceRecoveryNotification`. No polling. No new framework.

## Mobile

Faculty pending panel + sticky resume dialog reuse AI22.7C responsive workspace (`touchSx`, scrollable tabs). No separate mobile app.

## Guardrails

- `AttendanceSessionResolver` unchanged
- Attendance APIs backward compatible
- Tenant isolation on all queries
- Audit via `IAuditService` + `AttendanceRetryHistory`

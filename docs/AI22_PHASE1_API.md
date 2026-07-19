# AI22 Phase 1 — Enterprise Enrollment API Platform

## Overview

AI22 Phase 1 exposes the AI21 Enrollment Platform through REST APIs and SignalR. Controllers are orchestration-only; all business logic delegates to application/infrastructure services from AI20/AI21.

## Endpoints

| Method | Route | Policy | Description |
|--------|-------|--------|-------------|
| GET | `/api/enrollment/dashboard` | CanViewEnrollment | Dashboard metrics, system status, configuration |
| GET | `/api/enrollment/readiness` | CanViewEnrollment | API-driven readiness (`CanStart`, reasons) |
| GET | `/api/enrollment/history` | CanViewEnrollment | Paged batch history |
| GET | `/api/enrollment/batches` | CanViewEnrollment | Paged batch list |
| GET | `/api/enrollment/batches/{id}` | CanViewEnrollment | Batch detail |
| GET | `/api/enrollment/batches/{id}/progress` | CanViewEnrollment | Live progress snapshot |
| GET | `/api/enrollment/batches/{id}/students` | CanViewEnrollment | Student enrollment explorer (read-only) |
| POST | `/api/enrollment/preview` | CanManageEnrollment | Preview eligible student count |
| POST | `/api/enrollment/batches` | CanManageEnrollment | Create batch (queues work; no inline AI) |
| POST | `/api/enrollment/batches/{id}/cancel` | CanManageEnrollment | Cancel batch (audited) |
| POST | `/api/enrollment/batches/{id}/retry` | CanManageEnrollment | Retry/resume batch (audited) |

## SignalR

- Hub: `/hubs/enrollment`
- Auth: JWT bearer (`access_token` query for WebSocket)
- Events: `BatchCreated`, `BatchStarted`, `BatchProgress`, `BatchCompleted`, `BatchFailed`, `BatchCancelled`
- Server broadcasts active batch progress every 5 seconds (no UI polling)

## Authorization

- **CanViewEnrollment**: SuperAdmin, `Enrollment.View`, `Enrollment.Manage`, or `Students.View`
- **CanManageEnrollment**: SuperAdmin, `Enrollment.Manage`, or `Students.Manage`

## Application Services

- `IEnrollmentDashboardService`
- `IEnrollmentReadinessService`
- `IEnrollmentHistoryService`
- `IBatchCancellationService`
- `IBatchRetryService`
- `IEnrollmentEventPublisher`

## Audit

- Batch creation → `AuditAction.Created`
- Batch cancellation → `AuditAction.Cancelled`
- Batch retry → `AuditAction.Updated`

## Registration

```csharp
services.AddEnrollmentApiPlatform(); // Infrastructure DI
builder.Services.AddSignalR();
builder.Services.AddSingleton<IEnrollmentEventPublisher, EnrollmentEventPublisher>();
builder.Services.AddHostedService<EnrollmentProgressBroadcastService>();
app.MapHub<EnrollmentHub>("/hubs/enrollment");
```

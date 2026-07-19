# AI22 Phase 1 — Reuse Analysis

## Reused (not duplicated)

| Capability | Source | Usage in AI22 |
|------------|--------|---------------|
| Batch create/cancel/retry | `IEnrollmentBatchService` (AI21) | History service delegates create/cancel/retry |
| Eligible students | `IEnrollmentEligibleStudentQuery` | Readiness + preview |
| Progress / ETA | `IEnrollmentProgressReporter` | Batch detail, progress DTO, SignalR broadcast |
| Platform health | `IAIHealthService` (AI20 Operations) | Dashboard system status + readiness |
| Artifact queue depth | `IArtifactUploadQueue` | Dashboard `QueueLength` |
| Photo provider config | `ExamBranchPhotoProviderOptions` | Configuration DTO |
| Audit trail | `IAuditService` | Create, cancel, retry |
| Persistence | `IApplicationDbContext` | Read-only queries for history/explorer |
| Background workers | AI21 enrollment workers | Processing after batch create |

## Not modified (frozen)

- AI21 pipeline stages, orchestrator internals, face enrollment processors
- AI20 recognition, attendance, governance pipelines

## New surface area only

- DTOs in `Abhyanvaya.Application/EnrollmentApi`
- API services in `Abhyanvaya.Infrastructure/EnrollmentApi`
- Controllers under `Abhyanvaya.API/Controllers/Enrollment`
- `EnrollmentHub` + progress broadcast hosted service

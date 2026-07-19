# AI22 — Full Stack Reuse Analysis

## Backend (Phase 1)

See [AI22_PHASE1_REUSE_ANALYSIS.md](./AI22_PHASE1_REUSE_ANALYSIS.md).

## Frontend (Phase 2)

| UI concern | Implementation |
|------------|----------------|
| HTTP | Single `EnrollmentApiClient` (axios) — no scattered `fetch()` |
| State | `EnrollmentDashboardContext` — dashboard, readiness, batches, progress |
| Live updates | SignalR via `EnrollmentApiClient.connectSignalR` |
| Readiness gating | `EnrollmentStartButton` renders API `canStart` + `reasons` only |
| Wizard | MUI Stepper → `POST /api/enrollment/preview` + `POST /api/enrollment/batches` |
| Setup catalogs | Reuses `setupService` (courses, groups, semesters) |
| College context | Reuses `adminService.getTenantCollege` |
| System status tiles | Reuses `AiSystemStatusCard` with API-mapped items |
| Metrics tiles | Reuses `StatCard` via `EnrollmentStatistics` |

## Removed mock patterns

- `SYSTEM_STATUS_ITEMS` static health
- `DisabledActionButton` placeholder
- Hardcoded configuration card values
- `--` statistic placeholders
- Disabled History/Failures/Settings tabs (overview is live; other tabs deferred to same APIs)

## Accessibility / UX

- Loading skeletons on statistics and batch grid
- `EnrollmentErrorBoundary` for render failures
- Snackbar toasts for batch commands
- ARIA labels on tables, wizard, and progress bars
- Theme-driven MUI components (no inline styles beyond `sx` layout)

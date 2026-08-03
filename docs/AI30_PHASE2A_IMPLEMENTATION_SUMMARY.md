# AI30 Phase 2A — Implementation Summary

| Field | Value |
|-------|-------|
| **Migration** | `20260801181017_AI30_Phase2A_TimetableGovernance` |
| **Date** | August 2026 |

## Architecture decisions

1. Additive `ScheduleVersion` + nullable `Timetable.ScheduleVersionId`
2. Approval approves version; Publish is separate lifecycle step
3. Soft validation warnings only
4. Clone via dedicated background poller (not Enrollment queue)
5. Change history append-only (no rollback)

## High-level artifacts

- Domain entities/enums for version, approval, clone job, history, dismissals
- Application governance services + DTOs + validators
- Infrastructure repos, configs, `TimetableCloneBackgroundService`, migration
- API `Phase2AControllers` + timetable publish/archive/soft-warnings/history
- UI `pages/setup/scheduling/governance/*` + SoftWarningsPanel
- Docs `AI30_PHASE2A_*.md`

## Tests

`dotnet test --filter FullyQualifiedName~Phase2A` → **16 passed**, 0 failed.

```powershell
dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
```

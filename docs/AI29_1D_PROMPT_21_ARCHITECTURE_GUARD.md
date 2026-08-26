# AI29.1D Prompt 21 — Architecture Guard & Compliance Report

## Required layering

**UI → API / Application Contracts → Domain Services**

```
UI
 ↓
API / Application Contracts
 ↓
Domain Services
```

Existing backend services remain authoritative. The UI consumes HTTP contracts and presents results; it does not own academic authority. Calling server endpoints (e.g. `resolveAttendanceSession`) is allowed; reimplementing those engines in the UI is not.

## Enforced rules

### UI must NOT directly access

| Forbidden | Guard |
|-----------|--------|
| EF Core / Entity Framework | Source + package scan |
| `DbContext` / `IApplicationDbContext` | Source scan |
| Database tables / raw SQL | Source scan |
| Allocation persistence entities (`AllocationEngineScenario`, …) | Source scan |
| Scheduling / attendance persistence entity wiring (`DbSet<>`, domain entity namespaces) | Source scan |
| DB driver / ORM npm packages (`pg`, `mssql`, `typeorm`, `@prisma/client`, …) | `package.json` scan |

### UI must NOT implement authoritative logic

| Forbidden | Authoritative backend |
|-----------|------------------------|
| Calculate authoritative capacity | `ISectionCapacityEngine` |
| Calculate allocation scores | `AllocationEngine` / `AllocationScoreCalculator` |
| Resolve timetable sessions | `AttendanceSessionResolver` |
| Implement attendance eligibility | Attendance Application services / save scope |
| Implement SectionGroup resolution | Section / attendance session mapping services |
| Implement lifecycle transitions | `AllocationScenarioLifecycleService` |
| Implement governance rules | `IAllocationGovernanceService` |

Displaying API-authored scores, occupancy, or constraint evaluations is allowed.

### Assembly layering

- Domain must not reference Application / API / UI
- Application Academic types must not reference UI
- Platform `ValidateAllocationBoundaries()` must remain green

## Compliance report API

`GET /api/v1/academic-structure/architecture/ai29-1d-report`  
Policy: `CanManagePrograms` (same as platform architecture report).

When the UI source tree is not present in the deployment host, the report still runs assembly/backend checks and notes that the UI file scan was skipped.

## Local quality gate

```bash
dotnet test --filter FullyQualifiedName~AI29_1D_Prompt21
```

Produces / refreshes machine-readable snapshot:

`docs/architecture/AI29_1D_architecture_compliance.json`

## Implementation

| Piece | Path |
|-------|------|
| Guard | `Abhyanvaya.Application/Academic/Architecture/Ai291DArchitectureGuard.cs` |
| Report model | `Ai291DArchitectureComplianceReport.cs` |
| Tests | `AI29_1D_Prompt21_ArchitectureGuardTests.cs` |
| API | `AcademicStructureV1Controller.Ai291DArchitectureReport` |

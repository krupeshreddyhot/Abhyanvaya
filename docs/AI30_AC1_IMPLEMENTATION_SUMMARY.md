# AI30 AC1 — Implementation Summary

## Objective

Eliminate duplicate Scheduling Department master. Scheduling consumes Catalog Department via `DepartmentId`.

## Files deleted

| Path |
|------|
| `Abhyanvaya.Application/Scheduling/DepartmentSchedulingService.cs` |
| `Abhyanvaya.Application/Scheduling/IDepartmentSchedulingService.cs` |
| `Abhyanvaya.Application/DTOs/Scheduling/DepartmentDtos.cs` |
| `Abhyanvaya.Application/Scheduling/Departments/Validators/DepartmentSchedulingValidators.cs` |
| `Abhyanvaya.Application.UnitTests/Scheduling/Phase1A/DepartmentSchedulingServiceTests.cs` |
| `abhyanvaya-ui/src/pages/setup/scheduling/DepartmentsPage.tsx` |

## Files modified (selected)

| Area | Files |
|------|-------|
| API | `DepartmentController.cs`, `Phase1AControllers.cs`, `AuthorizationPolicies.cs`, `Program.cs` |
| Application | `DependencyInjection.cs`, `IDepartmentRepository.cs`, Catalog `DepartmentDtos.cs` |
| Infrastructure | `DepartmentRepository.cs` |
| Domain | `PermissionKeys.cs` (retire Scheduling Department from `All`) |
| UI | Scheduling Hub/Dashboard, Subject Allocation, Faculty Preferences, Timetable Hub/Entry, Schedule Versions, AppRoutes, MainLayout, setupService, schedulingService, permissionKeys, SetupHub, Catalog DepartmentsPage |
| Tests | Phase1A permission tests; new `Ac1/Ac1CatalogDepartmentSsotTests.cs` |

## Architecture decisions

1. Catalog `DepartmentController` is the only Department CRUD API.
2. Scheduling department lookups use `GET api/department` with `CanViewDepartmentLookup`.
3. `IDepartmentRepository` remains as a **read-only** Catalog helper for Scheduling services + delete reference guard.
4. No DB migration — entity/table unchanged.
5. Legacy permission seed rows 20–21 retained for compatibility; removed from active `PermissionKeys.All`.

## ADL compliance

| Principle | Compliance |
|-----------|------------|
| SSOT | Department owned once (Catalog) |
| Clear ownership | Documented in ownership matrix |
| No duplicate CRUD | Scheduling module removed |
| Backward compatibility | Same `DepartmentId` FKs; Catalog API extended with `Description`/`IsActive` |

## Migration

**None generated.**

## Prompt coverage

| Prompt | Outcome |
|--------|---------|
| AC1.1 Remove Scheduling Department module | Done |
| AC1.2 Rewire Scheduling | Done |
| AC1.3 Scheduling Dashboard / Hub | Done |
| AC1.4 Subject Allocation | Done |
| AC1.5 Faculty Preferences | Done |
| AC1.6 Timetable Designer | Done |
| AC1.7 Navigation cleanup | Done |
| AC1.8 Architecture validation doc | Done |
| AC1.9 SSOT + ownership matrix | Done |
| AC1.10 Tests + reports | Done |

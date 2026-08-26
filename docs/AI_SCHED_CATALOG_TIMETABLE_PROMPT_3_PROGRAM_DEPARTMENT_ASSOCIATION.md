# AI-SCHED-CATALOG/TIMETABLE — Prompt 3 (P1-2)
# Program → Department Association (Programs remain optional)

**Workstream:** AI-SCHED-CATALOG/TIMETABLE  
**Prompt:** 3 — P1-2 Associate Program with Department  
**Date:** 2026-08-21  
**Type:** Implementation + verification  
**Final status: PASS**

---

## 1. Discovery (Phase 1)

| Item | Finding |
| --- | --- |
| Program entity | `CollegeId` only; **no `DepartmentId`** before P1-2 |
| Department entity | `CollegeId`; table `Department`; parallel catalog SSOT |
| Programs table | `Programs`; index `(TenantId, ProgramCode)` |
| Ownership (before) | Program → College |
| Ownership (after) | Program → Department (CollegeId retained for tenant/college scope) |
| `EnablePrograms` | `TenantAcademicConfiguration.EnablePrograms` — unchanged, authoritative |
| API | `ProgramsController` + `AcademicCatalogService` |
| DTOs | `CreateProgramRequest` / `UpdateProgramRequest` / `ProgramDto` |
| UI | `ProgramsPage.tsx` + `programService.ts` |
| Seed | No Program seed rows in EF migrations |
| Live data (local) | **1** Program (`PG001` Commerce, Tenant 1, College 1); **1** Department (`001` Commerce, same Tenant/College) |
| Tests | `AI29_1A_ProgramManagementTests` + Course↔Program suites |

---

## 2. Architecture decision

- **Program belongs to exactly one Department** (`Program.DepartmentId` → `Department.Id`, Restrict FK).
- **`CollegeId` retained** on Program for existing tenant/college resolution and consistency checks.
- **`EnablePrograms` unchanged** — when false, Course workflows do not require Program; Create Program remains gated in UI by the flag.
- **P1-3 deferred** — `Course.DepartmentId` / Course→Program hierarchy resolution not implemented.

---

## 3. Existing-data handling

| Rule | Detail |
| --- | --- |
| Deterministic map | Backfill only when **exactly one** non-deleted `Department` exists for the same `TenantId` + `CollegeId` |
| Not used | Name matching, default Department, arbitrary IDs |
| Failure mode | Migration **raises** if any Program remains unmapped |
| Local DB result | Program `PG001` → DepartmentId **1** (sole department); **0 unmapped** |

---

## 4. Schema / migration

Migration: `20260821120000_AI_SCHED_CATALOG_P1_2_ProgramDepartment`

1. Add nullable `Programs.DepartmentId`
2. Deterministic backfill (`HAVING COUNT(*) = 1`)
3. Abort if any NULL remains
4. Alter to NOT NULL + indexes + FK → `Department` Restrict
5. Down drops FK/indexes/column (reversible)

Local migrate: **applied** (`Programs.DepartmentId` present; history row inserted).

---

## 5. API changes

- `CreateProgramRequest` / `UpdateProgramRequest`: required `DepartmentId`
- `ProgramDto`: `DepartmentId`, `DepartmentCode`, `DepartmentName`
- `GET /api/programs/department-options` — tenant/college-scoped choices (Program catalog auth)
- Server validation via `ProgramDepartmentAssociationRules`:
  - Department exists (tenant-visible)
  - Same tenant + same College as Program
  - Required when Programs enabled (and for Program writes generally)

Authorization policies unchanged.

---

## 6. UI changes

- `ProgramsPage`: Department column + required Department select on create/edit
- Loads options via `listProgramDepartmentOptions`
- Messaging: Department → Program → Course when enabled; Program not required when disabled
- **No** Course / Student / SA / Timetable screen changes

---

## 7. Optional Program behavior

| Mode | Behavior |
| --- | --- |
| `EnablePrograms = false` | Create Program disabled in UI; Course remains usable without Program |
| `EnablePrograms = true` | Program create/edit requires Department |

---

## 8. Tests

| Suite | Result |
| --- | --- |
| `AiSchedCatalogTimetableP12ProgramDepartmentTests` | PASS |
| `AI29_1A_ProgramManagementTests` (updated) | PASS |
| CAP/TG architecture + related filters | PASS (see regression) |
| UI Vitest `AiSchedCatalogTimetableP12ProgramDepartmentUi` | PASS |

---

## 9. Architecture guards

- Program ownership Department-based (`DepartmentId` + rules)
- Cross-tenant Department rejected
- `EnablePrograms` property untouched
- Course has no `DepartmentId` (P1-3)
- No TG/CAP production file changes

---

## 10. Residual issues

- Environments with **multiple** Departments per College and existing Programs will **fail migration** until a manual mapping worksheet is applied (by design).
- Academic hierarchy tree UI still roots at Program/Course (not Department→Program tree) — later catalog UX if needed.
- P1-3 (Course Department/Program resolution) not started.

---

## 11. Files changed

| File | Change |
| --- | --- |
| `Abhyanvaya.Domain/Entities/Academic/Program.cs` | `DepartmentId` + nav |
| `Abhyanvaya.Domain/Entities/Department.cs` | `Programs` collection |
| `Abhyanvaya.Application/Academic/ProgramDepartmentAssociationRules.cs` | New rules |
| `Abhyanvaya.Application/DTOs/Academic/ProgramDtos.cs` | Department fields + options DTO |
| `Abhyanvaya.Application/Academic/Validators/ProgramValidators.cs` | Require DepartmentId |
| `Abhyanvaya.Application/Academic/AcademicCatalogService.cs` | Validate + map Department |
| `Abhyanvaya.Application/Academic/IAcademicCatalogService.cs` | Department options |
| `Abhyanvaya.Application/Academic/IAcademicStructureService.cs` | Department options |
| `Abhyanvaya.Application/Academic/AcademicStructureService.cs` | Forward options |
| `Abhyanvaya.API/Controllers/ProgramsController.cs` | `department-options` |
| `Abhyanvaya.Infrastructure/Persistence/ApplicationDbContext.cs` | EF relationship |
| `Abhyanvaya.Infrastructure/Persistence/Migrations/20260821120000_AI_SCHED_CATALOG_P1_2_ProgramDepartment.cs` | Migration |
| `abhyanvaya-ui/src/services/programService.ts` | Department contract |
| `abhyanvaya-ui/src/pages/setup/ProgramsPage.tsx` | Department UI |
| Unit + UI tests | New/updated |
| This document | Documentation |

**STOP — do not start P1-3.**

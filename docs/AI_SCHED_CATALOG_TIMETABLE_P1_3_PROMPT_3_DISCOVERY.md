# AI-SCHED-CATALOG/TIMETABLE — P1-3 Prompt 3 Discovery  
# SubjectAllocation DepartmentId assignment surfaces

**Date:** 2026-08-22  
**Type:** READ-ONLY discovery (before implementation)  
**Status:** COMPLETE

---

## A–C. Catalog ownership (frozen)

| Entity | Ownership |
| --- | --- |
| Program | `DepartmentId` required (P1-2) |
| Course | `DepartmentId` required SSOT (P1-3 Prompt 2); `ProgramId` optional |
| Department | College + Tenant scoped |

---

## D. SubjectAllocation entity

`Abhyanvaya.Domain/Entities/Scheduling/SubjectAllocation.cs`

- Required: `CourseId`, `GroupId`, `SemesterId`, **`DepartmentId`**, Subject/Staff/Year, …
- Unique key includes `DepartmentId` (scheduling uniqueness dimension).

---

## E–G. Create / Update / Delete

**Sole production writer:** `SubjectAllocationService`

| Method | DepartmentId source today |
| --- | --- |
| `CreateAsync` | `request.DepartmentId` via `MapToEntity` |
| `UpdateAsync` | `request.DepartmentId` via `ApplyRequest` |
| `DeleteAsync` | Soft-delete only — no Department change |

**Validation today:** Department exists (tenant repo); duplicate uniqueness; workload. **No Course.DepartmentId check.**

---

## H. List / query

`ListAsync(academicYearId, staffId, departmentId)` filters on **stored** `SubjectAllocation.DepartmentId` (denorm). Keep for performance; not Catalog SSOT.

---

## I. TimetableEntry

`TimetableService` sets `entry.DepartmentId = allocation.DepartmentId` when creating from SA. Out of scope beyond keeping SA consistent (entries stay in sync via SA copy).

---

## J–K. Assignment / validation locations

| Path | Role |
| --- | --- |
| `SubjectAllocationService.MapToEntity` / `ApplyRequest` | **Only** SA DepartmentId write path |
| `SubjectAllocationValidators` | `DepartmentId > 0` |
| Controllers | Thin — `SchedulingResourceControllers` |

No other Application service creates SubjectAllocation rows.

---

## L. Data (local)

| Metric | Value |
| --- | --- |
| Active SA rows | 1 |
| `SA.DepartmentId <> Course.DepartmentId` | **0** |

Deterministic repair migration still safe as no-op locally.

---

## M–N. Tests / guards

- `SubjectAllocationServiceTests` — create/duplicate/workload
- TG/CAP architecture guards — must remain untouched functionally
- Course/Program P1-2/P1-3 tests — Catalog SSOT stays Course.DepartmentId

---

## Intentional multi-dept Course scheduling?

**No** product rule found that allows Course in Dept A scheduled under Dept B. Unique key historically allowed it; governance now forbids it.

---

## Ambiguities

None blocking. Rule locked: `SA.DepartmentId == Course.DepartmentId`.

---

## Implementation plan (Prompt 3)

1. Pure rules: `SubjectAllocationCourseDepartmentRules`
2. Resolve Course via `IApplicationDbContext.Courses` (tenant fail-closed)
3. Create/Update: reject mismatch; set `DepartmentId` from Course
4. Optional migration: UPDATE SA from Course where mismatched
5. UI: sync Department from Course selection; filter courses by department
6. Tests + architecture guards + docs

# AI-SCHED-CATALOG/TIMETABLE — P1-3 Prompt 4  
# Timetable Entry Department Consistency & Course Ownership Alignment

**Date:** 2026-08-22  
**Type:** Implementation  
**Final recommendation: PASS**

---

## 1. Problem statement

`TimetableEntry.DepartmentId` is a scheduling denormalization. It must not become an independent Catalog ownership source. After P1-3 Prompt 2–3, ownership is:

`Course.DepartmentId` → `SubjectAllocation.DepartmentId` → `TimetableEntry.DepartmentId`

## 2–5. Existing architecture & invariant

| Layer | Role |
| --- | --- |
| `Course.DepartmentId` | Catalog SSOT |
| `SubjectAllocation.DepartmentId` | Validated denorm (Prompt 3) |
| `TimetableEntry.DepartmentId` | Validated denorm (this prompt) |

**Invariant:** `TimetableEntry.DepartmentId == Course.DepartmentId` (via SA → Course).

## 6. Discovery — mutation paths

| Path | Prior DepartmentId source | Client-supplied? |
| --- | --- | --- |
| `CreateEntryAsync` | `ApplyAllocationDenormalization` ← SA.DepartmentId | No (DTO has no DepartmentId) |
| `UpdateEntryAsync` | same | No |
| `BulkUpsertEntriesAsync` | same | No |
| `CopyEntryAsync` / `DuplicateEntryAsync` | `CloneEntry` copies source | No |
| `TimetableCloneService` | `CloneEntry` | No |
| `ScheduleVersionService` | `CloneEntry` | No |
| `MoveEntryAsync` | Does not change DepartmentId | N/A |

**Prior gap:** denorm trusted `SA.DepartmentId` without resolving `Course.DepartmentId`. Clone paths could propagate a stale entry DepartmentId.

**Mismatch risk:** Possible if SA denorm drifted. Local audit: **0** entry↔course and entry↔SA mismatches (1 entry).

## 7. Implementation

1. `TimetableEntryCourseDepartmentRules` — SA must match Course; optional requested entry Department must match Course; align to Course.
2. `ApplyAllocationDenormalization(..., courseDepartmentId)` — fail-closed; sets `DepartmentId` from Course.
3. Create/Update/Bulk resolve Course via tenant-scoped query.
4. Copy/Duplicate/Clone job/Schedule version call `RealignDepartmentFromCourseAsync` after `CloneEntry`.
5. Create/Update entry DTOs remain without `DepartmentId` (server-derived).

## 8. UI contract

`TimetableEntryDialog` Department control labeled **Department (filter)** — filters allocations; synced from allocation on select; **not** sent on create/update. Server remains authority.

## 9. Data audit / migration

**0 mismatches found; no data repair required.** No migration added.

## 10. Tenant isolation

Course and SA lookups filter `TenantId`. Cross-tenant Course ⇒ “Course not found.”

## 11–13. Tests / regression / guards

| Suite | Result |
| --- | --- |
| Filtered unit (P1-3 + SA + TimetableEntry + TG/CAP architecture) | **118 passed** |
| UI Vitest Prompt 4 | see run |
| API build | see run |
| UI typecheck + production build | see run |

## 14. Deferred

- Historical entry repair for other environments (run Prompt-3 SA align first, then optional entry UPDATE from Course if needed)
- Timetable header `Timetable.DepartmentId` (optional filter scope — not entry ownership)
- Group-specific Semester / student cascade (later prompts)

## 15. Risks

Clone fails if SA/Course inconsistent — intentional fail-closed.

## 16. Final recommendation

**PASS**

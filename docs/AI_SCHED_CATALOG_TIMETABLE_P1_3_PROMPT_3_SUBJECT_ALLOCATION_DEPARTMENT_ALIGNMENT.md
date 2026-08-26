# AI-SCHED-CATALOG/TIMETABLE — P1-3 Prompt 3  
# Subject Allocation Department Consistency & Course Ownership Alignment

**Date:** 2026-08-22  
**Type:** Implementation  
**Final status: PASS**

---

## 1. Current state (before)

- `Course.DepartmentId` = Catalog SSOT (P1-3 Prompt 2).
- `SubjectAllocation.DepartmentId` taken from **client request** with no Course alignment.
- Unique key included DepartmentId as an independent dimension → multi-dept Course allocations were structurally possible.

## 2. Previous inconsistency

Client could submit `DepartmentId ≠ Course.DepartmentId`. Local DB had **0** mismatches after Prompt 2 backfill.

## 3. Architectural decision

`SubjectAllocation.DepartmentId` is **scheduling denormalization** of `Course.DepartmentId`.

```
Course.DepartmentId  →  authoritative
SubjectAllocation.DepartmentId  →  must equal Course.DepartmentId
```

## 4–5. SSOT vs denorm

| Field | Role |
| --- | --- |
| `Course.DepartmentId` | Catalog ownership SSOT |
| `SA.DepartmentId` | Aligned denorm for filters/queries |

## 6. Create / update

1. Resolve Course (tenant fail-closed).
2. Evaluate `SubjectAllocationCourseDepartmentRules`.
3. Reject if request Department ≠ Course Department.
4. Persist `SA.DepartmentId = Course.DepartmentId`.
5. Changing Course on update re-aligns Department from the new Course.

## 7. Tenant isolation

Course lookup filters `TenantId`; foreign-tenant Course ⇒ “Course not found”.

## 8. Migration

`20260822120000_AI_SCHED_CATALOG_P1_3_SubjectAllocationDepartmentAlign`

- `UPDATE SA SET DepartmentId = Course.DepartmentId WHERE mismatched`
- Does not modify Course rows
- Local: 0 rows updated (already aligned)

## 9. API

DTO `DepartmentId` retained for compatibility; server validates and overwrites with Course ownership.

## 10. UI

`SubjectAllocationPage`: Course selection syncs Department; Course list filtered by Department; server remains authoritative.

## 11. Tests / regression

| Suite | Result |
| --- | --- |
| Filtered unit (`SubjectAllocation` + P1-3 + TG/CAP architecture) | **68 passed** |
| UI Vitest P1-3 Prompt 3 | **3 passed** |
| API build | **PASS** |
| UI typecheck + production build | **PASS** |
| Local migration apply | **0 rows updated** (already aligned) |

## 12. Deferred

- TimetableEntry historical row repair (entries already copy from SA on write)
- Group-specific Semester / Student cascade
- SA uniqueness key simplification (optional later)

## 13. Non-actions

No TeachingGroup, TimetableSection projector, CAP, Attendance, EnablePrograms, or Program optionality changes.

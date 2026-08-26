# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 2A  
# Semester Write-Path Enforcement & Group Ownership

**Date:** 2026-08-22  
**Type:** Implementation (write-path only)  
**Final status: PASS**

---

## Changes made

1. `SemesterGroupOwnershipRules` — Group required; tenant check; CourseId from Group; Course hint mismatch rejected.
2. `CreateSemesterRequest` / `UpdateSemesterRequest` — `GroupId` is `int` (required).
3. `SemesterController` — create/update use rules; uniqueness `TenantId + GroupId + Number`; CourseId server-aligned; list exposes `IsLegacyCourseWide` + optional `courseId`/`groupId` filters.
4. `MasterController` semesters/full — exposes `IsLegacyCourseWide`.
5. `SemestersPage` — Group required; no None/course-wide create; legacy chip; explicit convert warning on edit.
6. `setupService` create/update payloads require `groupId: number`.

## API behavior

| Action | Behavior |
| --- | --- |
| Create without Group | 400 "Group is required for a Semester." |
| Create valid Group | CourseId = Group.CourseId; persist GroupId |
| Course ≠ Group.Course | 400 "Group does not belong to Course." |
| Cross-tenant Group | 400 "Group does not belong to tenant." |
| Duplicate Group+Number | 400 duplicate message |
| Update | Same validations; cannot clear GroupId |
| GET | Still returns legacy NULL-group rows; `isLegacyCourseWide` |

## UI behavior

- Course → Group (required) → Number/Name
- Table shows **Legacy / Course-wide** for `groupId == null`
- Editing legacy requires selecting a Group (explicit conversion)

## Legacy compatibility

- DB `GroupId` remains nullable
- AcademicTree `null \|\| group` wildcard **unchanged**
- No auto-assignment of NULL-group rows
- Reads still include legacy Semesters

## Migration intentionally deferred

- No NOT NULL
- No unique DB index (NULL-group duplicates / Case C ambiguity)
- No Semester split / Student remap

## Data audit (local, read-only)

| Metric | Value |
| --- | --- |
| Semesters total | 6 |
| NULL GroupId | 5 |
| Group-specific | 1 |
| Dup within Group+Number | 0 |
| Courses with 0 Groups | 0 |
| Courses with 1 Group | 0 |
| Courses with multiple Groups | 1 (B.Com) |
| Invalid Course/Group on SA rows | 0 |

Physical unique index deferred: unsafe while NULL-group legacy rows and multi-Group Course exist.

## Tests / guards / regression

| Suite | Result |
| --- | --- |
| Filtered unit (P1-4 + P1-3 + TG/CAP + CourseDept) | **83 passed** |
| UI Vitest Prompt 2A | **3 passed** |
| API build | **PASS** (0 errors) |
| UI production build (`tsc -b` + vite) | **PASS** |

## Known risks

- Editing a legacy row and choosing one Group is an **explicit** conversion; remaining Groups still need split worksheet later.
- Duplicate Number=4 among NULL-group rows remains until migration prompt.

## Next recommended prompt

**P1-4 Prompt 2B / 3** — Legacy Semester split worksheet + Student semester remapping (fail closed for multi-Group Courses).

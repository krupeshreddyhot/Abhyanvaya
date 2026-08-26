# AI29.1D Prompt 4B — Program Hierarchy Consistency & Feature-Mode Hardening

## Part 1 — Feature mode

| `EnablePrograms` | Behavior |
|------------------|----------|
| `false` | Legacy Course → Group → Semester → Subject (full authorized course catalog) |
| `true` | Program mode **even if zero Programs exist** — never fall back to full course catalog |

When Programs are enabled but none are configured:
- Program selector visible with empty state: **“No academic programs have been configured.”**
- Course selector disabled/empty with the same message

## Part 2 — Course.ProgramId authority

- **Authoritative:** `Course.ProgramId === selected Program` → may show
- **Conflict:** hierarchy lists course under Program A but `Course.ProgramId` is B or null → **do not show** under A
- **Null ProgramId:** never shown when a Program is selected
- Hierarchy is a read-optimized projection; inconsistencies are recorded via `hierarchyConsistencyWarnings` (+ console.warn)

Catalog enrichment uses existing `GET /api/course` (returns `ProgramId`) merged onto the authorization-friendly master course list. No API/schema changes.

## Part 3 — Unchanged rules

- Subject = Course + Group + Semester
- Section is operational only; changing Section does not clear Subject
- Attendance / Scheduling / Timetable / Allocation Engine / Section domain / Subject Master / APIs / DB unmodified

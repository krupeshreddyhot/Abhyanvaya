# AI29 — Academic Structure & Section Management

## Objective

Introduce an enterprise **operational Section** layer under Course → Group → Semester for:

- Section Master (CRUD, capacity)
- Student section allocation with history
- Faculty section allocation
- Timetable multi-section mapping (combined classes)
- Optional section-aware attendance lists
- Dashboard preparation APIs for AI31.x drill-down

**Subject remains curriculum-only.** Section is never stored on Subject Master.

## Hierarchy

```
College → Academic Year → Course → Group → Semester → Subjects (curriculum)
                                              ↓
                                         Sections (operational)
                                              ↓
                                    Students / Faculty / Timetable / Attendance
```

## Attendance Compatibility (Mandatory)

| Mode | Behavior |
|------|----------|
| Legacy / Manual (no timetable) | Course → Group → Semester → Subject → Period → students. **Unchanged.** Optional `sectionId` / `sectionIds` filter only when provided. |
| Timetable | Existing resolver path unchanged. **Additive** `SectionIds` / `SectionCodes` from `TimetableSections`. |

`AttendanceSessionResolver` continues to choose Legacy vs Timetable. Manual attendance never requires sections or timetable.

## Schema (non-destructive)

Tables: `Sections`, `StudentSections`, `FacultySectionAssignments`, `TimetableSections`, `AttendanceSessionSections`, `SectionAllocationPreferences`.

Apply: `scripts/Apply_AI29_SectionSchema.sql`

## APIs

| Method | Route |
|--------|-------|
| CRUD | `/api/sections` |
| Students | `/api/student-sections`, `/transfer` |
| Faculty | `/api/faculty-sections` |
| Timetable map | `/api/timetable/{id}/sections` |
| Dashboard prep | `/api/sections/statistics`, `/dashboard/*` |
| Reports | `/api/sections/reports/{kind}` |

## Permissions

`Section.View|Create|Edit|Delete|AssignStudents|AssignFaculty`

## UI

Academic Setup → **Sections** (`/setup/sections`): List, Student Allocation, Faculty Allocation, Transfer / Auto-Allocate.

## Non-goals / constraints

- No Subject Master changes
- No hard-coded capacity
- No destructive migrations
- No AttendanceSessionResolver behavioral rewrite (additive enrichment only)

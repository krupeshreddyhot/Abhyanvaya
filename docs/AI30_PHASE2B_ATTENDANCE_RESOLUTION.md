# AI30 Phase 2B — Attendance Session Resolution

**Status:** Implemented  
**Principle:** Timetable integration is **optional**. Legacy attendance remains fully supported.

## Modes

### Legacy Mode (default / fallback)

Faculty → Course → Group → Semester → Subject → Period → Attendance

Used when:

- Staff identity is unavailable
- No current academic year
- No published/locked timetable for the faculty
- No timetable entry for today / current period

### Timetable Mode

Faculty → Today's Timetable → Current class → Attendance context pre-filled

Resolved fields: `CourseId`, `GroupId`, `SemesterId`, `SubjectId`, `PeriodNumber`, optional room/subject labels.

## Components

| Artifact | Role |
|----------|------|
| `IAttendanceSessionResolver` | Contract |
| `AttendanceSessionResolver` | Chooses Legacy vs Timetable |
| `GET /api/attendance-resolution/current` | Optional helper API |
| `AttendanceMarking.tsx` | Soft pre-fill + hint; cascading dropdowns unchanged |

## Non-breaking guarantees

- Existing `AttendanceController` and `AttendanceSessionController` APIs **unchanged**
- No Attendance schema migration
- No removal of Course/Group/Semester/Subject/Period UI
- Resolver failures silently fall back to Legacy
- Faculty can always override pre-filled values manually

## API

```
GET /api/attendance-resolution/current?staffId=&date=
Authorization: CanManageAttendance
```

Response includes `mode` (`Legacy` | `Timetable`), `hasTimetable`, and optional context IDs.

## ADL alignment

Supports Volume 00 backward-compatibility expectations and AI30 AC1 Catalog ownership (resolver only reads Scheduling timetable FKs to Catalog IDs).

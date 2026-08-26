# AI29.1D Prompt 11 — Attendance UI Integration

## Objective

Integrate **Program/Section** context into the existing Mark Attendance UI **without** breaking the manual cascade:

`Course → Group → Semester → Subject → Period`

Timetable assignment is **never** required for manual attendance. `AttendanceSessionResolver` is consumed as-is (no second resolver, no bypass).

## Modes

### Mode 1 — Timetable context

When `GET /api/attendance-resolution/current` returns `mode=Timetable` and `hasTimetable=true`:

- Prefill Course, Group, Semester, Subject, Period, Room (name)
- Prefill `sectionIds` / `sectionCodes` from resolver (`TimetableSections`)
- Combined classes (A + B) → multi-select sections → one roster via `sectionIds[]`
- Cascade remains editable

Program is populated only when present in session/catalog context; it is **not** required and is **not** invented when the resolver does not return a Program id.

### Mode 2 — Manual attendance

When there is no valid timetable assignment (`Legacy` / `hasTimetable=false`):

- Required: Course → Group → Semester → Subject → Period
- **Section optional**
  - Selected → `students-for-marking` receives `sectionId` / `sectionIds` (server filters via `StudentSections`)
  - Empty → omit filters → **existing full cohort behavior**
- Not required: Program, Section, Timetable, Room

## Hard restrictions honored

| Restriction | Status |
|-------------|--------|
| No second Attendance Resolver | ✓ |
| Do not bypass AttendanceSessionResolver | ✓ consumes `/attendance-resolution/current` |
| Timetable not mandatory | ✓ Manual mode |
| No Section on Subject Master | ✓ |
| Manual cascade retained | ✓ |
| No second SectionGroup model | ✓ |
| No duplicated eligibility logic | ✓ API filters only |
| AttendanceSessionResolver unmodified | ✓ additive DTO already had SectionIds |

## UI / client changes

| File | Change |
|------|--------|
| `AttendanceMarking.tsx` | Mode chip, optional multi-select Section, resolver prefill of sections/room, pass filters to roster |
| `attendanceMarkingScope.ts` | Mode + roster param helpers |
| `attendanceService.ts` | Optional `sectionId` / `sectionIds` on `getStudentsForMarking` |
| `schedulingService.ts` | `sectionIds` / `sectionCodes` on resolution DTO |
| `attendanceContext.ts` | Additive section/room/scope fields for AI panel context |

## API (unchanged contract)

`GET /api/attendance/students-for-marking` already supports optional `sectionId` and `sectionIds[]` (AI29). Prompt 11 wires the UI to that contract.

## Regression tests

### Frontend (`attendanceMarkingScope.test.ts`)

1. Faculty with timetable  
2. Faculty without timetable  
3. Faculty with timetable and Section  
4. Faculty without timetable and no Section  
5. Faculty manually selecting Section  
6. Combined Section attendance  

### Backend (`AI29_1D_Prompt11_AttendanceUiIntegrationTests.cs`)

Same six scenarios at the resolution DTO / contract level, plus resolver type still present.

## Workflow summary

```
MODE 1 (timetable): Faculty → Timetable context → Program* / Course / Group / Semester / Section(s) / Subject / Period / Room → Attendance
MODE 2 (manual):    Faculty → Attendance → Course → Group → Semester → [Section optional] → Subject → Period → Students → Mark
* Program only when available; never required for Manual.
```

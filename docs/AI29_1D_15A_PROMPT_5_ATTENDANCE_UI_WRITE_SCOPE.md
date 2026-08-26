# AI29.1D.15A Prompt 5 — Attendance UI Write Scope

## Contract

| Concern | UI responsibility |
|---------|-------------------|
| Selected scope | Send optional request-level `sectionId` / `sectionIds` via `buildAttendanceSaveScope` |
| Student rows | `{ studentNumber, status }` **only** — never per-student section fields |
| Eligibility | **Server only** — do not filter submitted students in React as security |
| Timetable | Prefill from `/attendance-resolution/current` only — no second resolver |
| Manual | Course → Group → Semester → Subject → Period without Program / Section / Timetable |

## Builder

`buildAttendanceWritePayload` — used for both mark and edit.

## Files

- `abhyanvaya-ui/src/utils/attendanceMarkingScope.ts` (+ tests)
- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx`
- `docs/AI29_1D_15A_PROMPT_5_ATTENDANCE_UI_WRITE_SCOPE.md`

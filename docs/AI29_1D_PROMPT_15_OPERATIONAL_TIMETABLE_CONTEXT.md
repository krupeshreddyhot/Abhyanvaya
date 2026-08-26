# AI29.1D Prompt 15 — Operational Timetable Context UI

## Goal

Expose timetable/session context in operational attendance UI using **existing** `/attendance-resolution/current` — no parallel React timetable resolver.

## Fields (when timetable-derived)

Program · Course · Group · Semester · Section / SectionGroup · Subject · Period · Room · Date

Program is shown when Programs are enabled and a course→program mapping exists (optional).

## Source distinction

| Source | When |
|--------|------|
| **Timetable-derived context** | Resolver returns `mode=Timetable` + `hasTimetable` |
| **Manually selected context** | No timetable assignment, or user changed academic fields after prefill |

## Graceful fallback

When timetable context is unavailable:

- Do **not** show “Attendance unavailable because timetable is not assigned.”
- Keep the manual Course → Group → Semester → Subject → Period workflow
- Banner: *Timetable is not required*

## UI

- `OperationalTimetableContextPanel` on Mark Attendance
- Helpers: `operationalTimetableContext.ts` (maps session DTO only)
- Faculty Workspace Today tab uses the same Timetable-derived vs Manual wording

## Files

- `abhyanvaya-ui/src/utils/operationalTimetableContext.ts` (+ tests)
- `abhyanvaya-ui/src/components/attendance/OperationalTimetableContextPanel.tsx`
- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx`
- `abhyanvaya-ui/src/pages/faculty/FacultyWorkspacePage.tsx`
- `abhyanvaya-ui/src/utils/attendanceMarkingScope.ts` (+ tests)
- `docs/AI29_1D_PROMPT_15_OPERATIONAL_TIMETABLE_CONTEXT.md`

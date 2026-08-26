# AI29.1D Prompt 11A — Attendance Section Scope Hardening

## Objectives

Harden Prompt 11 Attendance UI integration without redesigning `AttendanceSessionResolver`.

## 1. Academic Year Section Scope

Section options load as:

`Academic Year (IsCurrent) → Course → Group → Semester → Section`

UI reuses `listAcademicYears()` + `listSections({ academicYearId, courseId, groupId, semesterId })`.
No second academic-year resolver.

## 2. Timetable Context Staleness

When Timetable mode is active and the user changes Course / Group / Semester / Subject / Period:

- Mode switches to **Manual**
- Resolver-owned Section / Room / section codes are cleared
- Section becomes an optional manual filter again

## 3. Program Context

Program remains **optional**. When `enablePrograms` and a course→program mapping exists, Program is shown as a contextual chip only. Subject is never dependent on Section. Manual cascade unchanged.

## 4–6. Server Filtering / Save / Security

`AttendanceSectionScope` (Application) consolidates the existing StudentSections filter and adds scope validation:

- Section A only → only A students
- A + B → students in A or B
- No section → full Course/Group/Semester cohort (legacy)
- Out-of-scope SectionId (wrong C/G/S or other academic year) → `400 BadRequest`
- Faculty subject authorization (`FacultySubjectAccess`) remains authoritative
- Mark/Edit save contract unchanged — payload is the loaded roster

## Architecture confirmation

| Constraint | Status |
|------------|--------|
| No second Attendance Resolver | ✓ |
| AttendanceSessionResolver unmodified | ✓ |
| Timetable not mandatory | ✓ |
| Manual cascade retained | ✓ |
| Subject Master unchanged | ✓ |

## Files changed

- `Abhyanvaya.Application/Academic/AttendanceSectionScope.cs` (new)
- `Abhyanvaya.API/Controllers/AttendanceController.cs`
- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx`
- `abhyanvaya-ui/src/utils/attendanceMarkingScope.ts`
- `abhyanvaya-ui/src/utils/attendanceMarkingScope.test.ts`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_Prompt11A_AttendanceSectionScopeTests.cs` (new)
- `docs/AI29_1D_PROMPT_11A_ATTENDANCE_SECTION_SCOPE_HARDENING.md` (new)

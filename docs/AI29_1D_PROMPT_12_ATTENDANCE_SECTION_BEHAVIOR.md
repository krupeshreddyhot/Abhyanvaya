# AI29.1D Prompt 12 — Attendance Section Behavior

## Rules

1. **Subject Master** remains `Course + Group + Semester` (never Section).
2. **Section** determines the operational **student population** only.
3. Section must **not** redefine Subject Master.
4. Section A selected → population = students currently assigned to Section A (`StudentSections`).
5. Section B selected → population = students currently assigned to Section B.
6. **No Section** → preserve legacy `Course + Group + Semester + Subject + Period` full cohort.
7. Timetable combined / SectionGroup class → consume `AttendanceSessionResolutionDto.SectionIds` / `SectionCodes` from the existing resolver contract (TimetableSections expansion on the server). **Do not** reimplement SectionGroup logic in the frontend.

```
Section A + Section B
        ↓
One attendance session (sectionIds = [A, B])
        ↓
Students from A ∪ B
```

## Implementation (consumes Prompts 11 / 11A / 11B)

| Layer | Behavior |
|-------|----------|
| UI | Subjects loaded by C/G/S only; optional Section multi-select; timetable prefills `sectionIds` from `/attendance-resolution/current` |
| Roster API | `GET /attendance/students-for-marking` optional `sectionId` / `sectionIds[]` → `AttendanceSectionScope` + `StudentSections` |
| AY authority | Section filter requires ExactlyOne current Academic Year (Prompt 11B); legacy no-section path does not |

## Files

- `abhyanvaya-ui/src/utils/attendanceSectionBehavior.ts` (+ tests)
- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx` (Subject/Section helper copy)
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_Prompt12_AttendanceSectionBehaviorTests.cs`
- `docs/AI29_1D_PROMPT_12_ATTENDANCE_SECTION_BEHAVIOR.md`

Related (unchanged architecture): `AttendanceSectionScope`, `AttendanceController.GetStudentsForMarking`, `AttendanceSessionResolver` (consumed, not redesigned).

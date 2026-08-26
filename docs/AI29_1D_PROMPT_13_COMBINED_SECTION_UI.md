# AI29.1D Prompt 13 — Combined Section UI

## Goal

Present single and combined section attendance as **one operational class** while preserving underlying Section membership for reporting.

## Supported UI shapes

| Mode | Display |
|------|---------|
| Single | `Section A` |
| Combined | `Section A + B` or `A + B + C` |

Source of truth: existing **TimetableSections** / resolver `sectionIds` + `sectionCodes` (and manual multi-select). **No** second combined-section model.

## Behavior

1. **Timetable-driven** — prefills participating sections from `/attendance-resolution/current`; banner shows one operational class.
2. **Attendance population** — `students-for-marking` with `sectionIds[]` returns students from all participating sections (Prompt 12 / `AttendanceSectionScope`).
3. **Reporting** — each student row includes additive `sectionId` / `sectionCode` from current `StudentSections`.
4. **Persistence** — `mark` / `edit` contracts unchanged (subject + date + student statuses).

## Additive API fields (`GET students-for-marking`)

- Envelope: `isCombinedClass`, `participatingSectionIds`, `participatingSectionCodes`, `operationalClassLabel`
- Student: `sectionId`, `sectionCode`

## UI pieces

- `CombinedSectionClassBanner` — operational class + membership chips
- Roster table/card **Section** column when section identity is relevant

## Files

- `Abhyanvaya.API/Controllers/AttendanceController.cs`
- `abhyanvaya-ui/src/components/attendance/CombinedSectionClassBanner.tsx`
- `abhyanvaya-ui/src/utils/combinedSectionClass.ts` (+ tests)
- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx`
- `abhyanvaya-ui/src/services/attendanceService.ts`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_Prompt13_CombinedSectionUiTests.cs`
- `docs/AI29_1D_PROMPT_13_COMBINED_SECTION_UI.md`

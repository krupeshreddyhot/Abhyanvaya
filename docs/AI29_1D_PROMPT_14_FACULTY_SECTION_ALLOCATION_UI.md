# AI29.1D Prompt 14 — Faculty Allocation in Section Management UI

## Goal

Integrate faculty allocation into Section management without inventing a second faculty↔section model.

## Columns

| Column | Source |
|--------|--------|
| Section | `FacultySectionDto.sectionCode` (or combined `A + B` from SectionGroup overlap) |
| Faculty | `FacultySectionDto.facultyName` / `facultyId` |
| Subject | Enriched from existing **Subject Allocations** (staff + AY + Course/Group/Semester) |
| Effective From / To | `FacultySectionDto` |
| Allocation Status | Derived: Current / Ended / Inactive |

## APIs reused

- `GET/POST /api/faculty-sections`
- `GET /api/section-groups`
- `GET /api/scheduling/subject-allocations`
- Subjects via master/subjects for names

## Combined SectionGroup

When the same faculty has current assignments on 2+ members of one SectionGroup, the UI shows one operational row (`Combined · A + B`) while retaining underlying assignment ids. No new combined faculty model.

## Timetable / attendance compatibility

Attendance remains timetable-driven (`AttendanceSessionResolver` StaffId + TimetableSections). FacultySectionAssignment is operational ownership for Section management / readiness — scheduling engine unchanged.

## UI

- `FacultySectionAllocationPanel` on `SectionsPage` → Faculty Allocation tab
- Helpers: `facultySectionAllocationView.ts`

## Files

- `abhyanvaya-ui/src/components/sections/FacultySectionAllocationPanel.tsx`
- `abhyanvaya-ui/src/utils/facultySectionAllocationView.ts` (+ tests)
- `abhyanvaya-ui/src/services/sectionService.ts` (`listSectionGroups`)
- `abhyanvaya-ui/src/pages/setup/SectionsPage.tsx`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_Prompt14_FacultySectionAllocationUiTests.cs`
- `docs/AI29_1D_PROMPT_14_FACULTY_SECTION_ALLOCATION_UI.md`

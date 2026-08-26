# AI29.1D — Combined Section UI

Present single and multi-section teaching as **one operational class** while preserving per-student Section membership for reporting.

## Hard rules

- **Section is an operational student grouping and is not part of Subject Master.**
- Combined class identity comes from existing **TimetableSections** / resolver `sectionIds` (+ optional manual multi-select) and **SectionGroup** for faculty display.
- React must **not** invent a second combined-section model or SectionGroup resolver.
- AttendanceSessionResolver remains the single session authority for timetable-driven section lists.

## UI shapes

| Mode | Display |
|------|---------|
| Single | `Section A` |
| Combined | `Section A + B` or `A + B + C` (operational class label) |

Banner: `CombinedSectionClassBanner` — one class title + membership chips.

## Timetable-driven path

1. `GET /api/attendance-resolution/current` returns participating section ids/codes from TimetableSections.
2. Attendance UI prefills multi-section scope and shows one operational class.
3. Roster via `students-for-marking?sectionIds[]` returns the union of memberships.

## Manual multi-select path

Operators may select multiple sections for the same Course/Group/Semester/Subject/Period session.  
Server scope is the **OR** (union) of student memberships — same as timetable combined.

## Additive roster contract

`GET /api/attendance/students-for-marking` may include:

| Field | Level |
|-------|--------|
| `isCombinedClass` | Envelope |
| `participatingSectionIds` / `participatingSectionCodes` | Envelope |
| `operationalClassLabel` | Envelope |
| `sectionId` / `sectionCode` | Per student (current StudentSection) |

Mark/edit persistence remains subject + date + student statuses with optional section scope arrays (15A) — no parallel “combined attendance” entity.

## Faculty allocation combined display

On Sections → Faculty Allocation:

- SectionGroup membership surfaces as one operational class (e.g. “Combined · A + B”).
- Still backed by `/api/section-groups` + `/api/faculty-sections` — no new relationship table.

## API contracts consumed

| Endpoint | Role |
|----------|------|
| `/api/attendance-resolution/current` | Participating sections for session |
| `/api/attendance/students-for-marking` | Combined roster envelope |
| `/api/attendance/mark`, `/api/attendance/edit` | Optional multi-section save scope |
| `/api/timetable/{id}/sections` | TimetableSections admin |
| `/api/section-groups` | Combined membership for faculty UI |

## Backward compatibility

- Single-section and no-section clients unchanged.
- Additive envelope fields are optional for older UIs.
- No mandatory timetable for combined manual multi-select.

## Security & integrity

- Multi-section writes authorized via `AttendanceSaveScope` / `AttendanceSectionScope`.
- Every student in the payload must belong to the authorized union; else atomic 4xx.

## Performance & responsive

- One roster request for the union — not N per-section fetches when the API accepts `sectionIds[]`.
- Banner + chips densified for mobile; horizontal chip scroll inside panels (Prompt 17).

## Primary files

- `CombinedSectionClassBanner.tsx`, `combinedSectionClass.ts`
- `AttendanceMarking.tsx`, `attendanceSectionBehavior.ts`
- `FacultySectionAllocationPanel.tsx` (combined faculty rows)
- Prompt docs: `AI29_1D_PROMPT_13_COMBINED_SECTION_UI.md`, `AI29_1D_15A_PROMPT_8_COMBINED_FACULTY_ALLOCATION.md`

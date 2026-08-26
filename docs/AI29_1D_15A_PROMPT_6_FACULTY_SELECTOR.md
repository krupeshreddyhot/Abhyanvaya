# AI29.1D.15A Prompt 6 — Enterprise Faculty Selector

## Change

Sections → Faculty Allocation: replace numeric **Faculty (Staff) Id** with:

**Faculty** → Search / Select Faculty (existing `GET /api/staff`)

## Architecture

| Rule | Implementation |
|------|----------------|
| Authoritative source | `Staff` / `listStaff` (`/api/staff`) |
| No new Faculty entity | Reuses `StaffListItem` |
| No new Faculty–Section model | Still `POST /api/faculty-sections` with `facultyId` |
| Display | Name + Staff ID (`staffCode` or `Staff #id`) |
| Submit | Numeric Staff Id only (`facultyIdForAssign`) |
| Authorization | API returns only staff the admin may list; 403 surfaced with Retry |
| Pagination / search | `pageSize=25` + debounced `search` — no full dump |
| Preserved | Effective From/To, Subject Allocation enrichment, Combined SectionGroup rows |

## Files

- `abhyanvaya-ui/src/components/sections/FacultyStaffSelector.tsx`
- `abhyanvaya-ui/src/components/sections/FacultySectionAllocationPanel.tsx`
- `abhyanvaya-ui/src/utils/facultyStaffSelector.ts` (+ tests)
- `docs/AI29_1D_15A_PROMPT_6_FACULTY_SELECTOR.md`

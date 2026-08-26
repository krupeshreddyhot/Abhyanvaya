# AI29.1D Prompt 19 — UI Performance

## Goal

Keep AI29.1D usable on enterprise datasets without dumping full Students / Subjects / Sections / Faculty / Timetable catalogs into the browser, and without bypassing hierarchy / Allocation Context / attendance scope contracts.

## Patterns applied

| Pattern | Where |
|---------|--------|
| Cascading queries + scoped caches | `AcademicUiContext` sections/subjects (keyed by Year+C/G/S or C/G/S); Attendance reuses AcademicUi options when scope matches |
| Server-side filtering + pagination | Faculty `listStaff` search pages; Attendance `students-for-marking` pageSize 50 / fetch 200 |
| Existing cache contracts | Allocation Context remains the student population source; AcademicUi section/subject maps; short-lived attendance roster cache (30s) for Mark-all + Save |
| Debounced search | Attendance search (300ms); FacultyStaffSelector (300ms) |
| Memoized / windowed selectors | `countPopulationFilter`, `takePopulationFilter`, `countUnassignedMatches`; preview `maxRows` |
| Cancel obsolete requests | `AbortSignal` on AcademicUi sections/subjects/faculty, Attendance loads, FacultyStaffSelector, Faculty panel load |
| Avoid N+1 | Sections list no longer calls `getSectionVersions` per row; Faculty panel one `listFacultySections({ currentOnly: true })` then client-scope to visible sections |

## Allocation (thousands of students)

- Engine still receives `populationSelection` criteria (not a browser-built student id dump for filtering UX).
- UI chips/tables use counts + windows (100 / 150) instead of materializing full filtered arrays every render.
- Preview rows capped via `buildAllocationPreviewRows(..., { maxRows })`.

## Non-goals / guards

- Do not invent a second Students API for allocation filtering.
- Do not expand hierarchy GETs with `includeSections` / `includeSubjects` for UI convenience.
- Server authorization and save-scope rules remain authoritative (Prompt 18 / 15A).

## Key files

- `abhyanvaya-ui/src/utils/academicRequest.ts`
- `abhyanvaya-ui/src/utils/allocationPopulationFilter.ts`
- `abhyanvaya-ui/src/utils/allocationPreviewSummary.ts`
- `abhyanvaya-ui/src/context/AcademicUiContext.tsx`
- `abhyanvaya-ui/src/components/allocation/EnterpriseAllocationWorkspace.tsx`
- `abhyanvaya-ui/src/components/allocation/AllocationPreviewPanel.tsx`
- `abhyanvaya-ui/src/components/allocation/StudentPopulationFilterPanel.tsx`
- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx`
- `abhyanvaya-ui/src/pages/setup/SectionsPage.tsx`
- `abhyanvaya-ui/src/components/sections/FacultySectionAllocationPanel.tsx`
- `abhyanvaya-ui/src/components/sections/FacultyStaffSelector.tsx`
- Service signal plumbing: `sectionService`, `attendanceService`, `setupService`

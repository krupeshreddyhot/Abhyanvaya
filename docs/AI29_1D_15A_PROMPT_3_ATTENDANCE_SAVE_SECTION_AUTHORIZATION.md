# AI29.1D.15A Prompt 3 — Attendance Save Section Authorization

## Security boundary

| Layer | Trust |
|-------|--------|
| UI / client `sectionId` / `sectionIds` / student list | **Untrusted** |
| Subject row (Course / Group / Semester) | Authoritative academic anchor for write scope |
| `FacultySubjectAccess` | Authoritative subject access (Forbid before section checks) |
| `AttendanceSectionScope` / `AttendanceSaveScope` | Authoritative section + StudentSections membership |
| `AttendanceSessionResolver` | **Not used on write** (unchanged) |

A client **cannot** successfully mark/edit by sending unauthorized `sectionIds` or out-of-section student numbers when section scope is present. Writes fail closed (`BadRequest`) with **no partial persist**.

## When `sectionId` / `sectionIds` are supplied

1. Normalize via existing `AttendanceSectionScope.NormalizeRequestedIds` (through `AttendanceSaveScope`).
2. Resolve authoritative current Academic Year (11B): require **exactly one** `IsCurrent`.
3. Validate every section against **Tenant + Academic Year + Course + Group + Semester** (C/G/S from the **subject**, not the client).
4. Restrict students with `StudentSections` (`ApplyAuthorizedSectionFilter`).
5. Require **every** submitted student number ∈ authorized set; otherwise reject the entire request.

## When no Section is supplied

- Legacy mark/edit path preserved.
- Academic Year **not** required.
- No section membership fail-closed check (existing cohort behavior).

## Faculty authorization

`AttendanceController` mark/edit continue to call `FacultySubjectAccess.FacultyMayAccessSubjectAsync` **before** section validation. Unauthorized faculty → `Forbid()`; section scope is never applied as a bypass.

## APIs

- `POST /api/attendance/mark`
- `PUT /api/attendance/edit`

## Key types

- `AttendanceSaveScope.ValidateWriteSectionScopeAsync`
- `AttendanceSaveScope.ApplyAuthorizedSectionFilter`
- `AttendanceSaveScope.EnsureAllSubmittedStudentsAuthorized`
- Reuses `AttendanceSectionScope` (no second section validation service)

## Error messages (reuse)

- `AttendanceSectionScope.NoCurrentAcademicYearMessage`
- `AttendanceSectionScope.MultipleCurrentAcademicYearsMessage`
- `AttendanceSectionScope.SectionOutOfScopeMessage`
- `AttendanceSaveScope.UnauthorizedStudentsMessage`

## Tests

`AI29_1D_15A_Prompt3_AttendanceSaveSectionAuthorizationTests.cs`:

- valid Section  
- wrong tenant / academic year / course / group / semester  
- unauthorized faculty (controller wiring: FacultySubjectAccess before section scope)  
- multiple valid Sections  
- invalid Section  
- no Section legacy (no AY)  
- out-of-section students fail closed  

## Files

- `Abhyanvaya.Application/Academic/AttendanceSaveScope.cs`
- `Abhyanvaya.API/Controllers/AttendanceController.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt3_AttendanceSaveSectionAuthorizationTests.cs`
- `docs/AI29_1D_15A_PROMPT_3_ATTENDANCE_SAVE_SECTION_AUTHORIZATION.md`

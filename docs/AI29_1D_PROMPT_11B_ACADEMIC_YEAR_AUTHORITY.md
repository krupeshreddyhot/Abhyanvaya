# AI29.1D Prompt 11B — Academic Year Authority Hardening

## Objective

Make Academic Year selection **deterministic and fail-closed** for Section-scoped Attendance, without breaking legacy attendance when Section is omitted.

## Authority rules

| Current years (`IsCurrent=true`) | Section-scoped Attendance |
|----------------------------------|---------------------------|
| Exactly one | Use that Academic Year |
| None | Do not guess — Section disabled / configuration error |
| Multiple | Do not guess — Section disabled / configuration error + warning log |

Legacy (no `sectionId` / `sectionIds`): **no Academic Year requirement**. Course → Group → Semester → Subject → Period continues to work.

## Server

`AttendanceSectionScope.ResolveAuthoritativeCurrentAcademicYearAsync`  
`AttendanceSectionScope.ValidateSectionIdsAsync` (when section ids supplied):

- Require ExactlyOne current year
- Validate Section against Tenant + Academic Year + Course + Group + Semester
- Multiple current years → `LogWarning` via controller `ILogger` + clear configuration error
- Never silently drop the Academic Year condition

## UI

`resolveAuthoritativeAcademicYear` (replaces guessing first year):

- ExactlyOne → enable Section selector + year-scoped `listSections`
- None → disable Section; show “Current academic year is not configured.”
- Multiple → disable Section; show configuration warning
- Effective roster params omit section filters when authority is not ExactlyOne

## Architecture confirmation

- AttendanceSessionResolver unmodified / not redesigned
- Subject Master / Scheduling / Allocation engines untouched
- Manual cascade without Section preserved

## Files changed

- `Abhyanvaya.Application/Academic/AttendanceSectionScope.cs`
- `Abhyanvaya.API/Controllers/AttendanceController.cs`
- `abhyanvaya-ui/src/utils/attendanceMarkingScope.ts`
- `abhyanvaya-ui/src/utils/attendanceMarkingScope.test.ts`
- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_Prompt11B_AcademicYearAuthorityTests.cs` (new)
- `docs/AI29_1D_PROMPT_11B_ACADEMIC_YEAR_AUTHORITY.md` (new)

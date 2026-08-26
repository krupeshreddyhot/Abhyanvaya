# AI29.1D.15A Prompt 2 — Attendance Save Scope Contract

## Goal

Smallest additive contract so optional Section scope can travel on mark/edit requests.  
**Authorization / fail-closed student-set validation is deferred** (later 15A prompt).

## Additive fields

On `MarkAttendanceRequest` and `EditAttendanceRequest`:

| Property | Type | Required |
|----------|------|----------|
| `sectionId` | `int?` | No — convenience for single section |
| `sectionIds` | `List<int>?` / `number[]` | No — one or many participating sections |

JSON (ASP.NET camelCase): `sectionId`, `sectionIds`.

Existing clients that omit both fields continue to work (legacy full cohort).

## Rules

| Case | Client payload | Normalized scope (`AttendanceSaveScope`) |
|------|----------------|------------------------------------------|
| No Section | omit both / empty `sectionIds` | `[]` — legacy behavior |
| Single Section | `sectionIds: [id]` (and optionally `sectionId`) | `[id]` |
| Combined Section | `sectionIds: […all participating…]` | distinct positive ids |
| Empty array | `sectionIds: []` | `[]` |
| Duplicates / ≤0 | e.g. `[11,11,0]` | Distinct positive ids (**normalize**, do not reject) — same as `AttendanceSectionScope.NormalizeRequestedIds` |

## Normalization

`Abhyanvaya.Application/Academic/AttendanceSaveScope.cs` delegates to existing `AttendanceSectionScope.NormalizeRequestedIds`:

- drop `id <= 0`
- `Distinct()`
- merge `sectionId` into the list when `> 0`

## UI

- `buildAttendanceSaveScope(selectedSectionIds)` mirrors roster attachment
- `AttendanceMarking` spreads scope into `markAttendance` / `editAttendance` payloads
- Timetable and Section remain **optional**

## Non-goals (this prompt)

- No write-time authorization / StudentSections enforcement yet
- No second eligibility model or AttendanceSessionResolver
- No Subject Master / DB schema change
- No combined-class write fields (`isCombinedClass` stays on roster response only)

## Files

- `Abhyanvaya.Application/DTOs/MarkAttendanceRequest.cs`
- `Abhyanvaya.Application/DTOs/public class EditAttendanceRequest.cs`
- `Abhyanvaya.Application/Academic/AttendanceSaveScope.cs`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt2_AttendanceSaveScopeContractTests.cs`
- `abhyanvaya-ui/src/utils/attendanceMarkingScope.ts` (+ tests)
- `abhyanvaya-ui/src/services/attendanceService.ts`
- `abhyanvaya-ui/src/pages/AttendanceMarking.tsx`
- `docs/AI29_1D_15A_PROMPT_2_ATTENDANCE_SAVE_CONTRACT.md`

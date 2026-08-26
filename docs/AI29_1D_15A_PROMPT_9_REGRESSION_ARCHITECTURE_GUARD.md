# AI29.1D.15A Prompt 9 — Regression & Architecture Guard

Verification report for Attendance save-scope integrity + Faculty Allocation UX.

## Architecture guard result — PASS

| Guard | Result |
|-------|--------|
| No second AttendanceSessionResolver | PASS |
| No second SectionGroup model | PASS |
| No Subject Master ↔ Section relationship | PASS |
| No new FacultySection entity (uses FacultySectionAssignment) | PASS |
| No parallel Allocation / scoring engine | PASS |
| Mark/Edit optional section contract additive | PASS |
| Atomic unauthorized-student reject | PASS |
| Faculty selector replaces numeric Staff Id UI | PASS |
| Assign API still posts `facultyId` | PASS |

## Attendance invariants (covered by 15A + 11–15 tests)

1. Manual Course→Group→Semester→Subject→Period without timetable — UI/scope helpers + legacy no-section path  
2. Edit attendance — same write contract / controller edit path  
3. No Section — empty normalize → legacy cohort  
4. Section A — scope + StudentSections membership  
5. A+B — combined sectionIds membership (OR)  
6. Unauthorized Section — `SectionOutOfScopeMessage`  
7. Unauthorized student injection — fail-closed  
8. Partial invalid (99+1) → 0 committed  
9. Timetable mode — single `AttendanceSessionResolver`  
10. Combined timetable — TimetableSections / SectionGroup unchanged  

## Faculty Allocation invariants

1. Faculty selector (`FacultyStaffSelector` + `/api/staff`)  
2. Manual Staff Id field removed from normal UI  
3. `AssignFacultySectionRequest.FacultyId` compatible  
4. Unauthorized faculty rejected (`FacultySectionAssignmentAuthorization`)  
5. Combined SectionGroup display (`Combined · A + B` + underlying ids)  

## Database changes

**None** for AI29.1D.15A (no migrations introduced by 15A prompts).

## APIs changed (additive / behavior-hardening only)

| API | Change |
|-----|--------|
| `POST /api/attendance/mark` | Optional `sectionId`/`sectionIds`; section-scoped atomic student auth |
| `PUT /api/attendance/edit` | Same optional scope + student auth before mutate |
| `POST /api/faculty-sections` | Stronger server validation (contract unchanged) |
| `GET /api/staff` | Unchanged (selector consumer) |
| `GET /api/attendance-resolution/current` | Unchanged |

## Builds / tests (executed)

| Suite | Result |
|-------|--------|
| API build (`Abhyanvaya.API`) | **PASS** (warnings only: NU190x / CS8602 preexisting) |
| UI build (`abhyanvaya-ui` tsc + vite) | **PASS** |
| `FullyQualifiedName~AI29` | **274 passed / 0 failed** |
| `FullyQualifiedName~AI29_1D` | **136 passed / 0 failed** |
| AI29.1D.15A Prompt 9 guards | **11 passed / 0 failed** |
| AI22 / AttendanceRecovery + AI31 + Optimization filter | **145 passed / 0 failed** |
| `FullyQualifiedName~Scheduling` (AI30-related) | **165 passed / 0 failed** |
| UI vitest (15A scope/selector/combined faculty) | **35 passed / 0 failed** |

## Files changed this prompt (Prompt 9)

- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt9_RegressionArchitectureGuardTests.cs` (new)
- `docs/AI29_1D_15A_PROMPT_9_REGRESSION_ARCHITECTURE_GUARD.md` (new)

No production code changes in Prompt 9 (verification only).

## 15A production surface (Prompts 1–8) — summary

**Created:** `AttendanceSaveScope`, `FacultySectionAssignmentAuthorization`, UI write-payload helpers, `FacultyStaffSelector`, combined faculty view helpers, docs + tests.

**Modified:** `AttendanceController` mark/edit (optional section + atomic student auth), `MarkAttendanceRequest` / `EditAttendanceRequest`, `SectionManagementService.AssignFacultyAsync`, Attendance Marking page, Faculty Allocation panel, attendance/section services.

**APIs:** Additive optional `sectionId`/`sectionIds` on mark/edit; faculty-sections POST contract unchanged (stronger validation).

**Database:** No 15A migrations.

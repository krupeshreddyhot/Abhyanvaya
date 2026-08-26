# AI29.1D Prompt 20 — Regression & Compatibility

Dedicated regression suite for AI29.1D. **Does not change production business logic to make tests pass.** Fixture updates are allowed only for additive contract changes.

## Suite locations

| Layer | Path |
|-------|------|
| C# (cases 1–36) | `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_Prompt20_RegressionSuiteTests.cs` |
| UI companion (hierarchy + attendance cascade) | `abhyanvaya-ui/src/utils/ai29_1d_prompt20_regression.test.ts` |
| Existing cascade matrix (referenced) | `abhyanvaya-ui/src/utils/academicCascade.test.ts` |
| Existing attendance scope (referenced) | `abhyanvaya-ui/src/utils/attendanceMarkingScope.test.ts` |

Filter: `dotnet test --filter FullyQualifiedName~AI29_1D_Prompt20`

## Mandatory case inventory

### Academic Hierarchy

| Case | Title | Primary assertion |
|------|-------|-------------------|
| Case 1: | Program enabled | Breadcrumb includes Program when EnablePrograms |
| Case 2: | Program disabled | Breadcrumb omits Program |
| Case 3: | Course filtering | `filterCoursesForProgram` + AcademicUi hierarchy fail-closed |
| Case 4: | Group filtering | `filterGroupsForCourse` |
| Case 5: | Semester filtering | `filterSemestersForCourseGroup` |
| Case 6: | Section filtering | `filterSectionsForScope` (Year + C/G/S) |
| Case 7: | Subject filtering | Subjects keyed by Course+Group+Semester only |
| Case 8: | Section does not alter Subject Master | Subject entity has no SectionId; cascade keeps Subject on Section change |

### Attendance

| Case | Title | Primary assertion |
|------|-------|-------------------|
| Case 9: | Faculty with timetable | Resolution Mode=Timetable, HasTimetable |
| Case 10: | Faculty without timetable | Mode=Legacy, manual path message |
| Case 11: | Manual C→G→S→Subject→Period | Legacy resolution carries curriculum + period |
| Case 12: | Manual attendance with Section | `AttendanceSaveScope` single section |
| Case 13: | Manual attendance without Section | Empty scope = legacy full cohort |
| Case 14: | Combined Section attendance | `IsCombinedSection` for multi ids |
| Case 15: | Timetable Section attendance | Timetable SectionIds → save scope |

### Allocation

| Case | Title | Primary assertion |
|------|-------|-------------------|
| Case 16: | Student Number Range | Population range + `StudentNumberRange` grouping |
| Case 17: | Last 3 digits | Deterministic `LastThreeDigits` order |
| Case 18: | Alphabetical | Name order |
| Case 19: | Gender | Grouping + population facet |
| Case 20: | Merit | Grouping + population facet |
| Case 21: | Scholarship | Grouping + `ScholarshipCategory` population |
| Case 22: | Minor | `MinorSubject` grouping + population |
| Case 23: | Language | Grouping + population facet |
| Case 24: | Transport | Grouping + `TransportRoute` population |
| Case 25: | Hostel | Grouping + population facet |
| Case 26: | Elective Combination | Grouping + population facet |
| Case 27: | Capacity violation | Mandatory Capacity unsatisfied |
| Case 28: | Preview | Engine scenario + draft “not modified” note |
| Case 29: | Simulation | `Simulate` audit + Simulated lifecycle |
| Case 30: | Scenario creation | `CreateScenario` / Generated lifecycle |
| Case 31: | Approval | Reviewed → Approved |
| Case 32: | Rejection | Reviewed → Rejected; Rejected ↛ Approved |
| Case 33: | Archive | Approved → Archived + Archive permission |
| Case 34: | Stale context | `ContextStale` blocks approve |
| Case 35: | Checksum failure | Canonical checksum diverge + `ChecksumInvalid` |
| Case 36: | Concurrency conflict | RowVersion + concurrency message/flag |

## Compatibility notes

- Population facet modes resolve **only** against `SectionAllocationContext` (fabricated facets in tests; live builder may leave some facets null → Unavailable).
- Hierarchy Course/Group/Semester/Section filter truth for UI remains `academicCascade.ts` — Prompt 20 guards presence + UI companion cases.
- Timetable is never mandatory for attendance; Section remains optional on mark/edit.
- Governance flags (`ContextStale`, `ChecksumInvalid`, `ConcurrencyConflict`) are authoritative server contracts; UI only surfaces them.

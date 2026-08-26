# AI29.1D — Test Strategy

How AI29.1D is verified without changing production business logic solely to make tests pass. Fixture updates are allowed only for additive contracts.

## Goals

1. Protect hierarchy / Program flag / Section vs Subject Master invariants.
2. Protect attendance timetable + manual + section scope + atomic save integrity.
3. Protect allocation population strategies, capacity, preview/simulate, and governance lifecycle.
4. Enforce UI → API → Domain architecture (Prompt 21 / 21A).
5. Keep UI cascade helpers aligned with server authority.

## Explicit invariant under test

**Section is an operational student grouping and is not part of Subject Master.**  
(Prompt 20 Case 8; Subject has no `SectionId`; cascade keeps Subject when Section changes.)

## Suite map

| Suite | Location | Focus |
|-------|----------|--------|
| **Prompt 20 regression** | `AI29_1D_Prompt20_RegressionSuiteTests.cs` + `ai29_1d_prompt20_regression.test.ts` | Mandatory cases 1–36 |
| **Prompt 21 guard** | `AI29_1D_Prompt21_ArchitectureGuardTests.cs` | Layering + UI scan + snapshot |
| **Prompt 21A hardening** | `AI29_1D_Prompt21A_ArchitectureGuardHardeningTests.cs` | `FULLY_VERIFIED` / `PARTIALLY_VERIFIED` / `FAILED` |
| Prompt 16 / 16A | Breadcrumb + OR-permissions + tree consistency | Auth + consistency |
| Prompt 10A–15 / 15A | Matching `AI29_1D_Prompt*` / `AI29_1D_15A_Prompt*` | Scope, attendance, faculty, combined |
| UI unit | `academicCascade`, `attendanceMarkingScope`, combined/faculty helpers | Client composition only |

## Prompt 20 mandatory inventory (summary)

### Hierarchy (1–8)

Program on/off, Course/Group/Semester/Section filters, Subject keyed by C/G/S only, Section does not alter Subject Master.

### Attendance (9–15)

Faculty with/without timetable, manual C→G→S→Subject→Period, with/without Section, combined sections, timetable section ids → save scope.

### Allocation (16–36)

Population strategies (range, last-3 digits, alphabetical, gender, merit, scholarship, minor, language, transport, hostel, elective), capacity violation, preview, simulation, scenario, approve/reject/archive, stale context, checksum failure, concurrency conflict.

## Architecture compliance (CI)

```bash
dotnet test --filter FullyQualifiedName~AI29_1D_Prompt21
```

Machine-readable:

- Report API: `GET /api/v1/academic-structure/architecture/ai29-1d-report`
- Snapshot: `docs/architecture/AI29_1D_architecture_compliance.json`
- Gate on **`Status`**: `FULLY_VERIFIED` | `PARTIALLY_VERIFIED` | `FAILED`  
  (Do not treat `PARTIALLY_VERIFIED` as `FULLY_VERIFIED`. `Passed` is true for both non-failed statuses.)

Latest local snapshot status: **`FULLY_VERIFIED`** (UI scan executed, zero violations).

## Recommended filters

```bash
dotnet test --filter FullyQualifiedName~AI29_1D_Prompt20
dotnet test --filter FullyQualifiedName~AI29_1D_Prompt21
dotnet test --filter FullyQualifiedName~AI29_1D_Prompt16A
dotnet test --filter FullyQualifiedName~AI29_1D
```

UI:

```bash
cd abhyanvaya-ui && npm test -- --run ai29_1d_prompt20_regression
```

## Layers of testing

| Layer | What it proves |
|-------|----------------|
| Domain / Application unit | Engines, save scope, lifecycle, governance flags |
| Architecture guard | No UI EF/DbContext/authority engines; Domain↛Application |
| UI unit | Cascade / scope builders compose contracts correctly |
| Integration (selected 15A) | Atomic write reject paths |

## Compatibility policy

- Prefer asserting existing contracts over inventing new production APIs for tests.
- Timetable never mandatory; Section optional; Program optional for attendance.
- Governance flags (`ContextStale`, `ChecksumInvalid`, `ConcurrencyConflict`) asserted as server contracts.

## Test results (Prompt 22 baseline)

| Gate | Result |
|------|--------|
| Prompt 20 suite | Dedicated cases 1–36 covered by C# + UI companion |
| Prompt 21 + 21A | **17/17 passed** (combined filter `~AI29_1D_Prompt21`) |
| Architecture snapshot | `Status=FULLY_VERIFIED`, `ViolationCount=0` |
| API build | 0 errors (Prompt 21A verification) |
| UI production build | Success (Prompt 21A verification) |

Broader `FullyQualifiedName~AI29_1D` aggregates Prompt 10A–21A / 15A suites (historical Prompt 15A report cited ~136 methods in the AI29_1D filter family — re-run locally for current totals).

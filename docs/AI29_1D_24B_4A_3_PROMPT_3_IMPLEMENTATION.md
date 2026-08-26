# AI29.1D.24B.4A.3 Prompt 3 — Canonical Context Integrity Implementation

**Workstream:** AI29.1D.24B.4A.3  
**Prompt:** 3 — Canonical Context Integrity Implementation & Determinism Tests  
**Date:** 2026-08-16  
**Design basis:** `docs/AI29_1D_24B_4A_3_PROMPT_2_CANONICAL_CONTEXT_DESIGN.md` (approved, not redesigned)

**FINAL STATUS: FULL PASS**

---

## 1. Implementation Summary

Replaced the volatile academic-context checksum (which hashed `ContextId` and live `CurrentStrength`) with **`AllocationAcademicContextIntegrity` v2.0.0**.

Generate → Review now yields identical `ContextChecksum` when academic data is unchanged, even when `ContextId` differs. Governance still compares rebuilt vs stored checksum; stale detection and approval gates were **not** weakened.

---

## 2. Files Changed

| File | Action |
|---|---|
| `Abhyanvaya.Application/Academic/Allocation/AllocationAcademicContextIntegrity.cs` | **Created** |
| `Abhyanvaya.Application/Academic/Allocation/SectionAllocationContextBuilder.cs` | Wired checksum to integrity service; removed volatile private hash body |
| `Abhyanvaya.Application.UnitTests/Academic/AllocationAcademicContextIntegrityTests.cs` | **Created** (determinism + true-drift) |
| `docs/AI29_1D_24B_4A_3_PROMPT_3_IMPLEMENTATION.md` | **Created** (this report) |

**Not changed:** `AllocationCanonicalChecksum`, governance evaluate compare, approval/RBAC, strategies, schema, UI, migrations.

---

## 3. New Integrity Service

`AllocationAcademicContextIntegrity` (static, application-layer single authority):

- `AlgorithmVersion = "2.0.0"`
- `Compute(SectionAllocationContext)`
- `BuildCanonicalPayload(SectionAllocationContext)`
- `ComputePopulationChecksum(IEnumerable<int> studentIds)`
- `ComputeFacultyAssignmentChecksum(IEnumerable<AllocationFacultyProjection>)`

Hash: SHA-256 over UTF-8 canonical JSON → uppercase hex (same convention as scenario checksum).

---

## 4. Canonical Payload Definition

```json
{
  "integrityAlgorithmVersion": "2.0.0",
  "schemaVersion": "...",
  "hierarchy": {
    "academicYearId": 0,
    "programId": null,
    "courseId": 0,
    "groupId": 0,
    "semesterId": 0
  },
  "sections": [
    {
      "sectionId": 0,
      "maximumCapacity": 0,
      "minimumCapacity": 0,
      "reservedSeats": 0
    }
  ],
  "populationChecksum": "...",
  "studentCount": 0,
  "facultyAssignmentChecksum": "..."
}
```

Sections sorted by `SectionId` ASC. Nested digests are uppercase SHA-256 hex.

---

## 5. Fields Included

- `integrityAlgorithmVersion` (`2.0.0`)
- `schemaVersion`
- Hierarchy IDs: AcademicYearId, ProgramId, CourseId, GroupId, SemesterId
- Per section: SectionId, MaximumCapacity, MinimumCapacity, ReservedSeats
- `populationChecksum` + `studentCount` (full `SectionAllocationContext.Students` pool)
- `facultyAssignmentChecksum` (SectionId, FacultyId, Role)

---

## 6. Fields Excluded

`ContextId`, `GeneratedAt`, timestamps, tenant/user/trace/session IDs, names/codes/DisplayOrder/Lifecycle/Health/Readiness, **all occupancy/derived capacity fields** (`CurrentStrength`, `AvailableCapacity`, `OccupancyPercent`, `WaitingList`, `CapacityStatus`, `RecommendedCapacity`), student display/facets/`CurrentSectionId`, faculty names, recommendations, metadata, health/readiness/timetable status, policy prose, **pipeline/config rules** (remain under scenario integrity).

---

## 7. ContextId Handling

- Builder still assigns `ContextId = Guid.NewGuid()` in `BuildCoreAsync`.
- `ContextId` is **not** present in the canonical payload or hash.
- Correlation / session / scenario `ContextId` persistence semantics unchanged.

---

## 8. Population Checksum Implementation

```text
StudentIds → OrderBy ascending → JSON array e.g. [12,45,90] → UTF-8 → SHA-256 → uppercase hex
```

Detects add, remove, and same-count replacement. Uses scope pool students only — not scenario population filters.

---

## 9. Faculty Checksum Implementation

Rows `{ sectionId, facultyId, role }` ordered by SectionId ASC, FacultyId ASC, Role ASC (`StringComparer.Ordinal`). Empty list hashes as `[]`.

---

## 10. Builder Integration

`SectionAllocationContextBuilder.ComputeChecksum` now delegates exclusively to:

```csharp
AllocationAcademicContextIntegrity.Compute(ctx)
```

No duplicated canonicalization in the builder.

---

## 11. Governance Impact

**ContextStale logic was NOT weakened.**  
**Approval gates were NOT bypassed.**

`EvaluateAsync` / `GetDetailAsync` still rebuild context and compare:

```csharp
!string.Equals(current.Checksum, row.ContextChecksum, StringComparison.OrdinalIgnoreCase)
```

Cache is not used as integrity authority.

---

## 12. Scenario Checksum Impact

**`AllocationCanonicalChecksum` was NOT changed.**  
ConfigJson / ScenarioChecksum boundary preserved.

---

## 13. Database Impact

- **No schema changes**
- **No migrations**
- **No historical checksum migration / backfill**

Pre-v2 scenarios may remain stale until regenerated (expected).

---

## 14. Test Results

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| `AllocationAcademicContextIntegrityTests` | **28** | **0** | **0** |
| Allocation regression (`Allocation` / `AI29_1C` / `AI29_1D_Prompt20`) | **174** | **0** | **0** |
| Architecture Guard (`Prompt21` / `ArchitectureGuard`) | **29** | **0** | **0** |
| Allocation-related UI tests (copy / governance lifecycle / UX24B1) | **24** | **0** | **0** |

Mandatory Prompt 3 tests (1–21 + empty faculty + exclusion asserts) are included in the 28 integrity tests — all passed. No mandatory tests skipped.

---

## 15. Architecture Guard

**PASS** — Failed: 0, Passed: 29, Skipped: 0

---

## 16. Build Results

| Build | Result |
|---|---|
| `Abhyanvaya.Application` | **PASS** (0 errors) |
| `Abhyanvaya.API` | **PASS** (0 errors) |
| `abhyanvaya-ui` (`npm run build`) | **PASS** (exit 0) |

### Full UI vitest suite (unchanged files; pre-existing failures)

| | Count |
|---|---:|
| Passed tests | 291 |
| Failed tests | 1 (`allocationStrategyCatalog` expects legacy label `"Student Number (Last 3 Digits)"`) |
| Failed suites | 1 (`dashboardLayoutTokens.test.ts` — empty suite) |

These failures are **outside Prompt 3 scope** (no UI changes in this prompt). Allocation governance UI tests: **24/0/0 PASS**.

---

## 17. Known Limitations

1. Historical scenarios stored under volatile v1 hashes will still show rebuild-required until regenerated under v2.0.0.
2. Occupancy drift no longer invalidates academic context integrity (by design); capacity configuration drift still does.
3. Optional `ContextIntegrityAlgorithmVersion` column deferred (design Phase 3 optional).

---

## 18. Recommendation for Prompt 4

Live Generate → Review acceptance on tenant `001`/`1053`:

1. Restart API with new build.
2. Complete Academic Scope → Generate Allocation for an unchanged scope.
3. Open Review: expect `contextCurrent=true`, `contextStale=false`, Approve enabled if other gates pass.
4. Confirm Refresh Status does not flip-flop checksum solely due to new `ContextId`.
5. Optionally mutate MaximumCapacity or eligible student pool and confirm rebuild required / Approve blocked.

---

## Acceptance Gate Matrix

| Gate | Result |
|---|---|
| AllocationAcademicContextIntegrity implemented | **PASS** |
| Algorithm version 2.0.0 | **PASS** |
| ContextId excluded | **PASS** |
| CurrentStrength excluded | **PASS** |
| Capacity configuration included | **PASS** |
| Population checksum implemented | **PASS** |
| Faculty checksum implemented | **PASS** |
| Canonical section ordering | **PASS** |
| Canonical student ordering | **PASS** |
| Deterministic serialization | **PASS** |
| Builder wired to new service | **PASS** |
| Scenario checksum unchanged | **PASS** |
| Governance unchanged | **PASS** |
| Approval gates unchanged | **PASS** |
| Unit tests | **PASS** (28/0/0) |
| Generate → Review deterministic test | **PASS** |
| True drift test | **PASS** |
| Architecture Guard | **PASS** (29/0/0) |
| API build | **PASS** |
| UI build | **PASS** |
| Allocation UI regression tests | **PASS** (24/0/0) |
| No DB migration | **PASS** |
| No historical backfill | **PASS** |

---

## FINAL STATUS

**FULL PASS** — Canonical academic context integrity v2.0.0 implemented; false ContextId-driven stale condition corrected; governance gates preserved.

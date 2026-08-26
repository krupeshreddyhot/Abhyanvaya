# AI29.1D.24B.4A.3 Prompt 2 — Canonical Allocation Context Integrity Design

**Workstream:** AI29.1D.24B.4A.3 — Allocation Context Consistency & Rebuild-State Diagnostic  
**Prompt:** 2 (Design-First — Implementation Preparation)  
**Date:** 2026-08-16  
**Depends on:** Prompt 1 — `docs/AI29_1D_24B_4A_3_PROMPT_1_CONTEXT_CONSISTENCY_DIAGNOSTIC.md`  

**FINAL STATUS: PASS — canonical design complete and implementation-ready**

---

## 1. Problem Statement

After a successful Generate Allocation, Review/Governance immediately reports that the allocation needs to be rebuilt and blocks Approve.

The server comparison `current.Checksum != row.ContextChecksum` is correct given today’s algorithm. The algorithm itself is wrong: it hashes ephemeral runtime identity (`ContextId`) as if it were academic integrity state.

Governance must continue to block approval when academic/configuration integrity has truly drifted. The fix is to make the integrity checksum **canonical and deterministic**, not to weaken stale detection or approval gates.

---

## 2. Confirmed Root Cause

From Prompt 1 (live-proven):

```text
BuildCoreAsync:  ContextId = Guid.NewGuid()
ComputeChecksum: includes ctx.ContextId

Generate: ContextId=A → Checksum=Hash(A + academic…)
Review:   ContextId=B → Checksum=Hash(B + same academic…)
A ≠ B ⇒ Checksums differ ⇒ ContextStale=true ⇒ CanApprove=false
```

Secondary hazard in the same payload: live `CurrentStrength` is hashed, coupling approval to operational occupancy drift.

---

## 3. Context Identity vs Context Integrity

| Concept | Role | Volatility | Participation in integrity checksum |
|---|---|---|---|
| **A. ContextId** | Runtime / correlation identity for a single build instance (telemetry, UI “context version” prefix, scenario linkage at generate time) | New GUID every `BuildCoreAsync` | **MUST NOT** |
| **B. Canonical Academic Context Checksum** | Deterministic integrity identity of the academic + allocation-governing capacity + eligible population state for a scope | Stable iff academic contract is stable | **Authoritative value stored as `ContextChecksum`** |

### Rules

1. `ContextId` may continue to be assigned with `Guid.NewGuid()` in `BuildCoreAsync`.
2. `ContextId` remains on `SectionAllocationContext` and persisted scenario/session rows for correlation/audit.
3. `ContextId` is **excluded** from the canonical academic checksum payload.
4. Do **not** remove `ContextId` from the context model to “fix” the defect.
5. Optional display field `ContextVersion` (today: first 8 hex of `ContextId`) remains a **correlation hint**, not an integrity proof. Integrity is solely `ContextChecksum` equality under the versioned algorithm.

---

## 4. Canonical Checksum Contract

### Name / version

| Item | Value |
|---|---|
| Contract name | `AllocationAcademicContextIntegrity` |
| Algorithm version | **`2.0.0`** (first canonical academic integrity contract) |
| Hash | SHA-256, hex uppercase (`Convert.ToHexString`) — same platform convention as `AllocationCanonicalChecksum` |
| Legacy (current production) | Implicit **`1.0.0-volatile`** — includes `ContextId` + `CurrentStrength`; not written as a version today |

### Authoritative payload (v2.0.0)

```json
{
  "integrityAlgorithmVersion": "2.0.0",
  "schemaVersion": "<SectionAllocationContext.SchemaVersion>",
  "hierarchy": {
    "academicYearId": 0,
    "programId": null,
    "courseId": 0,
    "groupId": 0,
    "semesterId": 0
  },
  "sections": [
    { "sectionId": 0, "maximumCapacity": 0, "minimumCapacity": 0, "reservedSeats": 0 }
  ],
  "populationChecksum": "<SHA256 hex of canonical ordered eligible StudentIds>",
  "studentCount": 0,
  "facultyAssignmentChecksum": "<SHA256 hex of canonical faculty assignment rows>"
}
```

### Nested digests

**PopulationChecksum**

```text
payload = UTF-8 canonical JSON array of ascending StudentId integers
         e.g. [12,45,90]
PopulationChecksum = SHA256_HEX(payload)
```

**FacultyAssignmentChecksum**

```text
rows ordered by (SectionId ASC, FacultyId ASC, Role ASC Ordinal)
each row: { "sectionId", "facultyId", "role" }
FacultyAssignmentChecksum = SHA256_HEX(canonical JSON array)
```

Empty faculty list ⇒ checksum of `[]`.

### What is intentionally omitted

`ContextId`, `GeneratedAt`, timestamps, tenant/user/trace/session/request IDs, display names, health/readiness/lifecycle display strings, recommendations, policy prose lines, rooms, occupancy/derived capacity fields, pipeline rules (see §9).

---

## 5. Field Classification

| Candidate field | Classification | Reason |
|---|---|---|
| `integrityAlgorithmVersion` | **INCLUDE** | Versions the contract; enables safe historical coexistence |
| `SchemaVersion` | **INCLUDE** | Context schema evolution must invalidate prior hashes |
| Hierarchy `AcademicYearId` | **INCLUDE** | Academic scope identity |
| Hierarchy `ProgramId` | **INCLUDE** | Part of hierarchy projection; null-stable |
| Hierarchy `CourseId` | **INCLUDE** | Academic scope identity |
| Hierarchy `GroupId` | **INCLUDE** | Academic scope identity |
| Hierarchy `SemesterId` | **INCLUDE** | Academic scope identity |
| Hierarchy *Name fields | **EXCLUDE** | Display-only; rename must not force rebuild |
| Ordered `SectionId` set | **INCLUDE** (via `sections[]`) | Section membership is core academic contract |
| `SectionCode` / `SectionName` / `SectionType` | **EXCLUDE** | Labels/type metadata; identity is `SectionId` |
| `DisplayOrder` | **EXCLUDE** | Presentation/placement sort key; reordering alone is not academic membership change. Placement still uses DisplayOrder at run time from live context |
| Section `Lifecycle` / `Health` / `Readiness` | **EXCLUDE** | Operational/derived status, not configuration |
| `MaximumCapacity` | **INCLUDE** | Allocation-governing capacity configuration |
| `MinimumCapacity` | **INCLUDE** | Capacity configuration used by capacity engine/policy surface |
| `RecommendedCapacity` | **EXCLUDE** | Advisory / derived recommendation |
| `ReservedSeats` | **INCLUDE** | Hard capacity = `MaximumCapacity - ReservedSeats` in placement/constraints |
| `CurrentStrength` | **EXCLUDE** | Live occupancy — see §6 |
| `AvailableCapacity` | **EXCLUDE** | Derived from max/reserved/current |
| `OccupancyPercent` | **EXCLUDE** | Derived |
| `WaitingList` | **EXCLUDE** | Operational/live |
| `CapacityStatus` | **EXCLUDE** | Derived/transient status string |
| `StudentCount` | **INCLUDE** | Fast diagnostic companion; must agree with population set size |
| Ordered eligible `StudentId`s → `populationChecksum` | **INCLUDE** | Detects add/remove with unchanged count — see §7 |
| Student numbers/names/facets | **EXCLUDE** from academic checksum | Facet filters live in **pipeline config** (scenario integrity). Context holds the eligible pool for the scope |
| `CurrentSectionId` on students | **EXCLUDE** | Live assignment state; also governed by `ExistingAssignmentPolicy` in config |
| Faculty count only | **EXCLUDE** (replaced) | Count alone misses swap; use assignment checksum |
| Faculty `(SectionId, FacultyId, Role)` → `facultyAssignmentChecksum` | **INCLUDE** | Improves on legacy `FacultyCount`; faculty-section binding is part of composed context |
| Faculty names | **EXCLUDE** | Display |
| Subject assignments | **EXCLUDE** | Not used as placement identity in current engine contract |
| Room availability | **EXCLUDE** | Timetable operational projection |
| Policies string list | **EXCLUDE** | Non-canonical prose; capacities already capture governing numbers |
| Recommendations | **EXCLUDE** | Advisory / regenerated |
| Metadata dictionary | **EXCLUDE** | Includes tenant/diagnostics; volatile |
| OverallHealth / OverallReadiness / TimetableStatus | **EXCLUDE** | Derived |
| `ContextId` | **EXCLUDE** | Ephemeral identity — root-cause field |
| `GeneratedAt` | **EXCLUDE** | Timestamp |
| Pipeline rules / bands / population mode / target sections | **EXCLUDE** from **this** checksum | Protected by scenario `ConfigJson` + `AllocationCanonicalChecksum` — see §9 |
| ScenarioId / SessionId / UserId / TraceId | **EXCLUDE** | Runtime/session identity |

No unresolved **PRODUCT DECISION REQUIRED** items remain for implementation of v2.0.0. Occupancy policy is decided in §6 (exclude from integrity; optional future product flag).

---

## 6. CurrentStrength Decision

### Classification of occupancy-related fields

| Field | Category | Notes |
|---|---|---|
| `MaximumCapacity` | **A. Academic configuration** (+ allocation input) | Configured capacity |
| `MinimumCapacity` | **A. Academic configuration** | Configured floor |
| `ReservedSeats` | **A/C. Configuration / allocation input** | Reduces hard seats for placement |
| `RecommendedCapacity` | **D. Derived/transient** | Advisory |
| `CurrentStrength` | **B. Operational/live state** | Reflects current StudentSection occupancy |
| `AvailableCapacity` | **D. Derived** | From max/reserved/current |
| `OccupancyPercent` | **D. Derived** | From current/max |
| `WaitingList` | **B. Operational/live** | Live queue |
| `CapacityStatus` | **D. Derived/transient** | Status label |

### Distinction required by product architecture

| Event | Meaning | Should invalidate approval via context checksum? |
|---|---|---|
| Section capacity configuration changed (`MaximumCapacity` / `MinimumCapacity` / `ReservedSeats`) | Academic/config contract changed | **YES** |
| Occupancy changed because students enrolled/transferred (`CurrentStrength`) | Live operational drift | **NO** (default) |

### Recommendation (binding for Prompt 3+)

**EXCLUDE `CurrentStrength` and all derived occupancy fields from `AllocationAcademicContextIntegrity` v2.0.0.**

Rationale:

1. Generate Allocation does not commit live StudentSection rows; occupancy is not the frozen “academic configuration” of the scenario.
2. Including occupancy recreates false rebuilds under concurrent enrollment — a second class of the same defect family.
3. True capacity-config drift remains detectable via `MaximumCapacity` / `MinimumCapacity` / `ReservedSeats`.
4. If a future product rule requires “block approve when occupancy moved,” implement it as an **explicit governance rule** (separate blocker), not by smuggling live occupancy into the academic integrity hash.

---

## 7. Student Population Identity Decision

### Options evaluated

| Option | Detects count-stable swap? | Fits architecture? |
|---|---|---|
| A. `StudentCount` only | No | Insufficient |
| B. Unordered Student IDs | Yes if set-equal | Needs canonical order for hash stability |
| C. Ordered Student IDs | Yes | Yes |
| D. Separate `PopulationChecksum` | Yes | Yes — preferred composition |

### Binding decision

Use **D + companion count**:

```text
PopulationChecksum = SHA256(canonical ordered eligible StudentIds)
INCLUDE PopulationChecksum and StudentCount in academic integrity payload
```

### Eligible population definition (v2.0.0)

For the academic context checksum, “eligible students” means:

> All students present on `SectionAllocationContext.Students` after `BuildCoreAsync` for the academic scope  
> (tenant + CourseId + GroupId + SemesterId filters already applied by the builder).

Pipeline `PopulationSelection` (AllEligible / ranges / LastThreeDigits / explicit IDs / facets) is **configuration**, persisted in `ConfigJson` and covered by scenario integrity (§9). It **narrows** who is allocated at Generate time but must not be duplicated inside the academic context hash.

**Why not hash only the post-filter subset inside the context checksum?**

- Context builder loads the scope pool; selection is applied later by `AllocationScopeSelectionValidator` / engine using config.
- Academic drift of the **scope pool** (student joins/leaves the course-group-semester) is what Review must detect when rebuilding context.
- Selection-criteria drift is a **config** change → scenario/config integrity, not academic context rebuild semantics alone.

### Canonical student ordering

```text
StudentIds.OrderBy(id => id)  // ascending int
```

Do not order by `StudentNumber` (culture/format risk) for the hash input.

---

## 8. Section Ordering

### Evaluation

| Key | Suitable as sole canonical order? |
|---|---|
| `SectionId` | **Yes** — stable architectural identity |
| `DisplayOrder` | No for integrity — presentation; can change without membership change |
| `SectionCode` | No — rename/code edit without id change |

### Binding rule

1. Build `sections[]` from the context’s section∩capacity join.
2. Sort by **`SectionId` ascending**.
3. Each element: `{ sectionId, maximumCapacity, minimumCapacity, reservedSeats }`.
4. Do **not** rely on EF/`OrderBy(DisplayOrder)` query order for hashing (builder may still sort that way for UX).

Equivalent logical contexts with different DB return orders **must** produce the same checksum.

---

## 9. Rule Configuration Integrity

### Current protection (verified)

On Generate, `AllocationExecutionService.PersistAsync`:

- Serializes normalized `AllocationPipelineConfig` → session/scenario **`ConfigJson`**
- Computes **`ScenarioChecksum`** via `AllocationCanonicalChecksum.Compute`, which includes canonicalized **`config`** (plus scenario JSON, context checksum, versions, score, trace, lifecycle)

Pipeline fields therefore already covered for scenario integrity:

- `GroupingMode` / LastThreeDigits ordering mode
- `EnabledStrategies` including `RollNumberBands`
- `RollNumberBandSize`
- `ExistingAssignmentPolicy`
- `TargetSectionIds`
- `PopulationSelection` (mode, ranges, student ids, facets)
- Constraint priorities

### Governance today

`EvaluateAsync` separately validates:

1. Scenario payload / version checksum integrity (`ChecksumInvalid`)
2. Academic context rebuild vs stored `ContextChecksum` (`ContextStale`)
3. Mandatory constraints, etc.

### Recommendation

| Concern | Owner |
|---|---|
| Academic scope / sections / capacity config / eligible pool / faculty bindings | **`AllocationAcademicContextIntegrity` (this design)** → `ContextChecksum` |
| Allocation rules & population selection criteria | **`AllocationCanonicalChecksum` + ConfigJson** → `ScenarioChecksum` |

**Do not duplicate** rule configuration inside the academic context checksum.

If a future defect shows config changes not detected, fix scenario canonicalization — do not overload context stale semantics.

---

## 10. Canonical Serialization

### Contract

Reuse the proven approach from `AllocationCanonicalChecksum`:

1. Build a `JsonObject` / `JsonArray` tree with **explicit property insertion order** OR sort object keys with `StringComparer.Ordinal`.
2. Collections pre-sorted by documented keys before serialization.
3. `WriteIndented = false`.
4. Integers as JSON numbers (not strings).
5. Null `programId` → JSON `null` (stable).
6. Strings: trim not required for ids; `role` use ordinal as stored.
7. **Forbidden in payload:** timestamps, GUIDs (except none), request/session/user/trace ids, floating occupancy percentages.
8. Nested digests are uppercase hex SHA-256 strings.
9. Outer hash: SHA-256 over UTF-8 bytes of the canonical JSON string → uppercase hex.

### Determinism guarantee

```text
Same logical academic context
  ⇒ same canonical JSON
  ⇒ same ContextChecksum
independent of ContextId, cache, clock, user, and DB row order
```

---

## 11. Generate → Review Invariant

```text
Build at Generate
  → CanonicalAcademicChecksum = X   (algorithm 2.0.0)
  → Persist X as ContextChecksum
  → Build again during Review (new ContextId allowed)
  → CanonicalAcademicChecksum = X
  ⇒ StoredChecksum == CurrentChecksum
  ⇒ ContextStale = false
```

when no academic/capacity-config/population/faculty-binding change occurred.

Governance comparison remains:

```csharp
!string.Equals(current.Checksum, row.ContextChecksum, StringComparison.OrdinalIgnoreCase)
```

No bypass. No UI override.

---

## 12. True Drift Detection

Under v2.0.0, the following **must** change the checksum:

| # | Change | Mechanism |
|---|---|---|
| 1 | Section added | New `sections[]` membership |
| 2 | Section removed | Membership loss |
| 3 | Maximum capacity changed | `maximumCapacity` field |
| 4 | Eligible student added | `populationChecksum` / `studentCount` |
| 5 | Eligible student removed | `populationChecksum` / `studentCount` |
| 6 | Academic scope changed | `hierarchy.*` ids |
| 7 | Allocation-governing capacity policy numbers | `minimumCapacity` / `reservedSeats` |
| 8 | Faculty-section assignment binding changed | `facultyAssignmentChecksum` |

Must **not** change checksum:

| Non-event | Why |
|---|---|
| New `ContextId` on rebuild | Excluded |
| `GeneratedAt` | Excluded |
| Occupancy / `CurrentStrength` drift | Excluded (§6) |
| DisplayOrder / SectionCode rename | Excluded |
| Pipeline rule edits alone | Scenario/config integrity, not context hash |
| Cache hit/miss | Integrity ignores cache |

---

## 13. Historical Allocation Compatibility

### Fact

Existing rows store `ContextChecksum = Hash(ContextId + CurrentStrength + …)` with **no algorithm version column**.

New algorithm ⇒ different hash space ⇒ naive compare marks all historical scenarios stale (already true today for another reason).

### Options

| Option | Description | Safety |
|---|---|---|
| A | Historical stay on old algorithm; require regeneration under new | Safe; no migration |
| B | Store checksum algorithm/version on row | Safe; needs schema or metadata |
| C | Version the integrity contract inside the hash payload | Safe; self-describing for new rows |

### Recommended strategy (composite)

**Primary: C + operational A for history.**

1. Embed `integrityAlgorithmVersion: "2.0.0"` in every new canonical payload (Option C).
2. **Do not migrate** historical production checksums in this workstream’s early prompts.
3. Historical scenarios retain old hashes; under the new builder they will not match current v2 hashes → remain `ContextStale` until administrators **regenerate** (Option A behavior). That is acceptable and already operationally required for the broken v1 hashes.
4. **Optional later (Prompt 3+ if needed):** additive nullable column `ContextIntegrityAlgorithmVersion` on `AllocationEngineScenario` / version rows for diagnostics and dual-read. **Not required** to ship correctness if all new generates write v2 and compare uses whatever `BuildAsync` now produces.
5. **Do not** dual-run old volatile algorithm for new generates.

Explicit non-goals for Prompt 2: no schema migration, no SQL rewrite of checksums, no auto-approval of historical rows.

---

## 14. Cache / Governance Interaction

| Path | Today | Integrity role |
|---|---|---|
| `GET /allocation/context` | May return `IAllocationContextCache` (TTL 10 min) | UI convenience only |
| Generate | `_builder.BuildAsync` (no cache read) | Produces stored checksum |
| Governance / Detail | `_builder.BuildAsync` (no cache read) | Recomputes current checksum |

### Recommendation

| Choice | Verdict |
|---|---|
| A. Shared canonical builder/checksum service is sufficient | **YES — primary** |
| B. Unify context cache across Generate/Governance | Optional performance only; **must not** be the integrity mechanism |
| C. Cache removed from integrity decisions | **YES — mandatory** |
| D. Generate & Governance call the same canonicalization service | **YES — mandatory** |

**Correctness over cache reuse.** Governance must never treat a cached UI context as proof of freshness. Cache may store a context that already carries a v2 checksum for display, but Evaluate always rebuilds (or rebuilds then canonicalizes) independently.

---

## 15. Service Ownership

### Single authority

Introduce one application-layer service (static helper acceptable, mirroring `AllocationCanonicalChecksum`):

```text
AllocationAcademicContextIntegrity
  .Compute(SectionAllocationContext context) → string checksum
  .BuildCanonicalPayload(...) → JsonObject (testable)
  .ComputePopulationChecksum(IEnumerable<int> studentIds) → string
  .ComputeFacultyAssignmentChecksum(...) → string
  public const string AlgorithmVersion = "2.0.0";
```

### Callers (must not fork logic)

| Caller | Usage |
|---|---|
| `SectionAllocationContextBuilder.BuildCoreAsync` | Set `context.Checksum` via integrity service |
| `AllocationExecutionService` | Persists `result.Scenario.ContextChecksum` (unchanged flow) |
| `AllocationGovernanceAndDashboard.EvaluateAsync` | Compare rebuilt context checksum to stored |
| `AllocationScenarioQueryService.GetDetailAsync` | Same via rebuild / governance |
| UI | Display only; never compute integrity |

No duplicated checksum formulas in controllers or React.

---

## 16. API / Domain Impact

| Layer | Impact |
|---|---|
| Domain entities | No required change for v2 ship; optional later algorithm version column |
| Application | New integrity helper; builder switches `ComputeChecksum` to it |
| API contracts | `checksum` / `contextChecksum` fields keep names; semantics become canonical |
| DTOs | No breaking API shape required |
| RBAC | Unchanged |
| Approval endpoints | Unchanged gates; stale still blocks |

`ContextId` continues to appear on API context payloads for correlation.

---

## 17. Database Impact

| Change | Prompt 2 | Prompt 3 (implementation) | Later optional |
|---|---|---|---|
| Modify `ContextChecksum` column type | No | No | No |
| Rewrite historical checksums | No | No | No (regenerate) |
| Add `ContextIntegrityAlgorithmVersion` | No | Prefer defer | Additive nullable string |
| Change FK/RBAC tables | No | No | No |

Persisted value remains the hex string in `ContextChecksum`; only the **bytes hashed** change for new generates.

---

## 18. Security / Governance Impact

| Control | Effect of this design |
|---|---|
| Tenant isolation | Unchanged — builder still tenant-scoped |
| Server-authoritative approval | Unchanged — `CanApprove` still requires no blockers |
| `ContextStale` gate | **Preserved** — now measures real academic drift |
| Scenario checksum gate | Preserved — config/rules still protected |
| RBAC / Allocation.Run / Approve | Unchanged |
| Architecture Guard / no IgnoreQueryFilters | Unchanged |
| Auditability | Improved — checksum means academic integrity, ContextId remains correlator |

**Forbidden “fixes”:** ignore `ContextStale`, client-side Approve enablement, SQL force-approve, disable rebuild validation.

---

## 19. Migration Strategy

```text
Phase 0 (this prompt): Design only — DONE when doc accepted
Phase 1 (next implementation prompt):
  - Add AllocationAcademicContextIntegrity
  - Wire builder to v2.0.0
  - Unit tests for determinism + drift
  - No schema migration
  - No historical backfill
Phase 2 (acceptance):
  - Generate → Review invariant on live tenant
  - Confirm true drift still blocks
  - Historical scenarios may still show rebuild until regenerated (expected)
Phase 3 (optional):
  - Persist algorithm version column for ops clarity
  - Admin messaging: “regenerate to refresh integrity contract”
```

---

## 20. Test Strategy

### Unit (non-invasive; no production data)

1. **ContextId independence:** two contexts identical except `ContextId` → same checksum.
2. **Ordering independence:** sections/students/faculty permuted → same checksum after canonicalization.
3. **Occupancy independence:** `CurrentStrength` / `OccupancyPercent` / `AvailableCapacity` differ → same checksum.
4. **Drift — section add/remove** → different checksum.
5. **Drift — MaximumCapacity / ReservedSeats / MinimumCapacity** → different checksum.
6. **Drift — student add/remove / swap with same count** → different `populationChecksum`.
7. **Drift — hierarchy id change** → different checksum.
8. **Drift — faculty binding change** → different checksum.
9. **Algorithm version:** payload includes `2.0.0`; changing version string changes checksum.
10. **Null programId** stable serialization.

### Integration / acceptance (later prompt)

1. Generate → immediate Review → `contextCurrent=true`, `contextStale=false` when scope unchanged.
2. Refresh Status does not flip-flop solely due to new GUIDs.
3. Genuine section capacity change → rebuild required.
4. Approve still blocked when stale; still allowed when current and other gates pass.
5. Faculty RBAC regressions unchanged.

---

## 21. Proposed Files / Methods

| File | Change (implementation prompt) |
|---|---|
| `Abhyanvaya.Application/Academic/Allocation/AllocationAcademicContextIntegrity.cs` | **New** — canonical payload + SHA-256 |
| `SectionAllocationContextBuilder.cs` | Replace private `ComputeChecksum` body with call to integrity service; keep `ContextId = Guid.NewGuid()` |
| `ISectionAllocationContextBuilder.cs` | XML notes only if needed |
| `AllocationGovernanceAndDashboard.cs` | No logic weaken; optional clearer diagnostic when algorithm evolves |
| `AllocationScenarioQueryService.cs` | Optional: avoid double rebuild for performance only |
| `AllocationCanonicalChecksum.cs` | Unchanged (scenario/config integrity) |
| `Abhyanvaya.Application.UnitTests/.../AllocationAcademicContextIntegrityTests.cs` | **New** tests |
| UI | No change required for root fix |

---

## 22. Risks

| Risk | Mitigation |
|---|---|
| Historical scenarios remain stale under v2 | Expected; regenerate; document in acceptance |
| Excluding occupancy allows approve after enrollment churn | Intentional; add explicit governance rule later if product demands |
| Excluding DisplayOrder misses “academic sort” edits | Placement uses live DisplayOrder; integrity tracks membership/capacity |
| Faculty checksum noise if faculty rows churn often | Still more correct than FacultyCount; monitor false rebuilds |
| Double `BuildAsync` in Detail+Evaluate | Performance only; correctness OK once checksum stable |
| Cache confusion in ops debugging | Document cache ≠ integrity |

---

## 23. Rollback Strategy

1. Feature is localized to checksum computation in the builder/integrity helper.
2. Rollback = restore previous `ComputeChecksum` implementation (not recommended long-term) **or** ship forward fix.
3. No schema to roll back if Phase 1 avoids new columns.
4. Scenarios generated under v2 that are rolled back to v1 code will appear stale again (symmetric with today’s defect).
5. Never roll back by disabling `ContextStale` checks or forcing Approve.

---

## Implementation-Ready Summary

| Decision | Binding value |
|---|---|
| Keep `ContextId` | Yes — exclude from hash |
| Algorithm | `AllocationAcademicContextIntegrity` **2.0.0** |
| Occupancy in hash | **No** |
| Population | Ordered StudentIds → `populationChecksum` + `studentCount` |
| Sections | Sort by `SectionId`; include max/min/reserved |
| Rules/config | Scenario `AllocationCanonicalChecksum` / `ConfigJson` only |
| Cache | Not used for integrity |
| Ownership | Single application integrity service |
| History | No migration; regenerate |
| Governance | Stale detection preserved |

---

## Explicit Statement — No Production Behavior Changed

**NO production code, database schema, live data, RBAC, approval behavior, or stale-detection rules were modified in Prompt 2.** This document is design-only.

---

## FINAL STATUS

**PASS — canonical design complete and implementation-ready**

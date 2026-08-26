# AI29.1D.24B.4A.1 Prompt 1 — Final Acceptance Discovery & Freeze Gate

**Date:** 2026-08-16  
**Mode:** Discovery only — **no production allocation behavior changes**

## Prior 24B.4A status (accepted)

Existing Assignment Policy, Preserve/Reallocate, Explicit/All Eligible, Last 3 Digits filter, Full vs Last 3 semantics, Faculty `Allocation.Run` 403, Operations.View separation, no-timetable attendance, server-side band>capacity warning (band 100 / cap 60).

**Outstanding gap:** exact **Band Size = 60** / **Section Capacity = 50**, plus visual confirmation in Allocation Rules.

---

## Discovery answers

### 1. Where Roll Number Band Size is configured

| Layer | Location |
|-------|----------|
| UI state | `EnterpriseAllocationWorkspace` → `rollNumberBandSize` |
| UI control | `AllocationStrategyConfigPanel` — “Band Size” (only when Section Allocation Method = Roll Number Bands) |
| API request | `AllocationRunRequest.RollNumberBandSize` |
| Pipeline | `AllocationPipelineConfig.RollNumberBandSize` → ConfigJson |

### 2. Where Section MaximumCapacity is obtained

| Layer | Location |
|-------|----------|
| Authority | `ISectionCapacityEngine` / `SectionCapacityEngine` |
| Allocation Context | `SectionAllocationContextBuilder` → `AllocationCapacityProjection.MaximumCapacity` |
| UI soft check | `context.capacities` passed as `targetSectionCapacities` into Allocation Rules |

### 3. Where the server calculates the warning

`AllocationPlacementSupport.WarnIfBandExceedsCapacity` during `RollNumberBandsAllocationStrategy.ApplyAsync`:

- If `bandSize > hard seats` (MaximumCapacity − ReservedSeats) for any target section → warning string added to execution warnings.

### 4. How the warning is exposed to the UI

1. **Server authoritative:** simulate/run response `warnings[]` (shown in preview/result flows).  
2. **UI advisory (Allocation Rules):** local compare `rollNumberBandSize > min(context capacities)` → MUI `Alert` (does not place students; does not write StudentSection).

### 5. Whether Allocation Rules displays the warning

**Yes** — when Roll Number Bands is selected and Band Size > minimum positive capacity in the capacities prop:

> “Your allocation band contains more students than the selected Section can hold. Some students may remain unallocated.”

Prior browser run did **not** show it because live capacities were all **60** and Band Size was **60** (equality → no warning).

### 6. Whether existing implementation already supports exact 60/50

| Mechanism | Supports 60/50? |
|-----------|-----------------|
| Server warning logic | **Yes** (any band > capacity) |
| UI Allocation Rules Alert | **Yes** (any band > min capacity) |
| Live college data (001/1053, AY1/C1/G2/S3) | **No** — SCCA01, CA-A, CA-B all MaximumCapacity **60** |

### 7. Can capacity be changed safely via existing admin configuration?

**Supported API:** `PUT /api/sections/capacity/{sectionId}` (`UpdateSectionCapacityAsync`) under `CanManageSectionCapacity`.

**Prompt 2 constraint:** prefer an existing **test-only** Section; do **not** casually modify production-like Sections.

| Section | Role (prior validation docs) | Suitable as test-only? |
|---------|------------------------------|-------------------------|
| SCCA01 | Pre-existing / retained legitimate | **No** |
| CA-A | Live CA III section | **No** |
| CA-B | Live CA III section | **No** |

No dedicated test-only Section with Capacity 50 was identified in this scope.

---

## Controlled test data required (if Chief Architect later approves)

To execute exact Band 60 / Capacity 50 without inventing engine behavior:

1. A controlled Section in tenant college `1053`, scope AY=1 / Course=1 / Group=2 / Semester=3  
2. Code e.g. `TEST-CAP50` (or Architect-named) marked/understood as validation-only  
3. Temporary `MaximumCapacity = 50` via existing capacity API  
4. Reversible manifest + restoration (Prompt 2/4)

Until such a Section exists, **do not** mutate CA-A / CA-B / SCCA01 solely for a PASS.

---

## Conclusion

**BLOCKED_WITH_REASON**

Exact Band 60 / Capacity 50 browser acceptance cannot proceed under Prompt 2 rules: no existing test-only Section with capacity 50 is available, and modifying live CA III sections is disallowed without explicit Architect-approved controlled test data.

**Already proven (do not regress):**

- Server soft warning when band > capacity (e.g. 100 vs 60)  
- Allocation Rules UI Alert wired for band > min capacity (needs unequal values to appear)

**Optional non-mutating visual check (not a substitute for 60/50):** set Band Size **61** with live Capacity **60** to confirm Allocation Rules Alert visibility — must be reported as OBSERVED / related evidence, **never** as PASS for exact 60/50.

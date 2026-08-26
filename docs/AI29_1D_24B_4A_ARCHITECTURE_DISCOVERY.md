# AI29.1D.24B.4A — Architecture Discovery (Existing Assignment Semantics)

**Phase:** Prompt 1 — Discovery only  
**Date:** 2026-08-16  
**Status:** Complete — no production changes in this prompt

## Relevant services / classes

| Area | Location |
|------|----------|
| Population | `AllocationPopulationSelection`, `AllocationScopeSelectionValidator`, `AllocationContextScopeApplier` |
| Config | `AllocationPipelineConfig` (+ `GroupingMode`, `EnabledStrategies`, `RollNumberBandSize`, `TargetSectionIds`) |
| Ordering | `StudentGroupingStrategy` (`LastThreeDigits` = order only) |
| Placement | `CapacityAllocationStrategy`, `RollNumberBandsAllocationStrategy` |
| Context build | `SectionAllocationContextBuilder` (loads sections `OrderBy DisplayOrder ThenBy SectionCode`) |
| Live assignment | `StudentSection` → projected as `CurrentSectionId` / `CurrentSectionCode` |
| Scenario | `AllocationScenarioFactory.FromWorkingState` → recommendations + explanations |
| Persistence | Session/Scenario `ConfigJson` → checksum / replay / compare |

## Current behavior (authoritative)

Both `CapacityAllocationStrategy` and `RollNumberBandsAllocationStrategy` share the same **seed** pattern before placing unassigned students:

```
if CurrentSectionId ∈ target sections AND remaining seats > 0
  → Assignments[student] = CurrentSectionId
  → explanation "✓ Kept in section (capacity available)"
else
  → not seeded; later placement loop may assign a new section
```

| # | Situation | Actual outcome |
|---|-----------|----------------|
| 1 | Unassigned | Placed by strategy (Capacity balance or RollNumberBands) |
| 2 | Assigned to Section A (in targets, capacity left) | **Preserved** (seeded) |
| 3 | Assigned to B but band would map to A | **Preserved** if B in targets + capacity; band ignored for that student |
| 4 | Assigned to target with capacity | **Preserved** |
| 5 | Assigned to target that is full (remaining = 0) | **Not preserved**; may be **reallocated** to another section or warned |
| 6 | Assigned outside explicit target list | **Not preserved**; **silently reconsidered** into a target by placement |
| 7 | Assigned + All Eligible population | Included in population; seed rules above apply |
| 8 | Assigned + Explicit target selection | Same as 6 if current section not in explicit list |

**Classification of current behavior:**  
**Preserve only when capacity permits AND current section is in the scoped target set**; otherwise **reallocate** (including silent move from outside-target into targets). Assigned students are **treated as population participants**, not excluded.

There is **no** explicit `ExistingAssignmentPolicy` on `AllocationPipelineConfig` today.

## Section ordering (related defect)

- Context builder uses authoritative `Section.DisplayOrder` then `SectionCode`.
- `AllocationSectionProjection` **does not carry `DisplayOrder`**.
- RollNumberBands / Capacity **re-sort targets by `SectionCode` ordinal** only → `Section 10` before `Section 2` risk; DisplayOrder ignored at placement.

## Explanations today

Technical/engine-flavored strings (e.g. `✓ Roll number band {n} → {code} (last3=…, bandSize=…)`). Not administrator business language. UI displays server `Explanations` as-is.

## Existing API / config

- Run/Simulate: `AllocationPipelineConfig` via request body → ConfigJson  
- No DB migration for strategy fields (ConfigJson precedent)  
- Governance / Approve unchanged (recommendations only)

## Existing tests

- `AI29_1D_24B4_RollNumberBandStrategyTests` (includes “existing assigned kept when capacity allows”)  
- Population / interaction tests from 24B.4  

## Risks

- Silent reallocation of outside-target assigned students under “preserve” mental model  
- SectionCode-only order ≠ academic DisplayOrder  
- Band size > capacity produces warnings but UX may not explain clearly  
- Legacy ConfigJson has no assignment-policy field  

## Recommended semantic boundary

| Concept | Owner |
|---------|--------|
| Who participates | Population filter |
| Order considered | GroupingMode (`LastThreeDigits` unchanged) |
| How placed | Placement strategy (Capacity / RollNumberBands) |
| Whether existing assignments move | **New** `ExistingAssignmentPolicy` on `AllocationPipelineConfig` |
| Target destinations | `TargetSectionIds` / All Eligible |
| Capacity hard limit | Capacity projections + constraints |
| Section band sequence | Authoritative `DisplayOrder` (reuse Section entity; project into context) |

**Do not** change `LastThreeDigits` ordering semantics or invent a second engine.

**Backward-compatible default (for Prompt 2):**  
Missing / null policy → **legacy seed behavior** (preserve when capacity + in targets; otherwise reconsider).  
Explicit `PreserveExisting` → never silently move outside-target or full-section students into another section.  
Explicit `Reallocate` → skip seed; engine reconsider all eligible.

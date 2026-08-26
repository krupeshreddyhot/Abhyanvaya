# AI29.1D.24B.4 — Architecture Discovery & Semantic Contract

**Phase:** Prompt 1 — Discovery only (no production code changes in this prompt)  
**Date:** 2026-08-15  
**Status:** Complete

## Current architecture (canonical flow)

```
UI configures AllocationPipelineConfig
  → API validates Allocation.Run + tenant scope
  → AllocationScopeSelectionValidator (population + targets vs SectionAllocationContext)
  → AllocationContextScopeApplier (scoped context view)
  → StudentGroupingStrategy.OrderStudents (ordering only)
  → AllocationPipeline strategies (placement + soft rules + scoring)
  → Scenario ConfigJson + checksum / replay / compare
```

React never places students. Server remains authoritative.

---

## Chief Architect Q&A

| # | Question | Answer |
|---|----------|--------|
| 1 | Canonical population filter contract? | `AllocationPopulationSelection` + `AllocationPopulationModes`; validated by `AllocationScopeSelectionValidator`; applied by `AllocationContextScopeApplier`. Request wire: `AllocationRunRequest.PopulationSelection`. |
| 2 | Canonical grouping strategy contract? | `AllocationPipelineConfig.GroupingMode` ∈ `AllocationGroupingModes`; executed by `IStudentGroupingStrategy` — **order only**. |
| 3 | Canonical placement strategy contract? | `IAllocationPipelineStrategy` pipeline; real placer today = `CapacityAllocationStrategy` (`Capacity`). Soft strategies annotate/score. |
| 4 | Can population filter specify last-three-digit semantics today? | **No.** `StudentNumberRange` uses full-string ordinal compare. `LastThreeDigits` is grouping only. |
| 5 | Does engine distinguish filtering / ordering / placement? | **Yes.** Validate+Apply → OrderStudents → Capacity (and later steps). |
| 6 | Where should configurable allocation policy live? | Per-run `AllocationPipelineConfig` → `ConfigJson` (checksum/replay). No separate college default store today. |
| 7 | Can strategy config already persist in AllocationPipelineConfig? | **Yes** — grouping, enabled strategies, constraints, population, targets. |
| 8 | Does ConfigJson capture selected strategy? | **Yes** — full normalized config. |
| 9 | Does checksum/replay capture strategy config? | **Yes** — checksum hashes ConfigJson; replay deserializes and re-runs. |
| 10 | Can different tenants have different strategy configuration? | **Per run / saved scenario**, yes. Persistent tenant defaults: **not implemented**. |
| 11 | Are strategy names catalogued? | Server codes + UI `allocationStrategyCatalog.ts` + C# `DisplayName`. |
| 12 | Are strategy descriptions available from server? | **No** — GETs return ids/priorities only; descriptions are UI-side. |

---

## Existing roles of key types

### Population — `AllocationPopulationSelection`

Modes: `AllEligible`, `StudentNumberRange`, `StudentIds`, Gender / Scholarship / Language / … facets.

`StudentNumberRange`: inclusive **full** `StudentNumber` ordinal ignore-case (`CompareStudentNumbers`). Empty numbers excluded. **Not numeric. Not last-3.**

### Grouping — `StudentGroupingStrategy`

| Mode | Behavior |
|------|----------|
| `StudentNumber` / `StudentNumberRange` | Order by full student number |
| `LastThreeDigits` | Order by last 3 chars, then full number |
| Others | Facet/hash proxies |

**Never assigns sections.**

### Placement — `CapacityAllocationStrategy`

Lowest occupancy ratio among eligible target sections; seeds existing assignments; hard capacity; no digit→section mapping.

### Config — `AllocationPipelineConfig`

`GroupingMode`, `EnabledStrategies`, `ConstraintPriorities`, `PopulationSelection`, `TargetSectionIds`. Persisted as scenario/session `ConfigJson`.

### UI catalogs

- Population: `allocationPopulationFilter.ts` + `StudentPopulationFilterPanel.tsx`
- Strategies: `allocationStrategyCatalog.ts` (human labels; LastThreeDigits copy currently implies “Distribute” — semantic defect)

---

## Current defects (from AI29.1D.24B.3)

| Id | Defect | Root |
|----|--------|------|
| P2-POP-001 | Range `46`–`50` → 0 matches | Full-string ordinal vs admin last-3 intent |
| P2-POP-002 | Range `1`–`5` → over-match | Ordinal prefix over-match on 12-digit numbers |
| P2-STRAT-001 | LastThreeDigits does not map 001–060→A | Ordering only; Capacity balances seats |

College banding example (001–060→A … 241–250→E) is a **policy**, not the grouping strategy.

---

## Proposed semantic boundaries

| Concept | Owns | Does not own |
|---------|------|--------------|
| **Population Filter** | WHO participates | Ordering, section placement |
| **Grouping Strategy** | HOW participants are ordered | Eligibility, section bands |
| **Placement Strategy** | HOW ordered students get sections | Population eligibility |
| **Allocation Policy** | College-selected combination of the above (per run / ConfigJson) | Hard-coded single-college rules in engine |
| **Target Sections** | WHERE placement may occur | Who is eligible |
| **Capacity Policy** | Hard seats / reserved seats (existing capacity projections) | UI-invented limits |

---

## Contracts that can be reused

- `AllocationPopulationSelection` / validator / applier (extend modes additively)
- `AllocationPipelineConfig` / ConfigJson / checksum / replay / compare
- `IAllocationPipelineStrategy` registration pattern
- `AllocationGroupingModes.LastThreeDigits` (preserve as ordering)
- Target section scope (AI29.1D.24B.2)
- Capacity projections and mandatory Capacity constraints
- Existing allocation APIs (`simulate` / `run` / grouping-modes / pipeline-strategies)

---

## Is a new API required?

| Need | New API? |
|------|----------|
| Last-3 population filter | **No** — additive population mode on existing contract |
| Roll-number band placement | **No new HTTP resource** — new pipeline strategy code + optional config property inside `AllocationPipelineConfig` |
| Strategy descriptions from server | Optional enrichment only; not required for engine |
| Tenant default policy store | Only if product later wants durable college defaults (out of scope) |

---

## Are database changes required?

**No** for ConfigJson-persisted strategy/population fields (same pattern as Prompt 10A).  
Checksum/replay already store ConfigJson. No new permission framework. No Attendance/Timetable/Section schema changes.

---

## Can college roll-number banding be expressed with EXISTING strategies?

**No.**

- `LastThreeDigits` only sorts.
- `Capacity` balances occupancy; does not map digit bands → sections.
- Soft strategies do not place by roll bands.
- Multi-run `StudentNumberRange` + explicit targets is an operational workaround, not automatic banding.

**Gap:** configurable placement strategy (e.g. `RollNumberBands`) using:

- ordered students (reuse grouping),
- configurable band size (not hard-coded 60),
- existing target sections + capacity projections,
- persisted in ConfigJson.

Must **not** rewrite `LastThreeDigits` into a placer.

---

## Recommended implementation sequence (Prompts 2–5)

1. Add `LastThreeDigitsRange` population mode (preserve `StudentNumberRange`).
2. Document strategy ownership (Prompt 3).
3. Implement `RollNumberBands` placement strategy + `RollNumberBandSize` config when Prompt 3 confirms gap.
4. Correct UI copy; expose Roll Number Bands without implying LastThreeDigits alone bands sections.
5. Interaction / security / browser validation.

## Risks

- Ambiguous UX if LastThreeDigits grouping and LastThreeDigitsRange filter share labels.
- Enabling both `Capacity` and `RollNumberBands` without mutual exclusion could double-place.
- Band size vs per-section capacity mismatch must surface as existing capacity constraint results, not silent overflow.

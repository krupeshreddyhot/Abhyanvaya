# AI29.1D.24B.4 Prompt 3 — Allocation Strategy Semantic Separation

**Date:** 2026-08-15  
**Status:** Complete (gap confirmed; Prompt 4 implements)

## Ownership boundaries

| Concept | Definition | Owner | Must not |
|---------|------------|-------|----------|
| **Population Filter** | WHO participates | `AllocationPopulationSelection` | Change ordering or placement |
| **Grouping Strategy** | HOW participants are ordered | `GroupingMode` / `StudentGroupingStrategy` | Assign sections; imply 001–060→A |
| **Placement Strategy** | HOW ordered students get sections | `IAllocationPipelineStrategy` (`Capacity`, `RollNumberBands`) | Change population eligibility |
| **Allocation Policy** | College-selected combination persisted per run | `AllocationPipelineConfig` → ConfigJson | Hard-code one college in `LastThreeDigits` |
| **Target Sections** | WHERE placement may occur | `TargetSectionIds` | Invent sections outside context |
| **Capacity Policy** | Hard seats / reserved | Context capacity projections + constraints | UI-invented limits |

## Existing strategies remain available

Student Number, Last Three Digits (**ordering**), Alphabetical, Gender, Merit, Scholarship, Minor Subject, Language, Transport, Hostel, Elective Combination, Weighted/Combined (UI preset), Capacity placement.

## Can existing engine express college roll banding without new logic?

**No.**

- `LastThreeDigits` sorts only.
- `Capacity` balances occupancy; does not map digit bands → sections.
- Soft strategies do not place by roll bands.

**Gap:** configurable placement strategy `RollNumberBands` with optional `RollNumberBandSize` (default = first target section `MaximumCapacity`). Banding example 001–060→A is **policy configuration**, not hard-coded into grouping.

## Decision

Implement Prompt 4 as an additive pipeline strategy under existing AI29.1C contracts. No parallel strategy framework. No Attendance/Timetable/governance rule changes.

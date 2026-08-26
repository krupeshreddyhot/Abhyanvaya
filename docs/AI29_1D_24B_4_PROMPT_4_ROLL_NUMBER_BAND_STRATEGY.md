# AI29.1D.24B.4 Prompt 4 — Roll Number Band Allocation Strategy

**Date:** 2026-08-15  
**Status:** Implemented

## Strategy

- Code: `RollNumberBands`
- Order: 19 (before Capacity)
- When enabled: Capacity placement is skipped (mutual exclusion)
- Band index: `(lastThree == 0 ? 0 : (lastThree - 1) / bandSize)`
- Band size: `RollNumberBandSize` or first target section `MaximumCapacity` (never hard-coded 60)
- Target sections: ordered by section code / id from scoped context
- Capacity: hard seats respected; overflow → warnings (no silent overflow)
- Existing assignments: kept when capacity remains (same seed semantics as Capacity)

## Persistence

`AllocationPipelineConfig.RollNumberBandSize` + `EnabledStrategies["RollNumberBands"]` in ConfigJson → checksum / replay / compare.

## API

`AllocationRunRequest.RollNumberBandSize` (optional). Strategy toggles merge onto defaults so omitted `RollNumberBands` stays **false**.

## Tests

`AI29_1D_24B4_RollNumberBandStrategyTests`: 60/120/240/250/241 students, capacity 50/75, explicit targets, existing assignments, ConfigJson round-trip.

# AI29.1D.24B.4 Prompt 6 — Population and Strategy Interaction

**Date:** 2026-08-15  
**Status:** PASS (automated)

## Rule

- Population filter → WHO  
- Allocation strategy (order + placement) → HOW  
- Neither silently changes the other  

## Matrix

| # | Population | Order / Placement | Result |
|---|------------|-------------------|--------|
| 1 | Full Student Number | Student Number + Capacity | PASS |
| 2 | Full Student Number | Last 3 Digits + Capacity | PASS |
| 3 | Last 3 Digits | Last 3 Digits + Capacity | PASS |
| 4 | Last 3 Digits | Student Number + Capacity | PASS |
| 5 | Last 3 Digits | Last 3 Digits + RollNumberBands | PASS |
| 6 | All Eligible | Last 3 Digits + RollNumberBands | PASS |
| 7 | All Eligible | Explicit section + RollNumberBands | PASS |
| 8–10 | Covered in Prompt 4 (explicit / assigned / capacity) | | PASS |

Config participates in ConfigJson / checksum / replay via `AllocationPipelineConfig`.

Tests: `AI29_1D_24B4_PopulationStrategyInteractionTests`

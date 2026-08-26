# AI29.1D.24B.4 Prompt 2 — Student Population Filter Semantics

**Date:** 2026-08-15  
**Status:** Implemented

## Contract

| Mode | Semantics | Bound fields |
|------|-----------|--------------|
| `StudentNumberRange` | Full `StudentNumber` inclusive ordinal ignore-case (unchanged) | `FromStudentNumber` / `ToStudentNumber` |
| `LastThreeDigitsRange` (**new**) | Numeric last-three digits 000–999 inclusive | Same bound fields, interpreted as last-3 |

Authority: `AllocationScopeSelectionValidator` + `AllocationContextScopeApplier` against `SectionAllocationContext` only. UI mirrors for counts; server is authoritative.

## Last 3 Digits rules

- Min 000 / Max 999
- From ≤ To
- Normalize `"46"` → `"046"`
- Digits only; reject empty / non-digit / &gt;999
- Match via trailing digit extraction from student number — **not** full-string ordinal compare

## Validation coverage

1. Full Student Number range  
2. Last 3 Digits range  
3. 001–005 (normalized from 1–5)  
4. 046–050  
5. 000 / 999  
6. From &gt; To  
7. Invalid / empty  
8. Legacy `StudentNumberRange` `"1"`–`"5"` ordinal over-match preserved  

Tests: `AI29_1D_24B4_PopulationFilterSemanticsTests` + UI `allocationPopulationFilter.test.ts`

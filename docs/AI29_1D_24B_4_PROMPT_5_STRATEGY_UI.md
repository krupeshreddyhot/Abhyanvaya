# AI29.1D.24B.4 Prompt 5 — Administrator Allocation Strategy UX

**Date:** 2026-08-15  
**Status:** Implemented

## UX changes

- **Allocation order** (grouping): Last 3 Digits = “Order students using the last three digits…” (no distribute/band implication)
- **Section placement policy**: Capacity balance | Roll Number Bands
- Roll Number Bands shows optional band size; capacity/targets remain from their steps
- Help: final placement is server + capacity authoritative
- Technical Details still gated by Operations.View

## Persistence

Selection maps to existing `groupingMode` + `enabledStrategies` + `rollNumberBandSize` on simulate/run — no React-only policy store.

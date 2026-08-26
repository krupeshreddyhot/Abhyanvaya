# AI29.1D.24B.4A Prompt 3 — Section Allocation Order

**Date:** 2026-08-16  
**Status:** Implemented

## Authoritative order

Reuse `Section.DisplayOrder` (already used by `SectionAllocationContextBuilder`).

Projected as `AllocationSectionProjection.DisplayOrder`.

Placement (`AllocationPlacementSupport.OrderTargetSections`):

1. `DisplayOrder` ascending  
2. `SectionCode` ordinal ignore-case  
3. `SectionId`

No hard-coded A/B/C/D. Lexical-only ordering removed from RollNumberBands / Capacity.

## Gap closed

Previously DisplayOrder was dropped at projection and strategies re-sorted by SectionCode only.

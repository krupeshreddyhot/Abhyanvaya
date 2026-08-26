# AI29.1D.24B.4A Prompt 2 — Existing Assignment Policy

**Date:** 2026-08-16  
**Status:** Implemented

## Policy values (`AllocationPipelineConfig.ExistingAssignmentPolicy`)

| Value | Behavior |
|-------|----------|
| `LegacyPreserveWhenCapacityAllows` | **Default when omitted** — preserve if current section ∈ targets and capacity remains; otherwise reconsider (may move outside-target into targets). Matches pre-4A seed behavior. |
| `PreserveExisting` | Preserve when capacity allows in targets; **never** silently move outside-target or full-section students |
| `Reallocate` | No seed; all eligible reconsidered by placement strategy |

## Compatibility

Missing ConfigJson property → Normalize → `LegacyPreserveWhenCapacityAllows`.  
UI default sends `PreserveExisting`.

## Persistence

Included in ConfigJson → checksum / replay / compare / governance. No second store. No direct StudentSection writes from UI.

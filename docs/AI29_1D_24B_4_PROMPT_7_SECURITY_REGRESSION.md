# AI29.1D.24B.4 Prompt 7 — Security and Regression

**Date:** 2026-08-15  

## Security (contract-level)

| Check | Result |
|-------|--------|
| Allocation.Run still gates simulate/run | Unchanged policies (`CanRunAllocation`) |
| Faculty without Allocation.Run | Still denied (24B.3/3A) — not re-weakened |
| Operations.View / Approve | Unchanged; UI Technical Details / Approve remain separate |
| Invalid population / strategy | Validator fail-closed; unknown modes rejected |
| Cross-tenant | No new IgnoreQueryFilters; tenant scoping unchanged |
| Opt-in RollNumberBands | Merged onto defaults — omitted key stays false |

## Regression filters executed

See final validation doc for exact counts.

## Unrelated logic

No Attendance / Timetable / Section entity / governance rule engine changes.

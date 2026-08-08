# AI29.1C.5A — Metrics / KPI Definitions

## Constraint KPIs (independent)

| KPI | Definition |
|---|---|
| Mandatory Compliance | Satisfied mandatory / total mandatory × 100 |
| Preferred Compliance | Satisfied preferred / total preferred × 100 |
| Informational Findings | Count of unsatisfied informational constraints |
| Mandatory Violations | Count of unsatisfied mandatory constraints (prominently shown; target 0) |

Do not blend Mandatory/Preferred/Informational into one compliance %.

## Allocation run KPIs (status counts)

From `AllocationEngineSession.Status` actual counts:

Total Runs, Successful, Failed, Cancelled, Timed Out, Running

Successful is **not** `TotalRuns × SuccessRate`.

## Heatmap bands (policy-aware)

Uses `TenantSectionCapacityPolicy.WarningPercent` / `UnderCapacityPercent`:

| Band | Rule |
|---|---|
| Over Capacity | Occupancy > 100% **or** assigned > maximum capacity |
| Near Capacity | Occupancy ≥ warning % and not over capacity |
| Healthy | Between under % and warning % |
| Underused | Occupancy ≤ under-capacity % |

Heatmap title: **Latest Scenario – Section Utilization** (not live institutional allocation).

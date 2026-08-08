# AI29.1C.5A — Allocation Governance

## Status vs LifecycleStatus

| Property | Meaning |
|---|---|
| `Status` | Execution/result status (`Generated`, `Completed`, `Failed`, `Cancelled`, `TimedOut`, `Running`, …) |
| `LifecycleStatus` | Authoritative governance lifecycle (`Draft`/`Generated`, `Saved`, `Simulated`, `Compared`, `Reviewed`, `Approved`, `Rejected`, `Archived`) |

Controllers must not set either field for governance transitions. All lifecycle changes go through `IAllocationScenarioLifecycleService`.

## Lifecycle state machine

Illegal (blocked):

- `Archived → *`
- `Approved → Draft`
- `Rejected → Approved`

Approval gates (human-controlled):

1. Not archived / not already approved  
2. Context freshness (checksum vs current `SectionAllocationContext`)  
3. Scenario checksum integrity  
4. Mandatory constraints = 0 violations  
5. Transactional draft creation + version + audit  

## Transaction boundary (Approve)

Validate → Context → Checksum → Permissions (API policy) → Constraints → Draft artifact → Lifecycle/Version → Audit → Commit  

If draft creation fails, the scenario is not approved and no success audit is committed.

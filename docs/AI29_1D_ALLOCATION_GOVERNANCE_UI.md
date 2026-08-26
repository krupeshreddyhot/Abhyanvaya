# AI29.1D — Allocation Governance Lifecycle UI

Integrates **AI29.1C.5A** scenario governance into the Enterprise Allocation Workspace (Review / Approve steps).

## Lifecycle vs execution

| Concern | Source | UI label |
|---------|--------|----------|
| Execution Status | Engine run/simulate `status` | Completed / Failed / … |
| Governance Lifecycle | Scenario `lifecycleStatus` | Draft · Review · Approved · Rejected · Archived |

These are never shown as a single conflated chip.

## Actions (existing APIs)

| Action | Endpoint |
|--------|----------|
| Review | `POST /allocation/scenarios/{id}/review` |
| Approve | `POST /allocation/scenarios/{id}/approve` |
| Reject | `POST /allocation/scenarios/{id}/reject` |
| Archive | `POST /allocation/scenarios/{id}/archive` |
| Version History | from scenario detail `versions` |
| Replay | `POST /allocation/scenarios/{id}/replay` |
| Compare | `GET /allocation/compare` + `POST /allocation/scenarios/compare` |

## Approval

Approve is enabled only when `governance.canApprove === true` (plus permission). Exact `blockingReasons` are listed (stale context, invalid checksum, mandatory violations, archived, already approved, concurrency conflict, etc.). The UI does **not** re-implement approval rules.

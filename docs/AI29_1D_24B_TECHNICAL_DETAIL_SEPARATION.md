# AI29.1D.24B — Technical Detail Separation

## Principle

Normal administrators see business language only. Implementation identifiers remain in API responses and may appear under optional **Technical Details**.

## Hidden from default workflow

- Scenario / Context GUIDs and checksums
- Raw lifecycle tokens
- `canApprove` / `blockingReasons` property names
- Engine payload JSON
- Pipeline / groupingMode / constraintPriorities jargon
- Trace step tables
- AI29.1C / AI29.1C.5A marketing strings
- API route names in alerts

## Technical Details (collapsed, optional)

Shown when the user has existing `Allocation.Operations.View`.

| Surface | Contents |
|---------|----------|
| Allocation Rules | JSON payload `{ groupingMode, enabledStrategies, constraintPriorities }` |
| Review / Approve | Scenario reference, context versions, checksum, raw lifecycle, governance flags |
| Preview | View Allocation Details — engine trace steps |

Default accordion state: **collapsed**.

## Permissions

| Audience | Existing key |
|----------|--------------|
| Normal section admin | `Section.View` / run / approve keys |
| Technical Details + ops links | `Allocation.Operations.View` |

No new permission keys. No new authorization logic for terminology.

## Governance mapping (presentation only)

| Server signal | Administrator title |
|---------------|---------------------|
| `contextStale` | Allocation needs to be rebuilt |
| `checksumInvalid` | Allocation data has changed |
| `concurrencyConflict` | Allocation was updated elsewhere |
| mandatory / required blockers | Required allocation rules are not satisfied |
| archived | Allocation archived |
| already approved | Already approved |

Helpers:

- `allocationAdministratorCopy.ts`
- `governanceBlockingPresentations()` in `allocationGovernanceLifecycle.ts`

## Architecture boundaries

| UI may | UI must not |
|--------|-------------|
| Consume existing allocation / governance APIs | Access EF / DbContext / SQL / repositories |
| Map display labels | Recreate approval rules |
| Collapse technical fields | Invent new endpoints |
| Use AcademicConfirmDialog | Use `window.confirm` for approve |

## Unchanged backends

Allocation Engine, SectionAllocationContext, capacity engine, governance services, AttendanceSessionResolver, Scheduling, Subject Master, StudentSection persistence, Program/Course relationship.

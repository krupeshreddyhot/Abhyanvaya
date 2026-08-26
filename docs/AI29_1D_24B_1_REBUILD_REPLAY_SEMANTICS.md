# AI29.1D.24B.1 Prompt 3 — Rebuild & Replay Semantics

**Mode:** UI presentation / action labels only.  
**Date:** 2026-08-10

## Why “Rebuild Allocation” was misleading

When governance reported a stale academic context, the UI labeled the primary action **Rebuild Allocation**.  
The handler only executed:

```ts
setActiveStep(0); // Academic Scope
setMessage(/* instruction */);
```

It did **not**:

- call any rebuild API  
- rebuild allocation context  
- call `runAllocation`  
- call `replayAllocationScenario`  
- write `StudentSection` records  
- approve the allocation  

Administrators were led to believe a rebuild had occurred or would occur immediately. That was incorrect.

## Corrected stale-context UX

| Element | Value |
|---------|--------|
| Heading | Allocation needs to be rebuilt |
| Explanation | The academic information used for this allocation has changed. Review the academic scope and generate the allocation again. |
| Button | **Review Academic Scope** |
| Behavior | Navigate to Academic Scope step only |
| Follow-up | Administrator continues the existing workflow and uses **Generate Allocation** when ready |

Constants: `LABEL_REVIEW_ACADEMIC_SCOPE`, `MSG_REVIEW_SCOPE_THEN_GENERATE`.

**No new rebuild endpoint** was created or required.

## Replay semantics

| Item | Value |
|------|--------|
| Administrator label | **Replay Allocation** |
| Client | `replayAllocationScenario(id)` (unchanged) |
| API | `POST /allocation/scenarios/{id}/replay` (unchanged) |
| Permission | `Allocation.Scenario.Replay` (unchanged) |
| Success toast | Allocation replay completed. Student records were not changed. |
| Version history action | Replayed |

### Why Replay is not regeneration

Replay re-executes against the **stored allocation scenario**. It is **not**:

- a full regeneration from the current academic scope  
- an academic-context rebuild  
- starting the guided workflow from Academic Scope  

Fresh allocation from current scope remains **Generate Allocation** (`runAllocation`).

Do **not** use “Regenerate Allocation” for the replay action.

## Governance authority

React does **not** implement `canApprove`, `contextStale`, `checksumInvalid`, or other approval rules.  
The UI displays server governance results and gates the Approve control using the existing `governance.canApprove` value as an implementation signal only (never shown as a property name in the normal path).

## Backend / API / database

| Item | Change |
|------|--------|
| API endpoints | **NONE** |
| Database schema | **NONE** |
| Entities / domain / engine | **NONE** |
| Permissions | **NONE** |

## Related docs

- `docs/AI29_1D_24B_1_ADMINISTRATOR_UX_LANGUAGE.md` — Generate / Replay / Review Academic Scope table  
- `docs/AI29_1D_24B_1_ARCHITECTURE_DISCOVERY.md` — Prompt 1 leakage audit  

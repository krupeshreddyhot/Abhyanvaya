# AI29.1D.24B.1 — Administrator UX Language

**Mode:** UI presentation only.  
**Updated:** Prompt 2A — Replay ≠ Regenerate; stale action = Review Academic Scope.

## Rule

Internal domain/API concepts are unchanged (`scenarioId`, `StudentSection`, `replayAllocationScenario`, `governance.canApprove`, etc.).  
Only administrator-visible labels, messages, headings, and helper text are hardened.

## Three distinct actions

| Administrator label | What it means | Underlying behavior |
|---------------------|---------------|---------------------|
| **Generate Allocation** | Creates an allocation using the current workflow/context. | Existing `runAllocation` / generate path |
| **Replay Allocation** | Replays an **existing** allocation scenario using the existing replay API. | `replayAllocationScenario` → `POST /allocation/scenarios/{id}/replay` |
| **Review Academic Scope** | Navigation when academic configuration is stale; **does not itself rebuild** anything. | `setActiveStep(0)` only — no API |

Do **not** call Replay “Regenerate”. Do **not** call Review Academic Scope “Rebuild”.

## Banner

Use the steps below to prepare, test, review, and approve student allocation.

## Success / error messages (workspace)

| Event | Administrator message |
|-------|------------------------|
| Test allocation OK | Test allocation completed. No student records were changed. |
| Test allocation errors | Test allocation could not be completed. Review the issues below and try again. |
| Generate allocation | Allocation created successfully. |
| Compare | Allocation comparison completed. |
| Draft save | Draft saved. Student records were not changed. |
| Replay (API unchanged) | Allocation replay completed. Student records were not changed. |
| Review / Reject / Archive | Allocation marked as reviewed / rejected / archived. |
| Stale navigation toast | Academic information has changed since this allocation was created. Review the academic scope and generate the allocation again. |

## Stale context

| Element | Copy |
|---------|------|
| Title | Allocation needs to be rebuilt |
| Description | … Review the academic scope and generate the allocation again before approving it. |
| Primary action | **Review Academic Scope** (navigate only) |

## Governance helpers

| Constant | Value |
|----------|-------|
| `LABEL_REPLAY_ALLOCATION` | Replay Allocation |
| `MSG_REPLAY_COMPLETED` | Allocation replay completed. Student records were not changed. |
| `LABEL_REVIEW_ACADEMIC_SCOPE` | Review Academic Scope |
| `versionActionLabel("Replay")` | Replayed |

Removed misleading identifiers: `LABEL_REGENERATE_ALLOCATION`, `MSG_REGENERATE_COMPLETED`.

## Panel copy

| Surface | Notes |
|---------|--------|
| Student Population / Capacity | No Engine / API-path jargon |
| Technical Details | Collapsed; `Allocation.Operations.View`; keeps diagnostics |
| Operations page | Replay Allocation; Review Academic Scope link |

## Non-goals (unchanged)

API contracts, TypeScript service shapes, permissions, engine, governance domain, Attendance, Scheduling.

# AI29.1D.24B — Allocation Review UX

## Purpose

Make the Enterprise Section Allocation workspace understandable to college administrators without changing AI29.1C / AI29.1C.5 / AI29.1C.5A server authority.

## Workflow labels

| Before | After |
|--------|-------|
| Allocation Strategy | Allocation Rules |
| Scenario | Allocation |
| Review — Governance Lifecycle | Review Allocation |
| Approve — Governance Lifecycle | Approve Allocation |
| Run Simulation | Test Allocation |
| Generate Scenario (Engine Run) | Generate Allocation |
| Approve | Approve Allocation |

## Allocation Rules UX

- **Primary Allocation Rule** — business labels (Student Number, Alphabetical Order, Gender Balance, …)
- **Additional Allocation Rules** — checkbox labels from catalog (not raw enum codes)
- **Selected Allocation Rules** — compact summary (primary / additional / section capacity)
- **Advanced Allocation Options** (collapsed) — Required / Preferred / Informational priorities with help text
- Values posted to the API remain `Mandatory` / `Preferred` / `Informational`

## Review page content

Shows:

- Allocation Status (Draft → Review → Approved / Rejected / Archived)
- Approval Status (business-mapped blockers)
- Version History (Version / Action / Status / Date / Reason)
- Approve confirmation via `AcademicConfirmDialog`

## Approval UX

- Button label: **Approve Allocation**
- Disabled when `governance.canApprove !== true` (implementation value; never shown as property name)
- Confirmation: draft-safe wording — does not claim permanent student moves
- Server remains authoritative; UI does not recalculate eligibility

## Stale context

| Before | After |
|--------|-------|
| Flag: stale context | Allocation needs to be rebuilt |
| Refresh Governance as primary CTA | **Rebuild Allocation** + Back |

## Simulation / Preview

- Simulation explained as testing distribution without changing student records
- Preview table: Student, Current Section, Proposed Section, Reason, Rule Applied, Capacity Status, Allocation Score
- Summary: Total / Allocated / Unallocated / per-section / Capacity Issues / Required Issues / Warnings

## Banner

Replaced engine/governance marketing text with:

> Create and review student section allocations based on your selected academic scope, students, allocation rules, and section capacity.

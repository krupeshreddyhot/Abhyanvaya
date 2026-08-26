# AI29.1D — Allocation Preview

Preview step of the Enterprise Allocation Workspace renders an existing **AllocationExecutionResult / AllocationScenario** from the AI29.1C engine.

## Actions

| Action | Contract |
|--------|----------|
| Preview | `POST /allocation/simulate` |
| Simulation | `POST /allocation/simulate` (advances to Simulation step) |
| Compare | `GET /allocation/compare?scenarioId=` |
| Back | Wizard previous step |
| Save Draft | `POST /allocation/sandbox` (sandbox draft only) |

No live `StudentSection` writes from Preview.

## Student grid (engine fields)

Student Number, Student Name, Current Section, Proposed Section, Allocation Reason (`explanations`), Strategy (grouping + executed trace strategies), Constraint Result (engine constraints / notes), Score (scenario score when available).

## Summary

Total / Allocated / Unallocated, Section A–C counts (first three section codes from engine summaries), Capacity / Mandatory / Preferred / Informational violation counts, score breakdown from the engine.

Does **not** invent a second explanation or scoring engine.

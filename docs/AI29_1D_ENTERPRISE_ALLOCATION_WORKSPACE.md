# AI29.1D — Enterprise Allocation Workspace

Replaces the minimal Student Allocation tab on `/setup/sections` with a guided workflow over **existing** AI29.1C engine and AI29.1C.5A governance APIs.

## Workflow

Academic Scope → Student Population → Allocation Strategy → Section Capacity → Preview → Simulation → Scenario → Review → Approve

## Contracts used (no UI algorithms)

| Step | API |
|------|-----|
| Scope | `AcademicScopeSelector` / `AllocationScope` |
| Population / Capacity | `GET /allocation/context` (+ UI filters over context students; see `AI29_1D_STUDENT_POPULATION_FILTERING.md`) |
| Strategy | `groupingMode` + `enabledStrategies` + `constraintPriorities` (see `AI29_1D_ALLOCATION_STRATEGY_SELECTION.md`) |
| Capacity | Section Capacity Engine `/sections/capacity/*` (see `AI29_1D_ALLOCATION_CAPACITY_INTEGRATION.md`) |
| Preview | Allocation scenario/result preview (see `AI29_1D_ALLOCATION_PREVIEW.md`) |
| Simulation | `POST /allocation/simulate` |
| Scenario | `POST /allocation/run` + `GET /allocation/scenarios/{id}` |
| Review / Approve / Reject / Archive / Replay / Compare | AI29.1C.5A governance (see `AI29_1D_ALLOCATION_GOVERNANCE_UI.md`) |

Approve = **draft only** (no live `StudentSection` writes). Live transfer/auto-allocate remain on the Transfer tab.

## Files

- `components/allocation/EnterpriseAllocationWorkspace.tsx`
- `pages/setup/SectionsPage.tsx` (Student Allocation tab)
- `services/allocationPlatformService.ts` (`listAllocationGroupingModes`, default strategy map)

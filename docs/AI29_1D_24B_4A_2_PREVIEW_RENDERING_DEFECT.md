# AI29.1D.24B.4A.2 — Preview & Test Allocation UI Rendering Defect

**Date:** 2026-08-16  
**Status:** Fixed (Prompt 3 + Prompt 4)

## Prompt 3 — Null / undefined rendering trace

### Contract: where recommendations live

| Source | Path | Shape |
|--------|------|-------|
| C# `AllocationExecutionResult` | `Scenario.Recommendations` | `IReadOnlyList<AllocationStudentRecommendation>` |
| Live JSON `/allocation/simulate` | `scenario.recommendations` | array of student placement objects |
| UI TypeScript `AllocationExecutionResult` | `scenario?.recommendations` | same |

**Mismatch documented:** There is **no** `result.recommendations` on `AllocationExecutionResult`.

A *different* type, `SectionAllocationContext`, has root `recommendations: string[]` (health tips). Confusing those two types would show empty or wrong preview data.

UI accessors: `getExecutionRecommendations(result)` → `result?.scenario?.recommendations ?? []`.

### Live shape notes (college 001/1053)

- Top-level keys: `sessionId`, `scenarioId`, `succeeded`, `status`, `scenario`, `trace`, `score`, `errors`, `warnings`, `durationMs`
- `hasRootRecommendations` = **false**
- `scenario.constraints[].priority` serializes as **number** (`0`/`1`/`2`) because `AllocationConstraintPriority` is a C# enum without `JsonStringEnumConverter` on this payload

### Root cause of blank page

Preview summary / capacity banner called `(priority ?? "").trim()`.

When `priority` is numeric `0`, nullish coalescing keeps `0`, then `.trim()` throws:

`TypeError: raw.trim is not a function`

Allocation Workspace has **no** page-level ErrorBoundary (unlike Attendance), so the React tree unmounts → **blank application page** after a successful simulate.

### Paths hardened

| Area | Fix |
|------|-----|
| recommendations | Access only via `scenario.recommendations` helpers |
| constraints.priority | `normalizeAllocationConstraintPriority` accepts `string \| number` |
| warnings / errors / trace.steps | Array guards (`?? []`) |
| section summaries | Optional chaining + empty arrays |
| summary / rows builders | try/catch → graceful empty / fallback summary |
| Preview panel | Error boundary wrapper; no forced `trace!.steps` |

## Prompt 4 — Preview vs Test Allocation

| Item | Finding |
|------|---------|
| Endpoint | **Both** call `POST /allocation/simulate` via `simulateAllocation` |
| Shared handler | `doSimulate` in `EnterpriseAllocationWorkspace` |
| Shared UI | `AllocationPreviewPanel` + `buildAllocationPreviewSummary` / `Rows` |
| Semantic distinction | **UX only** — Preview stays on step 4; Test Allocation advances to step 5 (`advanceToSimulationStep: true`) |
| Backend semantics | **Unchanged** |

Preview step now prefers `simulation ?? execution` so a newer simulate is not shadowed by a prior Generate Allocation (`execution`) result.

## Files touched

- `abhyanvaya-ui/src/utils/allocationConstraintPriority.ts` (new)
- `abhyanvaya-ui/src/utils/allocationExecutionResultAccessors.ts` (new)
- `abhyanvaya-ui/src/utils/allocationPreviewSummary.ts`
- `abhyanvaya-ui/src/utils/allocationCapacityViolations.ts`
- `abhyanvaya-ui/src/utils/allocationAdministratorCopy.ts`
- `abhyanvaya-ui/src/services/allocationPlatformService.ts` (DTO types)
- `abhyanvaya-ui/src/components/allocation/AllocationPreviewPanel.tsx`
- `abhyanvaya-ui/src/components/allocation/AllocationPreviewErrorBoundary.tsx` (new)
- `abhyanvaya-ui/src/components/allocation/EnterpriseAllocationWorkspace.tsx`
- Related unit tests

No allocation engine / approve / StudentSection write changes.

# AI29.1D.24B.2 — Target Section Scope & Explicit Selection

**Date:** 2026-08-10  
**Phase:** Hardening (Allocation Workspace UI + additive occupancy filter)

## Problem

Target Sections could display Sections outside the selected Academic Year / Course / Group / Semester because capacity occupancy was loaded year+semester-wide and, when Allocation Context section IDs were empty, the UI fell through to the unfiltered catalog. Explicit selection UX was also incomplete, and parent-scope changes did not reliably clear stale `targetSectionIds`.

## Architectural authority

| Concern | Authority |
|---------|-----------|
| Eligible Sections | Allocation Context (`SectionAllocationContextBuilder`) |
| `targetSectionIds` acceptance | `AllocationScopeSelectionValidator` (AI29.1D.10A) |
| Scoped apply | `AllocationContextScopeApplier` |
| Placement / scoring | Allocation Engine (unchanged) |
| Combined classes | Existing SectionGroup / TimetableSections (unchanged) |
| Attendance | Unchanged — out of scope |

React does **not** implement Course/Group name filters or a second eligibility engine.

## Scope resolution

1. Administrator selects Academic Year → Program (cascade) → Course → Group → Semester.
2. Workspace loads `GET /api/allocation/context` with year/course/group/semester.
3. Target Sections list = `context.sections` only.
4. Capacity occupancy is requested with the same section IDs (additive `sectionIds` query) and client-filtered again fail-closed.

## `targetSectionIds` contract

| UI mode | Client value | Server meaning |
|---------|--------------|----------------|
| All eligible sections | `null` | All sections in Allocation Context |
| Explicit selection | `number[]` (non-empty) | Only those IDs, each must be in context |
| Explicit + zero | `[]` — Continue disabled | Must not be sent as a successful run |

Unauthorized / out-of-context IDs are **rejected** (fail-closed); they are not silently removed.

## All Eligible behavior

Uses all Sections returned by Allocation Context for the selected academic scope. No unrestricted tenant catalog.

## Explicit Selection behavior

Radio: **Explicit selection** → checkboxes for context sections only → `Selected: N sections`.  
Continue/Next disabled when N = 0 with message: `Select at least one Section to continue.`

## Scope reset

When Academic Year, Program, Course, Group, or Semester changes (`allocationScopeKey`):

- `targetSectionIds` cleared (`null`)
- previous context / readiness / health cleared
- eligible list reloaded when already past Academic Scope step
- stale Finance selections cannot remain after switching to Computer Applications

## Fail-closed behavior

If Allocation Context cannot load:

- previous section list is dropped
- message: `Unable to load eligible Sections.`
- Retry available
- allocation cannot continue
- occupancy never falls back to year/semester-wide rows

## Combined sections

No new CombinedSection entity. Individual Section IDs from context remain the allocation targets. SectionGroup/Timetable contracts preserved.

## Security

Server validates tenant-scoped Allocation Context; forged `targetSectionIds` from another Course/Group/Semester/Year/Tenant fail 10A validation. Additive occupancy `sectionIds` limits capacity data returned for this workflow.

## Performance

- Single context bundle (parallel readiness/health/validation)
- Occupancy scoped by section IDs (no tenant-wide list for Target UI)
- Reload only when scope key changes

## Backward compatibility

- Existing `targetSectionIds` / populationSelection / Allocation Engine contracts unchanged
- Occupancy without `sectionIds` still returns year/semester rows (other consumers)
- No database migration

## Test coverage

- `AI29_1D_24B_2_TargetSectionScopeDiscoveryTests`
- `AI29_1D_24B_2_TargetSectionScopeAuthorityTests`
- Existing `AI29_1D_Prompt10A_AllocationScopeTests`
- UI: `allocationTargetSectionSelection.test.ts`

## Files changed

- `Abhyanvaya.API/Controllers/SectionCapacityController.cs` — additive `sectionIds`
- `abhyanvaya-ui/src/services/sectionService.ts`
- `abhyanvaya-ui/src/components/allocation/AllocationCapacityPanel.tsx`
- `abhyanvaya-ui/src/components/allocation/EnterpriseAllocationWorkspace.tsx`
- `abhyanvaya-ui/src/utils/allocationTargetSectionSelection.ts` (+ tests)
- Docs + unit tests under `Abhyanvaya.Application.UnitTests/Academic/`

## APIs changed

**Additive only:** `GET /api/sections/capacity/occupancy?sectionIds=` (optional).  
No new allocation endpoint. No new entity.

## Database changes

**None.**

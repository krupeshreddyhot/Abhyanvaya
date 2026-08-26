# AI29.1D Prompt 10A — Allocation Population & Target Section Scope Hardening

## Objective

Ensure the Enterprise Allocation Workspace’s selected **student population** and **target sections** are honored by the existing AI29.1C Allocation Engine, resolved exclusively against `SectionAllocationContext`.

## Contracts added

### `AllocationPopulationSelection`

Modes: `AllEligible`, `StudentNumberRange`, `StudentIds`, `Gender`, `ScholarshipCategory`, `MinorSubject`, `Language`, `TransportRoute`, `Hostel`, `ElectiveCombination`, `Merit`.

Fields: `Mode`, `FromStudentNumber`, `ToStudentNumber`, `StudentIds`, `FacetValue`.

### `AllocationPipelineConfig` (additive)

- `PopulationSelection` (null → AllEligible)
- `TargetSectionIds` (null/empty → all eligible sections)

Normalized via `Normalize()` (sorted ids) before persist / engine execution. Included in `ConfigJson` → scenario checksum, replay, compare, governance.

### Helpers

- `AllocationScopeSelectionValidator` — context-only validation
- `AllocationContextScopeApplier` — filtered context view (no repository)

## API changes

`POST /allocation/run` and `POST /allocation/simulate` accept:

```json
{
  "populationSelection": { "mode": "Gender", "facetValue": "Female" },
  "targetSectionIds": [1, 2]
}
```

Legacy requests without these fields remain **All eligible students + All eligible sections**.

Invalid selections → `400 BadRequest` with clear validation errors.

## Engine changes

`AllocationEngine.ExecuteAsync`:

1. Normalize config  
2. Validate selection against context  
3. Apply scoped context (students + sections + capacities)  
4. Group / pipeline on scoped context only  

No `StudentRepository` / `SectionRepository` access.

## UI changes

- Population step sends `populationSelection` on run/simulate  
- Facet readiness: Available / PartiallyAvailable / Unavailable (Unavailable disabled)  
- Capacity step: Target Sections — All eligible **or** explicit checkboxes  
- Matching count and target-section mode clearly displayed  

## Database changes

**None.** Selection identity lives in existing `ConfigJson` / checksum payload.

## Files changed (primary)

| Area | Files |
|------|--------|
| Contracts | `AllocationPopulationSelection.cs`, `AllocationScopeSelectionValidator.cs`, `AllocationContextScopeApplier.cs`, `AllocationModels.cs` |
| Engine | `AllocationEngine.cs`, `AllocationPipelineStrategies.cs` (metadata), `AllocationExecutionService.cs` |
| API | `AllocationEngineController.cs` |
| Tests | `AI29_1D_Prompt10A_AllocationScopeTests.cs` |
| UI | `allocationPlatformService.ts`, `allocationPopulationFilter.ts`, `StudentPopulationFilterPanel.tsx`, `AllocationCapacityPanel.tsx`, `EnterpriseAllocationWorkspace.tsx` |
| Docs | this file |

## Tests covered

All eligible · range · student ids · gender · unsupported facet · invalid range · student outside context · explicit targets · all sections · target not in context · different population/target → different ConfigJson/checksum · replay/compare round-trip · legacy behavior · engine honors scope · architecture guard · facet readiness.

## Backward compatibility

Preserved: omit population/targets → full context students and sections.

## Architecture guard

`AcademicArchitectureGuard.ValidateAllocationBoundaries()` — engine must not depend on Student/Section repositories. Prompt 10A helpers are static / context-only; guard remains green.

# AI29.1D — Canonical Academic UI Context

**Status:** Implemented (context + cascade helpers; attendance business logic unchanged)

## Purpose

Provide a single operational academic selection state for React pages:

Academic Year · Program (optional) · Course · Group · Semester · Section (optional) · Subject · Faculty  
+ Timetable overlay · Attendance overlay

## Cascade rules

| EnablePrograms | Path |
|----------------|------|
| `true` (and programs exist) | Program → Course → Group → Semester → Section |
| `false` / unavailable | Course → Group → Semester → Section |

- **Subject** loads only from Course + Group + Semester (never Section).
- **Section** is optional operational grouping; changing Section does not clear Subject.
- Tenants without Programs degrade gracefully (`programsAvailable === false`).

## Consumption

```tsx
import { useAcademicUi } from "../context/AcademicUiContext";
import { AcademicScopeSelector } from "../components/academic";

const { enablePrograms, programsAvailable, selection, options, setSelection } = useAcademicUi();
// Prefer `options.*` — do not re-filter catalogs in page components.

<AcademicScopeSelector
  fields={["academicYear", "program", "course", "group", "semester", "section", "subject"]}
  sectionOptional
  showCascadeHint
/>
```

Provider nesting (`main.tsx`):

```
AuthProvider → TenantContextProvider → AcademicUiProvider → App
```

College/tenant boundaries: catalogs load only when authenticated and (for SuperAdmin) operational college context is present; selection resets on `ContextChanged` / `ContextCleared`.

## Reusable selector

`AcademicScopeSelector` renders cascading selects with loading / empty / error / disabled states.
Program is shown only when `EnablePrograms` is true. Subject never depends on Section.

Consumers:
- `AllocationContextPage` — Year → Program? → Course → Group → Semester
- `SectionsPage` — full AI29 section ops tabs; list enriched via statistics / readiness / versions APIs

## Files

| File | Role |
|------|------|
| `src/types/academicUiContext.ts` | Selection / overlay types |
| `src/utils/academicCascade.ts` | Pure cascade + filters |
| `src/utils/academicCascade.test.ts` | Cascade / optional Program tests |
| `src/utils/academicSelectorFieldState.ts` | Selector field enablement |
| `src/utils/academicSelectorFieldState.test.ts` | Field-state tests |
| `src/context/AcademicUiContext.tsx` | Provider + `useAcademicUi` |
| `src/components/academic/AcademicScopeSelector.tsx` | Reusable cascading UI |

## Explicit non-goals

- No change to Attendance mark/edit/resolver business logic
- No Section on Subject Master
- No second academic hierarchy
- No full student/faculty dumps into browser state

# AI29.1D.15A Prompt 8 — Combined Faculty Allocation UX

## Model (unchanged)

```
SectionGroup
  → participating Sections
  → existing FacultySectionAssignments
```

No second combined-section / faculty-section relationship.

## Display

| Field | Example |
|-------|---------|
| Operational Class | `Combined · A + B` |
| Faculty | `Dr. John Smith` |
| Underlying Sections | chips `A`, `B` |
| Assignment IDs | persistent ids `1, 2` (detail/history) |
| Effective From / To | preserved (earliest From / latest To when combined) |
| Status | Current / Ended / Inactive |

Single-section rows show Operational Class `A` with underlying `A`.

## Files

- `abhyanvaya-ui/src/utils/facultySectionAllocationView.ts` (+ tests)
- `abhyanvaya-ui/src/components/sections/FacultySectionAllocationPanel.tsx`
- `Abhyanvaya.Application.UnitTests/Academic/AI29_1D_15A_Prompt8_CombinedFacultyAllocationTests.cs`
- `docs/AI29_1D_15A_PROMPT_8_COMBINED_FACULTY_ALLOCATION.md`

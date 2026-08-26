# AI29.1D — Student Population and Range Filtering

Population step of the Enterprise Allocation Workspace filters **only** against `SectionAllocationContext.students` (Allocation Context contract). The UI never mutates the context array and does not call a parallel Students API for allocation filtering.

## Supported filters

1. All eligible students  
2. Student Number Range (`From` / `To`)  
3. Gender  
4. Scholarship Category  
5. Minor Subject  
6. Language  
7. Transport Route  
8. Hostel  
9. Elective Combination  
10. Merit  

## Student number range

- Comparison uses ordinal ignore-case semantics (aligned with engine `StudentNumber` ordering).  
- Does **not** assume purely numeric student numbers.  
- Validates `From <= To` under those semantics.  
- Matching count is shown before continuing the allocation workflow.  

## Context facets

`AllocationStudentProjection` carries optional filter facets. Gender and Language are populated from the student catalog joins in `SectionAllocationContextBuilder`. Facets without domain columns remain null until sourced into the Allocation Context; the UI surfaces an informational empty state for those options.

## Reset

**Reset Filters** restores All eligible students and clears range/facet inputs.

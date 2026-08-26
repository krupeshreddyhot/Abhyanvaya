# AI29.1D.24B.4A Prompt 4 — Band Size vs Capacity

**Date:** 2026-08-16  

| Band | Capacity | Result |
|------|----------|--------|
| 60 | 60 | Normal |
| 50 | 60 | Valid 50-student bands |
| 60 | 50 | Valid; soft warning; hard capacity still enforced |
| missing band | — | First target section MaximumCapacity |

Server warning (and UI soft warning when capacities known):

"Your allocation band contains more students than … can hold. Some students may remain unallocated."

UI does not calculate final placement capacity.

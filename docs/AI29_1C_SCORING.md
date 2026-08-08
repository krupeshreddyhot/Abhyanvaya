# AI29.1C — Scoring

`AllocationScoreCalculator` scores scenarios deterministically:

- Capacity utilization (target ~70%)
- Policy compliance (mandatory failures → 0)
- Gender / merit / language / hostel / elective / transport dimensions

Preferred constraint violations reduce score; informational constraints report only.

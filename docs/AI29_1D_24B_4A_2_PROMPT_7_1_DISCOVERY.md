# AI29.1D.24B.4A.2 Prompt 7.1 — Final Residual Gate Discovery

**Date:** 2026-08-16  
**Mode:** DISCOVERY ONLY — no production code or data changes

## Live Allocation Context

AY=1 / Course=1 / Group=2 / Semester=3 · college `001`/`1053`

| SectionId | Code | Capacity |
|-----------|------|---------:|
| 13 | SCA - 01 | **50** |
| 5 | SCA - 02 | 60 |
| 14 | SCA - 03 | 60 |
| 15 | SCA - 04 | 60 |

Students in context: **235**

## Gate availability

| Gate | Status | Evidence |
|------|--------|----------|
| A. Legitimate capacity-50 Section | **AVAILABLE** | `SCA - 01` (id=13) MaximumCapacity=50 |
| B. Band Size 60 vs Capacity 50 | **AVAILABLE** | min capacity in context = 50; Band 60 > 50 is legitimate |
| C. Zero-result population filter | **AVAILABLE** | LastThreeDigitsRange `998`–`999` → 0 matches (no student mutation) |
| D. teststaff1 account | **NOT AVAILABLE** | Login HTTP **401** |
| E. knraj faculty denial persona | **AVAILABLE** | Login HTTP **200** (for Allocation.Run denial only) |
| Exact Band 60 / Cap 50 without data mutation | **AVAILABLE** | Uses existing SCA - 01 |
| Zero-result UI without data mutation | **AVAILABLE** | Filter only |
| Live intentional ErrorBoundary force | **BLOCKED** | Must not corrupt production; Prompt 5 unit tests remain controlled proof |

## Decisions for subsequent prompts

- **7.2** → EXECUTE (zero filter 998–999)
- **7.3** → EXECUTE (Band 60 against SCA - 01 capacity 50)
- **7.4** → Document teststaff1 UNAVAILABLE; verify knraj 403
- **7.5** → Prompt 5 unit + live healthy Preview/Test (no forced live fault)

Evidence: `Prompt 7/7.1/discovery.json`

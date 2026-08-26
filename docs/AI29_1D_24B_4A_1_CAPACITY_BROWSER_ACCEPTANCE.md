# AI29.1D.24B.4A.1 Prompt 3 — Capacity Warning Browser Acceptance (Retest)

**Date:** 2026-08-16  
**Environment:** UI `http://localhost:5173` · API `http://localhost:5210/api` · college `001`/`1053`  
**Scope:** AY=1, Course=1, Group=2, Semester=3  
**Existing Assignment Policy:** Preserve Existing  
**Approve:** **No**  
**StudentSection writes:** **No**

## Test section

| Field | Value |
|-------|-------|
| Section | **CA - B Section** (`SCCA02`, id=8) |
| Capacity | **50** |
| Create action | ALREADY_EXISTS |
| Live capacities | SCCA01=60, CA-A=60, CA-B=60, SCCA02=50 |

## Exact mandatory test — Band 60 / Capacity 50

| Item | Result |
|------|--------|
| Band Size = 60, Section Capacity = 50 | **PASS** |
| Server soft warning (SCCA02) | **PASS** — Your allocation band contains more students than section SCCA02 can hold. Some students may remain unallocated. |
| Warning visible in Allocation Rules UI | **PASS** |
| Warning understandable | **PASS** |
| Does not block config (Next not forced-disabled by soft warn) | **PASS** |
| Server capacity authoritative | **PASS** |
| No silent overflow (unallocated messages present) | **PASS** (simulate warnings include capacity / unallocated) |
| Preserve Existing unchanged | **PASS** (policy PreserveExisting on request) |
| No approval / no StudentSection write | **PASS** |

## Browser detail

| Check | Result |
|-------|--------|
| Reached Allocation Rules | true |
| Warning text | `Your allocation band contains more students than the selected Section can hold. Some students may remain unallocated.` |
| Internals exposed | **PASS** (not observed) |
| Screenshot | `D:\Resheta\AttendenceProject\CursonModifiedFiles\AI Attandance\AI29.1D.24B.4A.1\Prompt 3-retest\allocation-rules-band60-cap50.png` |
| Browser error | _none_ |

## Verdict

**Exact Band 60 / Capacity 50:** **PASS**

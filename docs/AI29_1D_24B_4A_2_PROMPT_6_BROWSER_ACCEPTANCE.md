# AI29.1D.24B.4A.2 Prompt 6 — Mandatory Live Browser Acceptance

**Date:** 2026-08-16  
**FINAL STATUS:** **CONDITIONAL PASS**  
**Do not claim FULL PASS.**

**Environment:** UI `http://localhost:5173` · API `http://localhost:5210` · college `001`/`1053`  
**Scope:** AY=1, Course=1, Group=2, Semester=3  
**Admin:** `admin` (Allocation.Run present; Allocation.Operations.View **absent**)  
**Approve:** **No** · **StudentSection writes:** **No**

---

## Exact root-cause validation (live)

| Step | Result |
|------|--------|
| POST `/allocation/simulate` | HTTP **200** |
| `scenario.constraints[].priority` | numeric **0, 1, 2** present |
| Preview UI | **renders** (not blank) |
| `.trim is not a function` | **not observed** |
| React render exception | **not observed** |

Live priorities sample: `[0, 1, 1, 2, 1, 1, 2, 0]`  
Recommendations path: `scenario.recommendations` only (root `recommendations` **absent**).

---

## Live context note

Allocation Context sections at acceptance time:

| Code | Capacity |
|------|---------:|
| SCA - 01 | 60 |
| SCA - 02 | 60 |
| SCA - 03 | 60 |
| SCA - 04 | 60 |

**SCCA02 / capacity-50** from prior 4A.1 retest is **not** present in this live context. Exact Band 60 / Cap 50 gate therefore **NOT EXECUTED — DATA UNAVAILABLE** (no capacity mutation performed).

---

## Mandatory gate matrix

| # | Gate | Status |
|---|------|--------|
| 1 | Preview works (no blank page) | **PASS** |
| 2 | Test Allocation works + advances to Simulation | **PASS** |
| 3 | Numeric priority 0/1/2 does not crash rendering | **PASS** |
| 4 | Results render (summary + proposed assignments) | **PASS** |
| 5 | Explicit Section | **PASS** (supplemental: target `SCA - 01`; 55 recs; 0 foreign) |
| 6 | All Eligible Sections (no context leak) | **PASS** |
| 7 | Last Three Digits 046–050 | **PASS** (expected 5 / recs 5) |
| 8 | Roll Number Bands UI configurable | **PASS** |
| 9 | Preserve Existing | **PASS** |
| 10 | Reallocate | **PASS** |
| 11 | Exact Band 60 / Cap 50 (SCCA02) | **NOT EXECUTED — DATA UNAVAILABLE** |
| 11b | Soft band>capacity path (Band 61 vs Cap 60) | **PASS** (supplemental; not a substitute for exact 60/50) |
| 12 | Zero-result handling | **PASS** (API); UI filter path **CONDITIONAL** |
| 13 | Error recovery | **PASS** (Prompt 5 unit; live `.trim` defect not reproducible) |
| 14 | Governance unchanged (no approve / no StudentSection) | **PASS** |
| 14b | Faculty denied Allocation.Run | **PASS** (`knraj` → simulate **403**; `teststaff1` login **401**) |
| 15 | Technical details hidden without Operations.View | **PASS** (`hasOps=false`, tech not visible) |
| 16 | Console: no allocation render exception | **PASS** (unrelated `ERR_CONNECTION_REFUSED` only) |
| 17 | Regression / builds | **PASS** (see below) |

---

## Preview vs Test semantics

| Action | Endpoint | Step behavior |
|--------|----------|----------------|
| Preview | POST `/allocation/simulate` (via UI proxy) HTTP 200 | Remains on Preview |
| Test Allocation | POST `/allocation/simulate` HTTP 200 | Advances to Simulation |

Two simulate network calls observed during browser session.

---

## Screenshots

- `Prompt 6/01-allocation-rules-band60.png`
- `Prompt 6/02-preview-result.png`
- `Prompt 6/03-test-allocation-result.png`

---

## Regression / build (Prompt 6 §17)

| Suite | Passed | Failed | Skipped |
|-------|-------:|-------:|--------:|
| Focused 4A.2 UI | 16 | 0 | 0 |
| AI29.1D.24B.4 | 29 | 0 | 0 |
| AI29.1D.24B.4A | 11 | 0 | 0 |
| AI29.1D.24B.3A / Allocation.Run auth | 9 | 0 | 0 |
| Architecture Guard / Prompt21 | 29 | 0 | 0 |
| UI build | PASS | | |
| API build | PASS | | |

No skipped tests counted as PASS.

---

## Change flags

| Flag | Value |
|------|-------|
| API contract changed | **NO** |
| Database changed | **NO** |
| Engine changed | **NO** |
| Governance changed | **NO** |
| RBAC changed | **NO** |

Prompt 6 made **no product code changes** (acceptance + documentation only).

---

## Why not FULL PASS

1. Exact **Band Size 60 / Capacity 50** against live **SCCA02** is **NOT EXECUTED — DATA UNAVAILABLE** (all live caps = 60).  
2. `teststaff1` credentials returned **401** (faculty denial proven with `knraj` instead).  
3. Zero-result **UI** path not fully exercised (API zero-population **PASS**).  
4. Live ErrorBoundary boom not forced (original defect fixed; Prompt 5 unit coverage retained).

---

## Verdict

**CONDITIONAL PASS** — blank-page defect closed under live numeric priorities; Preview and Test Allocation proven.  
Exact capacity-50 band warning and a few optional UI paths remain open gates pending Architect-approved data / credentials.

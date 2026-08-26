# AI29.1D.24B.4A — Browser Acceptance (Prompt 9)

**Date:** 2026-08-16  
**Environment:** UI `http://localhost:5173` · API `http://localhost:5210` · college uni `001` / college `1053`  
**Academic scope:** AY=1, Course=1, Group=2, Semester=3  
**Approve called:** **No** (recommendations / simulate only)  
**Script:** `scripts/ai29_1d_24b4a_prompt9_browser_acceptance.mjs`  
**Artifact JSON:** `CursonModifiedFiles/.../AI29.1D.24B.4A/Prompt 9/prompt9-live-acceptance.json`

---

## Captured live configuration

| Field | Value |
|-------|-------|
| Population (All Eligible) | **235** students |
| Existing assignments | **40** assigned / **195** unassigned |
| Student Order | LastThreeDigits |
| Section Allocation Method | RollNumberBands |
| Band size | **60** |
| Target sections (context) | SCCA01 (id=3, order=0, cap=**60**), CA-A (id=4, order=1, cap=**60**), CA-B (id=5, order=2, cap=**60**) |
| Preserve policy | PreserveExisting |
| Reallocate policy | Reallocate |
| Admin JWT | `Allocation.Run` **present**; `Allocation.Operations.View` **absent** |
| Faculty (teststaff1) JWT | `Allocation.Run` **absent** |

---

## Recommendation counts (simulate HTTP 200, not approved)

| Mode | Total recommendations | Preserved (explanation) | Reallocated (explanation) | New (explanation) |
|------|----------------------:|------------------------:|--------------------------:|------------------:|
| PreserveExisting | 135 | **40** | 0 | 95 |
| Reallocate | 175 | 0 | **40** | 135 |

Unallocated / warnings (band overflow beyond 3 sections with band size 60): server emitted capacity/band overflow warnings; scenarios were **not** approved.

---

## Test matrix (exact)

| Test | Result | Evidence |
|------|--------|----------|
| Preserve existing assignments | **PASS** | simulate 200; 40 kept explanations; 0 reassigned |
| Reallocate students | **PASS** | simulate 200; 40 reassigned explanations; existing students reconsidered=`true`; **approve not called** |
| TEST 5 Explicit CA-A only | **PASS** | targetSectionIds=[4]; recommendations=55; foreignTargets=**0**; targetCodes=`["CA-A"]` |
| TEST 6 All Eligible | **PASS** | sectionSummaries=`SCCA01, CA-A, CA-B` ⊆ context; leaks=`[]` |
| TEST 7 Band=60 / Capacity=50 | **NOT EXECUTED — DATA UNAVAILABLE** | Live capacities are all **60**; no section with MaximumCapacity=50 |
| TEST 7b Band > capacity warning (related) | **PASS** | Supplemental: bandSize=**100** vs cap=60 → server warnings: *“Your allocation band contains more students than section CA-A can hold…”* (also SCCA01, CA-B) |
| TEST 8 Last 3 Digits 046–050 | **PASS** | Expected **5**: `105325405046`…`050`; recommendations=**5**; only expected students; mode=`LastThreeDigitsRange` |
| TEST 9 Full Student Number vs Last 3 Digits | **PASS** | Full ordinal `46`–`50` matches **0**; Last3 `046`–`050` matches **5** |
| TEST 10 No-timetable attendance (teststaff1) | **PASS** | Browser: Course/Group/Semester/Subject/Period fields present |
| TEST 11 Faculty without Allocation.Run | **PASS** | teststaff1 simulate → **HTTP 403**; claim absent |
| TEST 12 Technical Details without Ops.View | **PASS** | Admin lacks `Allocation.Operations.View`; browser body did **not** expose Technical Details / checksum / engine JSON on observed routes |

---

## TEST 8 detail — Last 3 Digits population

Exact matched student numbers:

1. `105325405046`  
2. `105325405047`  
3. `105325405048`  
4. `105325405049`  
5. `105325405050`  

---

## TEST 9 detail — semantic difference

| Filter | From–To | Matches in live context |
|--------|---------|------------------------:|
| Full Student Number (`StudentNumberRange`) | `46`–`50` | **0** |
| Last 3 Digits (`LastThreeDigitsRange`) | `046`–`050` | **5** |

---

## Warnings observed

- Preserve/Reallocate with band 60 on 3 sections: overflow for higher bands (e.g. band 4 exceeds 3 sections).  
- Supplemental band 100 vs capacity 60: explicit administrator soft-warning text returned by server.  
- Exact **Band 60 / Capacity 50** UI path not runnable without changing live capacity data.

---

## Browser notes

- Playwright launched via system **Chrome** channel (`channel: "chrome"`).  
- Allocation Rules stepper soft-warning for band>capacity was **not** visually confirmed on workspace routes in this run (UI navigation to populated Allocation Rules with mismatched band/capacity not fully exercised); server warning path is proven via API (TEST 7b).  
- No scenario approval performed.

---

## Overall Prompt 9 verdict

**CONDITIONAL PASS** — mandatory functional/security tests passed live; exact Band 60 / Capacity 50 combination blocked by live data (all capacities 60).

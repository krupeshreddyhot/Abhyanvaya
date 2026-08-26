# AI29.1D.24B.3 — Prompt 2: Live Allocation Semantics, Population, Capacity & Permission Validation

**Date:** 2026-08-15  
**Mode:** Discovery / live validation only — **no production code, schema, API contract, or test changes**  
**Environment:** UI `http://localhost:5173`, API `http://localhost:5210` (college uni `001` / college `1053`)  
**Secrets:** Credentials, JWT tokens, and connection strings are **not** included in this report.

---

## Final status

# **BLOCKED**

Mandatory engine Preview / Simulation / Allocation live paths cannot complete for college **Admin** because the Admin JWT **lacks `Allocation.Run`**. Population ordinal semantics and Last-3 **ordering** were proven from Allocation Context data. Digit-band **placement** and simulate persistence were **not** executable under Admin.

This status is intentional for Prompt 2: defects are documented for Chief Architect → Prompt 3; **no fixes applied**.

---

## Gate checklist

| Gate | Result |
|------|--------|
| Actual StudentNumber format documented | **PASS** |
| 1–5 range tested | **PASS** (context ordinal filter; simulate HTTP 403) |
| 46–50 range tested | **PASS** (context ordinal filter; simulate HTTP 403) |
| LastThreeDigits behavior proven | **CONDITIONAL** — ordering proven via engine-equivalent sort on live numbers; capacity placement **NOT EXECUTED** (403) |
| Existing section assignments tested | **PASS** (context: 40 assigned / 195 unassigned) |
| Capacity with filtered population tested | **NOT EXECUTED** — simulate blocked (403) |
| All Eligible tested | **NOT EXECUTED** — simulate blocked (403); context lists 3 eligible sections |
| Explicit Selection tested | **NOT EXECUTED** — simulate blocked (403); explicit id set available in context |
| Admin JWT claims verified | **PASS** |
| Allocation.Run verified | **FAIL** — absent from Admin claims |
| Preview disabled/enabled root cause identified | **PASS** |
| Simulation permission root cause identified | **PASS** |
| Simulation persistence behavior verified | **NOT EXECUTED** — simulate 403 |
| StudentSection live-write behavior verified | **PASS (contract)** — controller documents scenarios/drafts only; no mutate exercised |
| Cross-group isolation tested | **PASS** (CA vs Finance student-id overlap = 0) |
| Cross-semester isolation tested | **PASS** (Sem I vs Sem III overlap = 0) |
| Cross-tenant isolation tested | **NOT EXECUTED — DATA UNAVAILABLE** |
| Defect register completed | **PASS** |
| No production code changed | **PASS** |
| No DB/API/schema changes | **PASS** |
| Documentation created | **PASS** |
| Browser validation marked | **PASS (documented)** — workspace reachability; full workflow blocked |

---

## 1. StudentNumber format (live)

| Item | Value |
|------|--------|
| Scope | AY=1, Course=B.Com(1), Group=CA(2), Semester=III(3) |
| Population | **235** students |
| Format | **Digits-only**, length **12**, college-prefixed |
| Examples (non-secret) | `105325405005`, `105325405006`, … `105325405219` |
| Last-3 examples | `005`, `006`, `046`, `050`, … |

Operators who type From=`46` To=`50` are entering **full-string** bounds, not last-3 bands.

---

## 2. Student Number Range — live results

Authoritative compare: ordinal ignore-case on **full** `StudentNumber` (`AllocationScopeSelectionValidator.CompareStudentNumbers`).

| Range entry | Matched (of 235) | Interpretation |
|-------------|------------------|----------------|
| From=`1` To=`5` | **235 (all)** | Every number starts with `1…` and is lexicographically ≤ `5` → over-match |
| From=`46` To=`50` | **0** | Prefixed numbers like `1053…` are **not** between `"46"` and `"50"` |

**Last-3 intent (not implemented as population mode)** — if operators meant last-3 bands:

| Intended last-3 band | Matched |
|----------------------|---------|
| `001`–`005` | 1 (`…5005`) |
| `046`–`050` | 5 (`…5046`…`…5050`) |

This confirms Prompt 1 suspected defects **P2-POP-001** / **P2-POP-002**.

---

## 3. LastThreeDigits behavior

| Aspect | Result |
|--------|--------|
| Identifier | `groupingMode = "LastThreeDigits"` |
| Ordering | Live numbers sorted by last-3 key → **non-decreasing** (mirror of `StudentGroupingStrategy`) |
| First keys | `005,006,007,008,010,…` |
| Placement (Capacity) | **NOT EXECUTED** — `POST /allocation/simulate` → **403** for Admin |
| College band expectation (001–060→A …) | **Not proven live**; engine design (Prompt 1) places by occupancy balance, not digit bands |

---

## 4. Existing section assignments

| Metric | Value |
|--------|--------|
| With `currentSectionId` | **40** |
| Unassigned | **195** |
| By section | CA-A: **20**, CA-B: **20** |
| Other context sections | SCCA01 present with 0 seeded from this membership snapshot |

Already-sectioned students remain **in** the Allocation Context population (not excluded).

---

## 5. Capacity / All Eligible / Explicit

| Test | Result |
|------|--------|
| Context sections (eligible) | `SCCA01`, `CA-A`, `CA-B` |
| Capacities | Present on context (see evidence JSON) |
| Filtered-population + capacity simulate | **NOT EXECUTED** (403) |
| All Eligible (`targetSectionIds: null`) | **NOT EXECUTED** (403) |
| Explicit (`targetSectionIds: [one id]`) | **NOT EXECUTED** (403) |

---

## 6. Admin JWT & Allocation.Run

| Check | Result |
|-------|--------|
| Role claim | `Admin` |
| Permission claims | **73** keys (JSON array under `permission`) |
| Allocation keys present | **`Allocation.Scenario.Archive` only** |
| `Allocation.Run` | **ABSENT** |
| `Allocation.Approve` / `.Reject` / `.Export` | **ABSENT** |
| `Allocation.Test` / `.Simulation` | **ABSENT** (keys do not exist in product catalog) |
| `POST /allocation/simulate` | **403** |
| `POST /allocation/run` | **403** |
| `GET /allocation/context` | **200** (does not require Run) |

### Root cause (permission)

Admin is authenticated with an **ApplicationRole** permission set that does **not** include `Allocation.Run`.  
`JwtService` uses assigned ApplicationRole permissions when present (does not merge `PermissionKeys.All` for Admin in that path).  
`CanRunAllocation` requires claim `permission = Allocation.Run` (SuperAdmin bypass only).

### Preview disabled

UI: `canRun = hasPermission("Allocation.Run") && Boolean(runRequest)`.  
Admin → `canRun = false` → Preview / Test Allocation **disabled**.

### Simulation “allocation tests” message

UI warning: *“You need permission to run allocation tests.”*  
Actual gate: **`Allocation.Run`**, not a non-existent `Allocation.Test`.

---

## 7. Simulation persistence & StudentSection writes

| Item | Result |
|------|--------|
| Simulate persistence | **NOT EXECUTED** (403) — contract from Prompt 1: `PreviewAsync` → `RunAsync` (scenario persist) |
| Live StudentSection writes on simulate/run | **Contract PASS** — `AllocationEngineController` documents scenarios/drafts only; Prompt 2 did not call student-section mutate APIs |

---

## 8. Isolation

| Test | Result |
|------|--------|
| Cross-group (CA id=2 vs Finance id=1) | **PASS** — student-id overlap = 0 |
| Cross-semester (Sem III vs Sem I) | **PASS** — overlap = 0 |
| Cross-tenant | **NOT EXECUTED — DATA UNAVAILABLE** (single-college env) |

---

## 9. Browser validation

| Item | Result |
|------|--------|
| Admin login / Sections reachability | Executed (see `prompt2-browser-results.json`) |
| Full C→Population→Rules→Preview→Simulate→Approve | **NOT EXECUTED** — blocked by missing `Allocation.Run` |
| Automated tests ≠ browser substitute | Acknowledged |

---

## 10. Baseline automated tests / builds

| Suite | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| Allocation-related filter (AI29.1C / 10A / 24B* / AllocationEngine / AllocationScope) | **98** | **0** | **0** |

Builds (Prompt 2 baseline; no production edits):

| Build | Status |
|-------|--------|
| UI (`npm run build`) | **PASS** |
| API (`dotnet build`) | **FAIL (environmental)** — DLL copy locked by running `Abhyanvaya.API` + Visual Studio (MSB3027). Not a source compile defect; API process was already serving validation traffic. |

Skipped never counted as passed.

---

## 11. Evidence artifacts

| File | Purpose |
|------|---------|
| `prompt2-live-evidence.json` | Initial live probe + defect stubs |
| `prompt2-jwt-claim-shape.json` | JWT claim shape (no token) |
| `prompt2-admin-permission-probe.json` | 403 on simulate/run |
| `prompt2-semantics-without-run.json` | Format, ranges, Last3 order, assignments |
| `prompt2-browser-results.json` | Browser smoke |
| `scripts/ai29_1d_24b3_prompt2_*.mjs` | Validation harnesses (non-production) |

---

## 12. Production / DB / API changes

**None.** Prompt 2 validation gate only.

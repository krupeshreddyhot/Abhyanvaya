# AI29.1D.24B.3 — Prompt 3 Final Report

**Date:** 2026-08-15  
**Phase:** Allocation.Run authorization repair + live engine re-validation

---

## STATUS: **PASS**

Authorization established; unauthorized users denied; Preview/Simulation/Run execute; security boundaries not weakened; regression + Architecture Guard green; UI build green.

---

### Authorization: **PASS**

| Check | Result |
|-------|--------|
| Root cause | Live DB missing Allocation.* **Permission catalog** rows (only Archive existed) → ADMIN role could not link `Allocation.Run` |
| Fix | Idempotent `AllocationPermissionCatalogReconciler` (startup) + SQL `Apply_AI29_1D_24B3_AdminAllocationPermissions.sql` |
| Admin JWT | `Allocation.Run` **present** (full Allocation.* set linked) |
| Faculty (`teststaff1`) | `Allocation.Run` **absent**; simulate **403** |

### Simulation: **PASS**

`POST /allocation/simulate` HTTP 200; `succeeded=true`; scenario persisted; 180 recommendations (All Eligible / LastThreeDigits).

### Preview: **PASS**

Same simulate API (UI Preview path); HTTP 200; scenarioId present; section fills observed.

### LastThreeDigits: **OBSERVED**

- Ordering by last-3 key: sample non-decreasing (`last3OrderOk=true`).
- Placement: **Capacity occupancy balance**, not digit-band→section.
- All Eligible fill: CA-A 60/60, CA-B 60/60, SCCA01 60/60; **57** capacity-exhausted warnings (235 students, 180 seats).

### All Eligible: **OBSERVED**

`targetSectionIds=null` → recommendations target **CA-A, CA-B, SCCA01** (180 placed).

### Explicit Section: **OBSERVED**

`targetSectionIds=[SCCA01]` → **60** recommendations, all to **SCCA01** only.

### Already Assigned: **OBSERVED**

Context: **40** students with current section (CA-A 20 + CA-B 20). Simulate explanations include **“Kept in section (capacity available)”** for seeded students.

### Capacity: **OBSERVED**

Hard capacity 60/section respected; filtered population (10 full-number range) → **10** recommendations; over-population warns rather than overfilling.

### Governance: **PASS** (executed)

Scenario detail HTTP 200; review HTTP 200; `canApprove=false` (server governance — not forced).

### Security: **PASS**

- Faculty without `Allocation.Run` denied (403).
- Cross-tenant ApplicationRole leak test PASS (Faculty + foreign ADMIN role).
- JwtService not weakened; no IgnoreQueryFilters expansion beyond existing auth-time ApplicationRole join.
- Reconciler grants Allocation.* **only** to `Code=ADMIN` roles.

### Regression

| Suite filter | Passed | Failed | Skipped |
|--------------|--------|--------|---------|
| Prompt3 + ArchGuard/Prompt21 | **34** | **0** | **0** |
| AI29.1C + Prompt10A + 24B2 JWT + Prompt8A/9 | **75** | **0** | **0** |

### Build

| Build | Result |
|-------|--------|
| UI | **PASS** |
| API | **PASS** (built to `_build_p3/api`; running instance restarted with reconciler) |

### Architecture Guard: **PASS** (included in 34/0/0)

### Browser

| Item | Result |
|------|--------|
| Full UI stepper click-through after repair | **NOT EXECUTED** (API Preview/Simulation/Run authoritative for this gate) |
| Preview/Simulation/Run API | **PASS** (live) |
| Unauthorized Faculty API | **PASS** (403) |

Automated tests were **not** treated as a substitute for missing browser click-through; engine acceptance is API-proven.

---

## Production changes (do not omit)

| File | Change |
|------|--------|
| `Abhyanvaya.Infrastructure/Authorization/AllocationPermissionCatalogReconciler.cs` | **New** — startup idempotent Permission catalog + ADMIN links |
| `Abhyanvaya.Infrastructure/DependencyInjection.cs` | Register reconciler hosted service |
| `scripts/Apply_AI29_1D_24B3_AdminAllocationPermissions.sql` | Idempotent SQL repair |
| `Abhyanvaya.Application.UnitTests/.../AI29_1D_24B3_Prompt3_AllocationRunAuthorizationTests.cs` | **New** authz tests |
| `scripts/ai29_1d_24b3_prompt3_*.mjs` | Validation harnesses |

**Not changed:** Allocation Engine placement semantics, LastThreeDigits ordering contract, Attendance, tenant JWT isolation rules.

---

## Known defects (carried from Prompt 2 — not fixed here)

| Id | Title | Status |
|----|-------|--------|
| P2-POP-001 | Range 46–50 → 0 (ordinal full-string) | OPEN |
| P2-POP-002 | Range 1–5 → all (ordinal over-match) | OPEN |
| P2-STRAT-001 | UI “distribute” vs Capacity balance (no digit bands) | OPEN — **observed again** live |
| P2-UX-001 | “allocation tests” copy vs `Allocation.Run` | OPEN (low) |

P2-PERM-001 (**Admin missing Allocation.Run**): **CLOSED** by Prompt 3.

---

## Recommended next prompt

**Prompt 4 — Population Range UX / optional last-3 filter semantics + LastThreeDigits copy (or optional banded strategy)**  
Address P2-POP-001/002 and P2-STRAT-001 under Chief Architect decision. Do **not** replace existing `LastThreeDigits` ordering with a hard-coded roll-band placer without explicit ADR.

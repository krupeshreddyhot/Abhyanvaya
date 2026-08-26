# AI29.1D.24B.3 — Prompt 2 Defect Register

**Date:** 2026-08-15  
**Rule:** Document only — **do not fix** in Prompt 2.  
**Status legend:** OPEN (for Prompt 3 / Architect)

---

## P2-PERM-001 — Admin missing `Allocation.Run` (blocks Preview / Simulation / Run)

| Field | Value |
|-------|--------|
| Severity | **Critical** |
| Layer | JWT claims / ApplicationRolePermissions / `CanRunAllocation` policy |
| Authoritative contract | `PermissionKeys.AllocationRun` + `AddSetupManagePolicy(CanRunAllocation, …)` |
| Evidence | Admin JWT: 73 permissions; Allocation keys = `{ Allocation.Scenario.Archive }` only; simulate/run **403** |
| Impact | Preview/Test/Generate disabled; Prompt 2 engine placement cannot execute |
| Root cause | Assigned ApplicationRole permission set omits `Allocation.Run` (and most Allocation.* keys). JwtService emits assigned keys without Admin `PermissionKeys.All` merge when roles exist. |
| Proposed correction | Architect: grant Admin role `Allocation.Run` (+ Approve/Review as required) via RBAC seed/API; **or** clarify intentional restriction and provide a dedicated Allocation Admin persona |
| Regression risk | Medium — broad Admin grants; prefer explicit role permission update |
| Tests required | JWT claim assertion for Admin; policy integration; UI canRun true |
| Status | **OPEN** |

---

## P2-POP-001 — Range `46`–`50` matches zero students

| Field | Value |
|-------|--------|
| Severity | **High** |
| Layer | Population filter (`StudentNumberRange`) — UI + `AllocationScopeSelectionValidator` |
| Authoritative contract | Inclusive **ordinal full-string** From/To on `StudentNumber` |
| Evidence | Live numbers `105325405xxx`; matched **0 / 235**. Last-3 band `046`–`050` would match **5**. |
| Impact | Empty population; Continue blocked; operators believe filter is broken |
| Proposed correction | UX: require/paste full student numbers; help text; optional last-3 population mode (**not** a second LastThreeDigits placer) |
| Regression risk | High if compare semantics change without dual mode (Prompt 10A/20) |
| Tests required | Live matrix for prefixed digits; last-3 population mode tests if approved |
| Status | **OPEN** |

---

## P2-POP-002 — Range `1`–`5` matches all students

| Field | Value |
|-------|--------|
| Severity | **High** |
| Layer | Same population ordinal compare |
| Authoritative contract | Ordinal full-string inclusive |
| Evidence | Matched **235 / 235** (all start with `1…` and compare ≤ `5`) |
| Impact | Appears to return “everyone” |
| Proposed correction | Same as P2-POP-001 — clarify semantics / optional numeric or last-3 filter |
| Regression risk | Medium–High |
| Tests required | Digit-prefix over-match cases |
| Status | **OPEN** |

---

## P2-STRAT-001 — LastThreeDigits UI “distribute” vs Capacity placement

| Field | Value |
|-------|--------|
| Severity | **High** (business expectation gap) |
| Layer | UI catalog copy vs `StudentGroupingStrategy` + `CapacityAllocationStrategy` |
| Authoritative contract | LastThreeDigits = **order**; Capacity = **place** by lowest occupancy |
| Evidence | Catalog: “Distribute using last three digits…”. Live placement **not** executed (403). Prompt 1 code audit: no digit-band→section mapping. |
| Impact | College expectation (001–060→A …) not implemented by current strategy |
| Proposed correction | Correct copy **and/or** new **optional** configurable banded strategy **without** replacing existing `LastThreeDigits` |
| Regression risk | High if Capacity semantics replaced |
| Tests required | Preserve Case17 order; new banded acceptance if approved |
| Status | **OPEN** |

---

## P2-UX-001 — Simulation message implies `Allocation.Test`

| Field | Value |
|-------|--------|
| Severity | **Low** |
| Layer | UI copy (`EnterpriseAllocationWorkspace`) |
| Authoritative contract | Gate = `Allocation.Run` |
| Evidence | Message: “permission to run allocation tests”; key `Allocation.Test` does not exist |
| Impact | Misleading ops diagnosis |
| Proposed correction | Align copy to `Allocation.Run` |
| Regression risk | Low |
| Tests required | Copy / permission gate unit test |
| Status | **OPEN** |

---

## P2-CAP-001 — Capacity + filtered population (unverified live)

| Field | Value |
|-------|--------|
| Severity | **Medium** (suspected from Prompt 1) |
| Layer | `CapacityAllocationStrategy` remaining seats |
| Authoritative contract | Remaining = max − reserved; may ignore out-of-filter occupancy |
| Evidence | **NOT EXECUTED** live (403). Code suspicion retained from Prompt 1. |
| Impact | Possible over-assign into sections holding out-of-population students |
| Proposed correction | Architect review after Run permission restored |
| Regression risk | Medium |
| Tests required | Filtered population + pre-seeded sections fixture |
| Status | **OPEN — UNVERIFIED LIVE** |

---

## Intentionally not defects

| Item | Note |
|------|------|
| Missing `Allocation.Test` key | By design — product uses `Allocation.Run` |
| Cross-tenant not run | Data unavailable — not a product defect |
| Simulate ≡ Run persistence | Documented behavior — Architect may treat as enhancement later |

---

## Recommended Prompt 3 inputs (Architect)

1. Restore/grant **`Allocation.Run`** (and governance keys) for Admin allocation persona.  
2. Decide population UX vs last-3 filter mode for P2-POP-001/002.  
3. Decide whether banded last-3 placement is a **new optional strategy** (keep existing LastThreeDigits).  
4. Re-run Prompt 2 engine gates after permission fix (simulate/run/browser).  
5. Do **not** invent a duplicate “Roll Number / Last 3 Digits” placer under a new name without Architect approval.

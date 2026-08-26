# AI29.1D.24B.3A — Final Validation

**Date:** 2026-08-15  
**STATUS:** **CONDITIONAL PASS** (browser stepper not fully re-executed; API/JWT/RBAC proven)

## 1–5. Roles / permissions / matrix / decisions

- Existing roles: **ADMIN**, **FACULTY**  
- Allocation operator: **ADMIN**  
- ADMIN decision: least-privilege operator set; **no** automatic Approve/Reject/Archive/Export/Ops.View  
- Matrix: see `AI29_1D_24B_3A_PERMISSION_MATRIX.md`

## 6–8. Catalog / role provisioning / startup

- Catalog: reconciler inserts missing keys only — **PASS**  
- Role provisioning: seed + SQL + RBAC API — **PASS**  
- Startup: no blanket ADMIN grants — **PASS**

## 9. JWT

Admin Allocation claims: Run, Scenario.View/Create/Compare/Replay/Review.  
Absent: Approve, Operations.View, Reject, Export, Archive.  
Faculty: no Allocation.Run.

## 10–12. Simulation / Run / Governance

| Call | Admin | Faculty |
|------|-------|---------|
| simulate | **200** | **403** |
| run | **200** | **403** |
| approve | **403** (no Approve claim) | — |

Governance `canApprove` remains server-authoritative; Run does not imply Approve.

## 13. Operations.View

Admin lacks `Allocation.Operations.View` → Technical Details gated off in UI (`showTechnicalDetails=false`). API policies unchanged.

## 14. Tenant isolation

Unchanged JwtService same-tenant ApplicationRole join; cross-tenant leak tests still green in suite. No new IgnoreQueryFilters for role grants.

## 15. Browser

**NOT EXECUTED** (full Allocation Workspace click-through after 3A).  
API authorization for Preview/Sim/Run/Faculty denial proven live.

## 16. Regression

| Filter | Passed | Failed | Skipped |
|--------|--------|--------|---------|
| 24B.3A + Prompt3 auth + JWT 8A/9 + ArchGuard | **56** | **0** | **0** |
| AI29.1C + 15A + 24B2 + Prompt10A | **148** | **0** | **0** |

## 17. Builds

UI **PASS** · API **PASS** (restarted `_build_p3a/api`)

## 18. Architecture Guard

**PASS** (in 56/0/0 filter)

## 19. Known limitations

- No dedicated Approver ApplicationRole — Approve must be granted explicitly if needed.  
- Full UI browser matrix not re-run in 3A.  
- Prior Prompt 3 SQL that granted all Allocation.* to ADMIN is superseded by 3A least-privilege SQL/API.

## 20. Recommended next phase

AI29.1D.24B.3 population-range UX / LastThreeDigits copy (P2-POP/STRAT) — separate from RBAC.  
Optionally define explicit Approver grants via RBAC UI for colleges that need Admin approve.

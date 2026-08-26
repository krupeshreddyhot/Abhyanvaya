# AI29.1D.24B.4A.3 Prompt 1 — Allocation Context Consistency & Rebuild-State Diagnostic

**Workstream:** AI29.1D.24B.4A.3 — Allocation Context Consistency & Rebuild-State Diagnostic  
**Prompt:** 1 (Discovery / Diagnostic Only)  
**Date:** 2026-08-16  
**Tenant under inspection:** university `001` / college `1053`  
**Live scenario:** `e271865f-b459-4167-8d66-7fcd276f5c4f` (Session `e1b1f093-02d8-459e-8abd-c74e60c2023f`)  
**Scope:** AY=1, Course=1, Group=2, Semester=3  

**FINAL STATUS: PASS — root cause conclusively identified**

---

## 1. Executive Summary

After Generate Allocation, Review immediately shows **“Allocation needs to be rebuilt”** and disables Approve because the server compares a **stored context checksum** to a **freshly rebuilt context checksum**, and those checksums **never match** for a stable academic scope.

The checksum algorithm in `SectionAllocationContextBuilder.ComputeChecksum` includes **`ContextId`**, and `BuildCoreAsync` assigns **`ContextId = Guid.NewGuid()` on every rebuild**.

- Generate Allocation calls `_builder.BuildAsync` directly (no shared cache with Review).
- Review / Governance call `_builder.BuildAsync` again → new `ContextId` → new checksum → `contextStale = true` / `contextCurrent = false`.

Live proof: consecutive governance evaluations against the same unchanged scenario yield a **different** `currentContextVersion` (first 8 hex chars of `ContextId`) on every call, while the stored scenario `ContextVersion` remains fixed. Academic capacities and student count can be identical; the checksum still diverges.

**This is not a user workflow error.** It is a non-deterministic / volatile checksum design defect.

No production code, schema, data, governance rules, or RBAC were changed in this prompt.

---

## 2. Reproduction Evidence

| Observation | Value |
|---|---|
| ScenarioId | `e271865f-b459-4167-8d66-7fcd276f5c4f` |
| SessionId | `e1b1f093-02d8-459e-8abd-c74e60c2023f` |
| Run / execution status | Completed / Generated |
| Version | 1 |
| Lifecycle / Allocation Status | Review path shows Review; row `LifecycleStatus` = Generated |
| Stored `ContextChecksum` | `AFF15A6279CC3C3DE6630810C4732A1B44FB2E565EF5E5351DE55103129C843E` |
| Stored `ContextVersion` | `5fa572b0` (= `ContextId` prefix of generate-time context) |
| Scenario payload `ContextId` | `5fa572b0-cbcb-4411-80e2-0dd11719f08a` |
| Governance | `contextStale=true`, `canApprove=false` |
| Blocking reason | “This scenario was created using an earlier academic configuration and must be rebuilt before approval.” |
| UI copy | “Allocation needs to be rebuilt” / academic information changed |
| Refresh Status | Re-evaluates; still stale (expected under this defect) |

**Volatility proof (same scenario, no data mutation):**

| Call | `currentContextVersion` | `contextStale` |
|---|---|---|
| governance #1 | `8bd605e2` | true |
| governance #2 (~300ms later) | `94b14cfc` | true |
| detail #1 | `c55fdeed` | `contextCurrent=false` |
| detail #2 (~300ms later) | `4919c0ca` | `contextCurrent=false` |

Stored version stayed `5fa572b0` throughout.

Artifacts:  
`CursonModifiedFiles/.../AI29.1D.24B.4A.3/Prompt 1/live-readonly-evidence.json`  
`CursonModifiedFiles/.../AI29.1D.24B.4A.3/Prompt 1/governance-contextid-volatility.json`

---

## 3. Allocation Context Lifecycle

| Stage | What happens | File / method |
|---|---|---|
| **A. Context creation** | Scope validated; hierarchy, sections, capacities, students, faculty, subjects, rooms, policies, recommendations assembled; `ContextId = Guid.NewGuid()`; `GeneratedAt = UtcNow` | `SectionAllocationContextBuilder.BuildCoreAsync` |
| **B. Context normalization** | Section projections / capacities ordered by construction pipelines; checksum payload selects ordered projections implicitly via list order from EF/capacity engine | `BuildCoreAsync` + capacity/section services |
| **C. Checksum generation** | SHA-256 over JSON of `{ ContextId, SchemaVersion, Hierarchy, SectionIds, Capacities{SectionId,CurrentStrength,MaximumCapacity}, StudentCount, FacultyCount }` | `SectionAllocationContextBuilder.ComputeChecksum` |
| **D. Context persistence (optional cache)** | Wizard `GET /api/allocation/context` may cache via `IAllocationContextCache` (TTL 10 min). **Generate and Review do not read this cache.** | `AllocationPlatformController.GetContext`, `AllocationContextCache` |
| **E. Allocation generation** | `AllocationExecutionService.RunAsync` → `_builder.BuildAsync(scope)` → engine pipeline → scenario | `AllocationExecutionService.RunAsync` |
| **F. Version creation** | Persist session + scenario + version with `ContextChecksum` / `ContextVersion` (= `ContextId.ToString("N")[..8]`) | `AllocationExecutionService.PersistAsync` |
| **G. Review loading** | `GET .../scenarios/{id}` → `AllocationScenarioQueryService.GetDetailAsync` (+ nested `EvaluateAsync`) | `AllocationOperationsController`, `AllocationScenarioQueryService` |
| **H. Current-context checksum** | Fresh `_builder.BuildAsync(scope)` (new `ContextId`) | same builder |
| **I. Stale/rebuild determination** | `current.Checksum != row.ContextChecksum` → stale | `AllocationGovernanceAndDashboard.EvaluateAsync`; also `GetDetailAsync` sets `contextCurrent` |
| **J. Approval eligibility** | `canApprove = blockers.Count == 0`; stale adds blocker; Approve disabled when `!CanApprove` / rebuild preferred | `EvaluateAsync`, UI `AllocationGovernancePanel` / `presentAllocationIssue` |

---

## 4. Stored Checksum Source

| Item | Value |
|---|---|
| Entity | `AllocationEngineScenario` (also mirrored on `AllocationEngineSession`, `AllocationScenarioVersion`) |
| Property | `ContextChecksum` |
| DB column | `ContextChecksum` (string, max length per entity config ≤ 128) |
| Also stored | Scenario JSON field `AllocationScenario.ContextChecksum`; `ContextId`; `ContextVersion` |
| Populated when | `AllocationExecutionService.PersistAsync` after successful generate |
| Calculated by | `SectionAllocationContextBuilder.ComputeChecksum` at generate-time `BuildCoreAsync` |
| Timing | **Before** engine assignment work completes persistence; checksum is taken from the context built at the start of `RunAsync`, then copied onto the scenario via pipeline (`ContextChecksum = state.Context.Checksum`) |
| Mutable after generation? | **No** for the stored row value (immutable snapshot). Current rebuild checksum **does** change every rebuild |

Domain: `Abhyanvaya.Domain/Entities/Academic/AllocationEngineEntities.cs`

---

## 5. Current Checksum Source

| Item | Detail |
|---|---|
| Method / service | `ISectionAllocationContextBuilder.BuildAsync` → `BuildCoreAsync` → `ComputeChecksum` |
| Called from | `AllocationGovernanceAndDashboard.EvaluateAsync`; `AllocationScenarioQueryService.GetDetailAsync` |
| Inputs | `AllocationScopeRequest` from scenario row: AcademicYearId, CourseId, GroupId, SemesterId (+ tenant from `ICurrentUserService`) |
| Normalization | Scope must be all > 0; sections/students loaded for tenant |
| Algorithm | `JsonSerializer.Serialize(payload, WriteIndented=false)` → UTF-8 bytes → `SHA256` → `Convert.ToHexString` |
| Fields **included** | `ContextId`, `SchemaVersion`, `Hierarchy`, section ID list, capacities (`SectionId`, `CurrentStrength`, `MaximumCapacity`), `StudentCount`, `FacultyCount` |
| Fields **excluded** | Students IDs/order detail, rules/config, recommendations, policies text, rooms, `GeneratedAt`, metadata, health/readiness strings, allocation strategy config, band rules, etc. |
| Ordering | Whatever order `Sections` / `Capacities` lists have after build (not explicitly re-sorted inside `ComputeChecksum`) |
| Serialization | System.Text.Json default property naming; no custom converters observed on payload anon type |
| Null / defaults | Anon payload uses present values; empty collections serialize as `[]` |

```523:537:Abhyanvaya.Application/Academic/Allocation/SectionAllocationContextBuilder.cs
    private static string ComputeChecksum(SectionAllocationContext ctx)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ctx.ContextId,
            ctx.SchemaVersion,
            ctx.Hierarchy,
            Sections = ctx.Sections.Select(s => s.SectionId),
            Capacities = ctx.Capacities.Select(c => new { c.SectionId, c.CurrentStrength, c.MaximumCapacity }),
            StudentCount = ctx.Students.Count,
            FacultyCount = ctx.FacultyAssignments.Count,
        }, JsonOpts);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
```

```356:356:Abhyanvaya.Application/Academic/Allocation/SectionAllocationContextBuilder.cs
        var contextId = Guid.NewGuid();
```

---

## 6. Rebuild Required Logic

**Server expression (authoritative):**

```text
stale = !string.Equals(current.Checksum, row.ContextChecksum, OrdinalIgnoreCase)
→ blocker: "…earlier academic configuration and must be rebuilt…"
→ CanApprove = (blockers.Count == 0)
→ ContextStale = stale
→ ContextCurrent = !stale
```

Source: `AllocationGovernanceAndDashboard.EvaluateAsync` (approx. lines 98–140).

Additional blockers (independent): scenario checksum invalid; unresolved mandatory constraints; etc.

| Question | Answer |
|---|---|
| Server authoritative? | **Yes** |
| UI display only? | UI maps `contextStale` / `!contextCurrent` / blocker text via `presentAllocationIssue` — does not invent stale state |
| Approve disabled by? | Server `CanApprove=false` (+ UI rebuild preference) |
| Solely checksum? | Stale flag is solely stored-vs-current **context** checksum compare; approval also considers other blockers |
| Weakened in this prompt? | **No** |

UI mapping: `abhyanvaya-ui/src/utils/allocationAdministratorCopy.ts` → title “Allocation needs to be rebuilt”.

---

## 7. Generate → Review Trace

```text
Wizard Academic Scope
  → GET /allocation/context (may cache context C_cache for 10 minutes)
  → … configuration / simulate …
Generate Allocation
  → AllocationExecutionService.RunAsync
  → BuildAsync → BuildCoreAsync → ContextId = G_new, Checksum = H(G_new, …)
  → Engine run (scenario JSON only; no live StudentSection commit)
  → PersistAsync stores ContextChecksum=H(G_new,…), ContextVersion=prefix(G_new)
Review Allocation
  → GetDetailAsync
       → EvaluateAsync → BuildAsync → ContextId = R1_new ≠ G_new → checksum mismatch → stale
       → BuildAsync again → ContextId = R2_new ≠ R1_new (second rebuild in same request)
  → UI shows rebuild; Approve disabled
```

**Does Generate mutate checksum inputs?**

| Mutation | On Generate? | In checksum? |
|---|---|---|
| StudentSection live writes | **No** (scenario-only persist; live writes on Approve path) | N/A for immediate defect |
| Section occupancy (`CurrentStrength`) | Not by Generate itself | **Yes** (secondary volatility if occupancy changes elsewhere) |
| New `ContextId` on Review rebuild | Review always | **Yes — primary defect** |
| Scenario version / lifecycle | Creates v1 Generated | Not in context checksum |
| Section DisplayOrder / UpdatedDate | Not required for Generate | DisplayOrder not in checksum; UpdatedDate not in checksum |

**Critical answer:** Generate does **not** need to mutate academic data. Review’s rebuild alone changes `ContextId`, which is hashed → immediate “needs rebuild”.

---

## 8. Stored vs Current Checksum

| | Value |
|---|---|
| StoredChecksum | `AFF15A6279CC3C3DE6630810C4732A1B44FB2E565EF5E5351DE55103129C843E` |
| CurrentChecksum (any fresh BuildAsync) | **Different every call** (because `ContextId` differs) |
| Comparison | **StoredChecksum ≠ CurrentChecksum** |

Component causing mismatch (definitive): **`ContextId`** (volatile GUID included in hash).

Secondary risk components (same algorithm, not required to explain immediate failure): **`CurrentStrength`** (live occupancy), collection order of sections/capacities if non-stable, `StudentCount` / `FacultyCount` if population drifts.

Note: `GET /allocation/context` without `refresh=true` can return a **cached** context (stable id/checksum for up to 10 minutes). That cache is **not** used by Generate/Review governance paths, so it can **mask** volatility during wizard browsing while Review still fails.

---

## 9. Field-Level Context Diff

Comparing generate-time stored context identity vs Review rebuild (same academic scope):

| Field / component | Classification | Notes |
|---|---|---|
| Academic Year / Course / Group / Semester | SAME | Scope ids on scenario row |
| Hierarchy (scope projection) | SAME (expected) | Included in checksum |
| Section IDs set | SAME (expected) | Included |
| Section codes / DisplayOrder | SAME / N/A | Not in checksum |
| MaximumCapacity | SAME (live sample) | Included |
| CurrentStrength / occupancy | SAME or DIFFERENT | Included — **volatile** if enrollment changes |
| Student population IDs | N/A in checksum | Only **StudentCount** hashed |
| StudentCount | SAME in live samples | Included |
| FacultyCount | likely SAME | Included |
| Allocation rules / bands / ConfigJson | SAME / N/A | **Excluded** from context checksum |
| ScenarioId / SessionId | DIFFERENT purpose | Not in context checksum; Review uses correct scenario id |
| **ContextId** | **DIFFERENT every rebuild** | **Included — ROOT CAUSE** |
| GeneratedAt | DIFFERENT | Excluded from checksum |
| Timestamps / user / tenant / permissions | — | Tenant scopes queries; not hashed except indirectly via data |
| Trace / diagnostic fields | — | Excluded |

---

## 10. Non-Deterministic / Volatile Fields

| Field | In checksum? | Deterministic? | Impact |
|---|---|---|---|
| **ContextId (`Guid.NewGuid`)** | **YES** | **NO** | **Always mismatches across Generate vs Review** |
| CurrentStrength | YES | NO (live) | Can cause legitimate or spurious rebuild |
| StudentCount / FacultyCount | YES | Semi | Population drift |
| Section/capacity list order | YES (implicit) | Depends on query order | Potential secondary non-determinism |
| GeneratedAt | NO | NO | OK |
| Dictionary Metadata | NO | — | OK |
| JSON property order | Fixed by anon type | YES | OK |
| Culture / DateTime in checksum payload | No dates in payload | — | OK |

Canonical invariant **currently fails:**

```text
Generate(context) → Persist → Reload scope → Calculate checksum
⇏ StoredChecksum == ReloadedContextChecksum
```

even when academic data is bit-identical.

---

## 11. Version / Scenario Consistency

| Check | Result |
|---|---|
| Review ScenarioId == Generate ScenarioId | **Yes** (`e271865f-…`) |
| Review SessionId == Generate SessionId | **Yes** (`e1b1f093-…`) |
| Version loaded | **Version 1** (current); not an older N−1 mis-load |
| Stored ContextVersion | `5fa572b0` (generate-time ContextId prefix) |
| Review CurrentContextVersion | **New prefix every Evaluate/Detail** |
| Wrong scenario validation? | **No** — correct scenario; wrong **fresh ContextId** |

Defect is **not** “Review loads wrong version”; it is “Review validates against a newly minted context identity.”

---

## 12. Exact Root Cause

**Root cause:** Allocation context checksum is non-canonical because it hashes a per-build ephemeral `ContextId` (`Guid.NewGuid()` in `BuildCoreAsync`). Review/Approval always rebuild context via `BuildAsync`, minting a new `ContextId`, so `current.Checksum != stored.ContextChecksum` even when academic scope and capacity/student counts are unchanged. Server then sets `ContextStale` / blocks approval; UI correctly surfaces “Allocation needs to be rebuilt.”

**Contributing design gap:** Generate and governance bypass the allocation context cache used by `GET /context`, so they never share a stable context identity across the Generate→Review boundary.

**Secondary hazard (not required for this defect):** `CurrentStrength` in the checksum couples approval eligibility to live occupancy snapshots.

---

## 13. Evidence Supporting Root Cause

1. **Code:** `ContextId = Guid.NewGuid()` then `ComputeChecksum` includes `ctx.ContextId`.
2. **Live:** `currentContextVersion` changes on every governance/detail call for an immutable stored scenario.
3. **Live:** Stored checksum equals scenario payload checksum, but never equals a later rebuild checksum.
4. **Live:** `canApprove=false`, `contextStale=true`, blocker text matches checksum mismatch path only.
5. **Architecture:** Generate persists scenario only; does not need to alter occupancy to trigger the symptom.
6. **UI:** Copy is driven by server `contextStale` / `!contextCurrent`, not a client-only flag.

---

## 14. Recommended Corrective Design

*(Design only — not implemented in Prompt 1.)*

1. **Canonical academic checksum** must hash only stable academic identity + approved configuration inputs, e.g.:
   - SchemaVersion, Hierarchy (scope ids), ordered SectionIds, MaximumCapacity (and other policy caps if intentional), Student identity set or ordered student ids (if population is part of contract), Faculty assignment identity if required.
2. **Exclude** ephemeral fields: `ContextId`, `GeneratedAt`, request/user/trace ids, and almost certainly **live `CurrentStrength` / occupancy** unless product explicitly wants occupancy drift to force rebuild.
3. Keep `ContextId` as a correlation id for telemetry/UI, but **do not** include it in integrity checksum.
4. Stabilize ordering: sort section ids, capacity rows, and any collections before serialize.
5. Optionally: at Generate, persist a **canonical context payload** (or hash inputs) and compare Review against that freeze; or rebuild with identical canonicalization function.
6. **Do not** bypass rebuild validation, auto-approve, or weaken RBAC/governance gates — fix canonicalization so the gate measures real academic drift.

---

## 15. Files/Methods Requiring Change

| File | Method / area | Likely change |
|---|---|---|
| `SectionAllocationContextBuilder.cs` | `ComputeChecksum`, possibly `BuildCoreAsync` | Canonical payload; exclude `ContextId`; sort collections; reconsider `CurrentStrength` |
| `AllocationGovernanceAndDashboard.cs` | `EvaluateAsync` | Keep compare; may surface clearer diagnostics after fix |
| `AllocationScenarioQueryService.cs` | `GetDetailAsync` | Avoid double `BuildAsync` if expensive; keep semantics |
| `AllocationExecutionService.cs` | `PersistAsync` | Continue storing checksum; ensure it uses new canonical function |
| Tests (new) | Canonicalization unit tests | Same context different ContextId → same checksum; order permutations; null/default equivalence |
| UI | None required for root fix | Copy remains valid when true drift occurs |

---

## 16. Risks

| Risk | Notes |
|---|---|
| Recomputing historical checksums | Existing scenarios hashed with old algorithm remain “stale” until regenerated or migrated |
| Removing CurrentStrength | Approvals may proceed despite occupancy drift — product decision needed |
| Keeping CurrentStrength | Spurious rebuilds continue under concurrent enrollment |
| Cache semantics | Wizard cache vs Generate/Review path divergence must be documented or unified carefully |
| False sense of security | Fix must preserve tenant isolation and approval authority |

---

## 17. Regression Requirements

1. **Invariant:** Build → Persist checksum → Rebuild same scope with new ContextId → checksum **equal** (after fix).
2. Same sections/students/rules in different orders → same checksum (canonical sort).
3. Equivalent null/default representations → same checksum.
4. Genuine academic change (section added/removed, capacity max changed, student population contract change) → checksum **differs** → rebuild required.
5. Generate → immediate Review → `contextCurrent=true` when scope unchanged; Approve enabled if other gates pass.
6. Refresh Status does not flip-flop `currentContextVersion` solely due to new GUIDs.
7. Existing RBAC: Allocation.Run / Approve unchanged; no IgnoreQueryFilters; no approval bypass.
8. Faculty denial / tenant isolation regressions remain green.

Diagnostic establishment (this prompt): invariant **does not hold** today (proven live).

---

## 18. Explicit Statement — No Production Behavior Changed

**NO production behavior was changed.**

This prompt performed read-only code inspection and read-only live API diagnostics only. No production code, database schema, allocation engine behavior, checksum generation, approval rules, RBAC, query filters, or live student/section/allocation records were modified. No allocation was approved. No workaround was applied.

---

## FINAL STATUS

**PASS — root cause conclusively identified**

Primary defect: **volatile `ContextId` included in allocation context checksum**, causing Generate→Review checksum mismatch and permanent rebuild/approval block without real academic drift.

# AI29.1D.24B.1 Prompt 1 — Architecture Discovery & UX Leakage Audit

**Mode:** Discovery only — **no production code modified**.  
**Baseline:** AI29.1D.24B implemented and frozen except final UX hardening.  
**Date:** 2026-08-10

---

## 1. Executive summary

AI29.1D.24B removed most engine/governance marketing from the primary workflow, but **administrator-visible technical leakage remains** in:

- Success/error toast messages (`scenario`, `StudentSection`, `Engine`, sandbox IDs)
- Student Population and Section Capacity panels (“Allocation Engine”, “Capacity Engine”, API path text)
- Action labels with **semantic mismatch**: **Rebuild Allocation** does not rebuild; **Re-run Allocation** invokes **replay** API
- Allocation Operations page (support surface) still shows Scenario tabs/IDs/Replay copy
- Technical Details accordion (gated by `Allocation.Operations.View`) intentionally exposes GUIDs/checksum/`canApprove` — acceptable if kept collapsed and support-only

**No backend / API / schema / domain change is required** for Prompts 2–5. Hardening is UI copy + action semantics only, reusing existing `simulate` / `run` / `replay` / scenario-detail APIs.

---

## 2. Files inspected

| # | Path | Role |
|---|------|------|
| 1 | `abhyanvaya-ui/src/components/allocation/EnterpriseAllocationWorkspace.tsx` | 9-step workspace orchestration |
| 2 | `abhyanvaya-ui/src/components/allocation/AllocationStrategyConfigPanel.tsx` | Allocation Rules UI |
| 3 | `abhyanvaya-ui/src/components/allocation/AllocationPreviewPanel.tsx` | Preview / Test Allocation |
| 4 | `abhyanvaya-ui/src/components/allocation/AllocationGovernancePanel.tsx` | Review / Approve |
| 5 | `abhyanvaya-ui/src/pages/setup/AllocationOperationsPage.tsx` | Ops / Scenarios dashboard |
| 6 | `abhyanvaya-ui/src/components/allocation/CapacityViolationBanner.tsx` | Capacity issue banners |
| 7 | `abhyanvaya-ui/src/components/allocation/AllocationCapacityPanel.tsx` | Capacity step (also leakage) |
| 8 | `abhyanvaya-ui/src/components/allocation/StudentPopulationFilterPanel.tsx` | Population step (also leakage) |
| 9 | `abhyanvaya-ui/src/utils/allocationAdministratorCopy.ts` | Business copy helpers |
| 10 | `abhyanvaya-ui/src/utils/allocationGovernanceLifecycle.ts` | Lifecycle / blocker mapping |
| 11 | `abhyanvaya-ui/src/utils/allocationStrategyCatalog.ts` | Rule catalogs |
| 12 | `abhyanvaya-ui/src/utils/allocationReviewUx24B.test.ts` (+ related 24B tests) | Source contracts |
| 13 | `docs/AI29_1D_24B_*.md` | Prior 24B documentation |
| 14 | `abhyanvaya-ui/src/services/allocationOperationsService.ts` | Replay / governance API clients |

**Not classified as UX leakage:** TypeScript property names, comments, permission key constants, or test assertions that never render.

---

## 3. Remaining administrator-visible technical terminology

### 3.1 Normal workflow (high priority — all users with Section/Allocation access)

| Current user-visible wording | File:approx | Recommended administrator wording |
|------------------------------|-------------|-----------------------------------|
| `Simulation completed (scenario {guid}). Engine-produced recommendations only — no live StudentSection writes.` | `EnterpriseAllocationWorkspace.tsx` ~358–359 | `Test allocation completed. Proposed section assignments are shown below. Student records were not changed.` |
| `Simulation finished with errors. Review engine messages below.` | same ~359 | `Test allocation finished with errors. Review the messages below.` |
| `Run Preview or Simulation first to obtain a scenario id for Compare.` | ~376 | `Run Preview or Test Allocation first before comparing.` |
| `Compare completed (engine comparison report).` | ~384 | `Comparison completed.` |
| `No scenario to save. Run Preview or Simulation first.` | ~395 | `Nothing to save yet. Run Preview or Test Allocation first.` |
| `Allocation.Run permission required to save a sandbox draft.` | ~399 | `You need permission to save a draft.` |
| `Sandbox draft saved ({sandboxId}). No live StudentSection changes.` | ~407 | `Draft saved. Student records were not changed.` |
| `Scenario marked Reviewed.` / `Scenario rejected.` / `Scenario archived.` | ~443, ~500, ~516 | `Allocation marked as reviewed.` / `Allocation rejected.` / `Allocation archived.` |
| Population Alert: `Selection is sent to the Allocation Engine and resolved only against Allocation Context (not a parallel Students API).` | `StudentPopulationFilterPanel.tsx` ~61–62 | `Students are selected from the current academic scope. Final placement is decided by the server.` |
| `Refresh Capacity Engine` | `AllocationCapacityPanel.tsx` ~169 | `Refresh Capacity` |
| Chip: `Source: Section Capacity Engine` / `Allocation Context projection` | ~175 | `Source: Live capacity` / `Source: Scope capacity` |
| Alert mentioning Capacity Engine + `/sections/capacity APIs` | ~121–129, ~180–181 | Business capacity copy; hide API paths |
| Button **Rebuild Allocation** (stale) | `AllocationGovernancePanel.tsx` ~148 | See §7 — label must match real behavior |
| Button **Re-run Allocation** | same ~257 | See §8 — either rename to Replay semantics or wire to regenerate |
| Footer: `use Allocation Operations for archive, compare, or re-run` | `EnterpriseAllocationWorkspace.tsx` ~987 | Align with chosen Re-run/Replay wording |

### 3.2 Technical Details (medium — only if `Allocation.Operations.View`)

Shown when `showTechnicalDetails === true` (permission `Allocation.Operations.View`):

| Wording | Location |
|---------|----------|
| `Scenario reference: {scenarioId}` | Governance Technical Details |
| `Context version` / `Current context version` | same |
| `Checksum: …` | same |
| `Raw lifecycle: …` | same |
| `Governance evaluation: canApprove=… · contextStale=… · checksumInvalid=…` | same |
| JSON payload (`groupingMode`, `enabledStrategies`, `constraintPriorities`) | Strategy Technical Details |
| Trace table under “View Allocation Details” | Preview Technical Details |

**Recommendation:** Keep collapsed; soften labels (e.g. “Allocation reference” instead of “Scenario reference”; hide property names `canApprove`/`checksumInvalid` behind plain sentences). Do not show to users without Operations.View.

### 3.3 Allocation Operations page (support surface — medium/low)

| Current | Recommended |
|---------|-------------|
| Tab **Scenarios** | **Allocations** |
| Table column **Scenario** + truncated GUID | **Allocation** + short reference or date |
| Button **Replay** + message `Replay created scenario {id}` | **Replay Allocation** + business success text |
| Link **Rebuild Allocation** → Context Explorer | Align with workspace rebuild semantics (§7) |
| `Scenario Workspace` heading | `Allocation Workspace` |
| Heatmap title `Latest Scenario – Section Utilization` | `Latest Allocation – Section Utilization` |

### 3.4 Already acceptable (24B) — not leakage

- Step labels: Academic Scope → Allocation Rules → … → Approve Allocation  
- Banner via `ALLOCATION_WORKSPACE_BANNER`  
- Primary / Additional Allocation Rules  
- Required / Preferred / Informational display labels  
- Stale title: “Allocation needs to be rebuilt” (copy OK; **action** mismatched — §7)  
- Approve via `AcademicConfirmDialog` without `window.confirm`

---

## 4–6. Source locations → current → recommended

Consolidated in §3 tables. Highest-impact rendered strings for Prompt 2:

1. Simulation / draft / compare messages in `EnterpriseAllocationWorkspace`  
2. Population + Capacity panel engine/API copy  
3. Review/Reject/Archive fallback messages containing “Scenario”

---

## 7. Rebuild action behavior (semantic mismatch)

### Current UI

**Label:** `Rebuild Allocation`  
**Where:** Stale-context warning in `AllocationGovernancePanel`  
**Handler (workspace):**

```ts
onRebuildAllocation={() => {
  setActiveStep(0);
  setMessage("Update academic scope if needed, then regenerate the allocation.");
}}
```

### Actual behavior

| Step | What happens |
|------|----------------|
| API call | **None** |
| Allocation regenerate | **None** |
| Context rebuild | **None** |
| Navigation | Sets stepper to **Academic Scope** (step 0) |
| Follow-up | Toast instructs user to update scope and regenerate manually |

### Semantic mismatch

**Yes.** The label promises a rebuild; the action only **returns the user to Academic Scope**. Approval remains blocked until the user manually regenerates (Generate Allocation / Test Allocation → Allocation step).

### Operations page variant

`Rebuild Allocation` links to `/setup/academic/allocation-context` (Context Explorer) — also **navigation**, not an automatic rebuild API.

### Recommended UX (no new API)

Pick one consistent administrator model:

| Option | Label | Behavior |
|--------|-------|----------|
| **A (preferred)** | `Review Academic Scope` or `Update Scope & Regenerate` | Navigate to step 0; secondary CTA `Generate Allocation` when ready; copy explains regenerate is required |
| **B** | Keep `Rebuild Allocation` | On click: navigate to Allocation step **and** auto-call existing `runAllocation` / guided regenerate after context refresh — still no new endpoint |

Do **not** present “Refresh Status” as the fix for stale context (already improved in 24B).

---

## 8. Re-run / replay behavior

### Workspace button

**Label:** `Re-run Allocation`  
**Handler:** `doReplay` → `replayAllocationScenario(activeScenarioId)`  
**API:** `POST /allocation/scenarios/{id}/replay`  
**Permission:** `Allocation.Scenario.Replay`  
**Result message:** `Allocation re-run completed. Student records were not changed.`

### Actual semantics

This is the **existing replay** API (historical scenario unchanged; new execution/result returned). It is **not** a full “start over from Academic Scope” regenerate, and **not** a context rebuild.

### Semantic mismatch

**Mild.** “Re-run” may imply regenerating with current scope/rules; replay replays the **stored scenario**.

### Recommended UX

| Option | Label | Keep API |
|--------|-------|----------|
| **A (preferred)** | `Replay Allocation` | `POST …/replay` |
| **B** | Hide from normal admin; keep under Technical / Operations | same |
| **C** | If product wants true regenerate: label `Generate New Allocation` and call existing `runAllocation` | `POST /allocation/run` |

Operations page still says **Replay** + technical success with scenario GUID — soften in Prompt 3/4.

---

## 9. Permission implications

| UI capability | Existing permission | Change needed? |
|---------------|---------------------|----------------|
| View workspace | `Allocation.Operations.View` **or** `Section.View` | No |
| Simulate / Run / Draft | `Allocation.Run` | No |
| Review | `Allocation.Scenario.Review` | No |
| Approve | `Allocation.Approve` | No |
| Replay / Re-run button | `Allocation.Scenario.Replay` | No |
| Technical Details | `Allocation.Operations.View` | No — do not add keys |
| Ops page | `Allocation.Operations.View` / Scenario.View | No |

Approval eligibility remains **server** `governance.canApprove` (implementation value; must not be displayed as a property name in normal UI).

---

## 10. APIs involved (existing only)

| Action | Existing client | Route (conceptual) |
|--------|-----------------|--------------------|
| Test / Preview | `simulateAllocation` | allocation simulate |
| Generate Allocation | `runAllocation` | allocation run |
| Scenario detail / refresh | `getAllocationScenarioDetail` | scenarios/{id} |
| Approve / Review / Reject / Archive | existing ops clients | governance endpoints |
| Re-run (current) | `replayAllocationScenario` | scenarios/{id}/replay |
| Compare | `compareAllocation` / `compareAllocationScenarios` | existing |
| Capacity | `getSectionOccupancy` / `getCapacityPolicy` | sections capacity |

**Rebuild today:** no API.

---

## 11. Confirmation — no backend / API / schema change required

| Item | Required for 24B.1? |
|------|---------------------|
| Database schema | **NONE** |
| New entities | **NONE** |
| New allocation APIs | **NONE** |
| New governance APIs | **NONE** |
| New permissions | **NONE** |
| Engine / governance domain changes | **NONE** |

UI may only remap copy and align button labels with existing client calls / navigation.

---

## 12. Recommended implementation sequence (Prompts 2–5)

| Prompt | Focus |
|--------|--------|
| **2** | Sanitize workspace toast/error strings; Population + Capacity panel copy; remove Scenario/Engine/StudentSection/API-path leakage from normal path |
| **3** | Fix **Rebuild** semantics (label + helper + optional guided regenerate using existing run); fix **Re-run** → Replay labeling (or regenerate path) |
| **4** | Soften Technical Details labels; Allocation Operations page Scenario→Allocation copy; keep GUIDs support-only |
| **5** | Tests + docs (`AI29_1D_24B_1_*.md`) + regression + artifact copy; architecture guard unchanged |

---

## Discovery report (Prompt 1 close-out)

| Item | Finding |
|------|---------|
| **Files inspected** | 14 primary surfaces listed in §2 |
| **Technical leakage** | Residual in toasts, Population/Capacity panels, Operations page; Technical Details OK if gated |
| **Rebuild semantics** | **Mismatch** — navigates to Academic Scope only; does not rebuild |
| **Re-run semantics** | Calls **replay** API (`POST …/replay`), not full regenerate |
| **API / DB / domain change required?** | **No** |

**Production code modified in this prompt:** **NONE**

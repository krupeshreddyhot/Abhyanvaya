# AI29.1D.24B Prompt 1 — Architecture Discovery

**Mode:** UI presentation only — no API / DB / engine changes.  
**Frozen:** AI29 through AI29.1D.24A architecture.

---

## 1. Surface under change

| Surface | Path | Role |
|---------|------|------|
| Enterprise Allocation Workspace | `abhyanvaya-ui/src/components/allocation/EnterpriseAllocationWorkspace.tsx` | 9-step guided workflow (Sections → Students) |
| Allocation Rules panel | `AllocationStrategyConfigPanel.tsx` | groupingMode + strategies + constraintPriorities |
| Preview / Simulation | `AllocationPreviewPanel.tsx` | engine result table |
| Review / Approve | `AllocationGovernancePanel.tsx` | AI29.1C.5A governance UI |
| Capacity banners | `CapacityViolationBanner.tsx` | engine constraint surfacing |
| Catalog helpers | `allocationStrategyCatalog.ts` | labels / payload shaping |
| Governance helpers | `allocationGovernanceLifecycle.ts` | lifecycle display + blockers |

**Out of scope for redesign:** Allocation Engine, SectionAllocationContext builder, capacity engine, governance services, Attendance, Scheduling, Subject Master, Program/Course APIs.

---

## 2. Existing contracts (must keep consuming)

| Concern | Contract |
|---------|----------|
| Simulate / Run | `POST` via `allocationPlatformService` |
| Scenario governance | `allocationOperationsService` approve/review/reject/archive |
| Approval authority | `governance.canApprove` + `blockingReasons` (+ flags) |
| Scope | `AcademicUiContext` + `AcademicScopeSelector` |
| Strategies payload | `{ groupingMode, enabledStrategies, constraintPriorities }` |

UI must **not** recalculate approval, scores, or placements.

---

## 3. Where technical terms leak today

| Location | Examples |
|----------|----------|
| Workspace banner | “Guided AI29.1C Allocation Engine + AI29.1C.5A…” |
| Step labels | Allocation Strategy, Scenario, Review — Governance Lifecycle |
| Strategy panel | Pipeline strategies, Engine payload preview, grouping mode, constraintPriorities |
| Governance panel | canApprove, Raw lifecycle, Checksum, Flag: stale context, Scenario GUID |
| Preview | Allocation Engine, Score breakdown, Allocation trace (engine) |
| Simulation step | `POST /allocation/simulate` |

---

## 4. Insertion points (24B)

1. **Terminology helpers** — pure UI mappers (no API).  
2. **Strategy panel** — business labels; Advanced + Technical Details collapsed.  
3. **Governance panel** — Allocation Status, business blockers, stale rebuild CTA, Approve confirm.  
4. **Workspace** — step rename + banner + Simulation/Allocation copy.  
5. **Preview** — business column labels; trace under Technical Details.  

---

## 5. Permissions (reuse only)

| Audience | Existing key |
|----------|----------------|
| Normal ops | `Section.View` / allocation run-approve keys |
| Technical Details optional | `Allocation.Operations.View` already gates ops |

No new permission keys.

---

## 6. Non-goals

- No new APIs / entities / DB migrations  
- No AttendanceSessionResolver changes  
- No Subject Master Section coupling  
- No React-side approval rules  

---

## 7. Expected docs (later prompts)

- `AI29_1D_24B_ALLOCATION_REVIEW_UX.md`  
- `AI29_1D_24B_TECHNICAL_DETAIL_SEPARATION.md`  
- `AI29_1D_24B_FINAL_VALIDATION.md`  

# AI29.1D.24B.3 — Prompt 1: Allocation Workflow Architecture Discovery

**Date:** 2026-08-15  
**Mode:** Read-only discovery — **no production code changes**  
**Phase:** Allocation Workflow Semantics & Existing Strategy Validation

---

## 1. Current architecture

### Mount / orchestration

| Layer | Artifact |
|-------|----------|
| UI entry | `SectionsPage` → Students tab → `EnterpriseAllocationWorkspace` |
| UI orchestrator | `abhyanvaya-ui/src/components/allocation/EnterpriseAllocationWorkspace.tsx` |
| Engine contract | `IAllocationEngine` (`EngineCode` = `"AI29.1C"`) |
| Engine | `AllocationEngine` → `AllocationPipeline` |
| Ordering | `IStudentGroupingStrategy` / `StudentGroupingStrategy` |
| Placement | `CapacityAllocationStrategy` (pipeline code `"Capacity"`) |
| Context | `SectionAllocationContextBuilder` |
| Population filter | `AllocationPopulationSelection` + `AllocationScopeSelectionValidator` + `AllocationContextScopeApplier` |
| Run | `AllocationExecutionService.RunAsync` |
| Simulate / Preview | `AllocationSimulationService.PreviewAsync` → **calls `RunAsync`** |
| Governance | `AllocationOperationsController` + `AllocationGovernanceService` / lifecycle |

### Principle (ADL / existing)

UI must **not** compute placement. Authoritative decisions are server-side Allocation Engine + governance APIs.

---

## 2. Workflow sequence

`EnterpriseAllocationWorkspace` stepper (9 steps):

1. **Academic Scope** — Program/Course/Group/Semester (+ AY) via academic UI context  
2. **Student Population** — filter modes → `populationSelection`  
3. **Allocation Rules** — `groupingMode` + `enabledStrategies` + constraint priorities  
4. **Section Capacity** — capacity table + **Target Sections** (All Eligible vs Explicit)  
5. **Preview** — `POST /allocation/simulate` (stay on preview)  
6. **Simulation** — same simulate API (advance to simulation step / Test Allocation)  
7. **Allocation** — `POST /allocation/run` (Generate) or reuse tested scenario  
8. **Review Allocation** — mark reviewed / compare / replay  
9. **Approve Allocation** — governed approve (server `canApprove`)

Supporting load on scope continue:

- `GET /allocation/context`
- `GET /allocation/readiness`
- `GET /allocation/health`
- `GET /allocation/validation`
- `GET /allocation/grouping-modes`

---

## 3. Authoritative layer for each decision

| Decision | Authoritative layer | Not authoritative |
|----------|---------------------|-------------------|
| Who is in academic pool | `SectionAllocationContextBuilder` (Course/Group/Semester students) | React chips alone |
| Population subset | `AllocationScopeSelectionValidator` + applier from `populationSelection` | UI matched-count preview (mirrors semantics) |
| Target sections | `TargetSectionIds` null = all context sections; else explicit ids validated against context | Year-wide section catalog |
| Student ordering | `StudentGroupingStrategy` + `groupingMode` | UI explanations |
| Seat assignment | `CapacityAllocationStrategy` | Grouping mode “distribute” copy |
| Soft attributes / score | Later pipeline strategies + Scoring | — |
| Persist scenario | `AllocationExecutionService` | — |
| Review / Approve / Replay | Ops lifecycle + governance services | Client inventing approval rules |
| Permissions | JWT permission claims + API policies | Hidden buttons alone |

---

## 4. Existing allocation strategies

### Grouping modes (`AllocationGroupingModes`) — ordering only

| Code | UI label (catalog) |
|------|--------------------|
| `StudentNumber` | Student Number |
| **`LastThreeDigits`** | **Student Number (Last 3 Digits)** |
| `StudentNumberRange` | Student Number Range *(ordering alias — see §6)* |
| `Alphabetical` | Alphabetical Order |
| `Gender`, `Merit`, `Scholarship`, `MinorSubject`, `Language`, `Transport`, `Hostel`, `ElectiveCombination` | matching labels |

Source: `AllocationModels.cs`, UI `allocationStrategyCatalog.ts`.

### Pipeline strategies (`AllocationStrategyCodes`) — placement / soft rules

`Validation`, **`Capacity`**, `Policy`, `Gender`, `Language`, `Scholarship`, `Elective`, `Transport`, `Hostel`, `Merit`, `Scoring`

**There is no second “Roll Number / Last 3 Digits” placer.** Last-3 is an existing grouping mode only.

---

## 5. Student Number (Last 3 Digits) — exact behavior

| Item | Value |
|------|--------|
| Exact class | `StudentGroupingStrategy` |
| Exact strategy identifier | **`LastThreeDigits`** (`AllocationGroupingModes.LastThreeDigits`) |
| Role | **Ordering only** — not banded section placement |
| Input contract | `AllocationPipelineConfig.GroupingMode = "LastThreeDigits"` |
| Ordering | `OrderBy(LastThreeKey(StudentNumber))` → then full `StudentNumber` → then `StudentId` |
| `LastThreeKey` | Trim; if length ≤ 3 use whole string; else last 3 characters |
| Section assignment | **Not performed by this mode.** Placement is `CapacityAllocationStrategy` |
| Capacity interaction | Ordered list is consumed by capacity placer (lowest occupancy ratio, then section code, then id) |
| Tie-breaking | Last3 key → full student number → student id (ordering); placement ties → occupancy → code → id |
| Final section fewer students | **Not** “remainder dumps into last section.” Unplaced students get capacity-exhausted **warnings** |
| Already-sectioned students | Included in context; capacity strategy **seeds** them into current section when seats remain |
| No eligible section | Capacity strategy errors: `"No eligible sections for capacity allocation."` |

### UI vs engine inconsistency

Catalog explanation:

> “Distribute students using the last three digits of the student number.”

Engine reality: **sort by last three digits**, then **balance by capacity**.  
College operational expectation (001–060 → A, 061–120 → B, …) is a **validation scenario** that is **not** implemented as digit-band → section mapping in the current engine.

---

## 6. Student Number Range — exact semantics

**Student Number Range is two different concepts sharing one string.**

### A. Population filtering (primary meaning for “range returns N students”)

| Item | Value |
|------|--------|
| Mode | `AllocationPopulationModes.StudentNumberRange` = `"StudentNumberRange"` |
| Layer | Population filter |
| Inputs | `fromStudentNumber`, `toStudentNumber` (inclusive) |
| Compare | **Ordinal** ignore-case on **full** `StudentNumber` (`CompareStudentNumbers`) — **not** numeric last-3 |
| Empty student number | Excluded |
| Authoritative | `AllocationScopeSelectionValidator.IsInRange` |
| UI mirror | `allocationPopulationFilter.ts` (`isStudentNumberInRange`) |

**Not** an allocation placement strategy.

### B. Grouping mode (same code string)

| Item | Value |
|------|--------|
| Mode | `AllocationGroupingModes.StudentNumberRange` |
| Behavior | **Identical to `StudentNumber` ordering** (full string sort) |
| Does **not** apply From/To | Range filter must be set on Student Population step |

Catalog note correctly says it orders within a range selected on the population step — but selecting only the grouping mode without population filter does **not** filter.

### Classification answer

| Question | Answer |
|----------|--------|
| Population filtering? | **Yes** (when used as `populationSelection.mode`) |
| Allocation strategy? | **Ordering alias only** (when used as `groupingMode`) |
| Both? | Same identifier, **different layers** — easy to confuse |
| Banded placement? | **No** |

---

## 7. Target-section semantics

| UI | Wire | Server |
|----|------|--------|
| All eligible sections | `targetSectionIds: null` | All sections in Allocation Context |
| Explicit selection | non-empty `number[]` | Must exist in context; applied by scope applier |
| Explicit empty | Continue blocked in UI; empty treated as null on wire in workspace builder | — |

Eligible list = **Allocation Context sections only** (fail-closed; Prompt 24B.2).  
Lifecycle `Merged`/`Split`/`Archived`/`Closed` excluded at capacity placement.

---

## 8. Capacity semantics

`CapacityAllocationStrategy`:

1. Eligible sections ordered by code/id.  
2. Remaining seats = `max(0, MaximumCapacity - ReservedSeats)` (or large default if max ≤ 0).  
3. **Does not subtract CurrentStrength of students outside the filtered population** when computing remaining.  
4. Seed already-assigned in-scope students into `CurrentSectionId` if seats remain.  
5. Place ordered unassigned students into section with **lowest occupancy ratio**, then code, then id.  
6. Exhausted → warning, no assignment.

Capacity panel UI also loads live occupancy (`getSectionOccupancy`) + policy for display; placement authority remains the engine.

---

## 9. Preview semantics

| Item | Behavior |
|------|----------|
| UI buttons | Preview + Test Allocation |
| API | Both → `POST /api/allocation/simulate` |
| Service | `AllocationSimulationService.PreviewAsync` → **`AllocationExecutionService.RunAsync`** |
| Persistence | Simulate path **persists** scenario (same run path; telemetry name differs) |
| Permission | `Allocation.Run` (`CanRunAllocation`) |
| UI disable | `!canRun` **or** `!runRequest` **or** loading |

There is **no** separate dry-run engine path and **no** `Allocation.Preview` permission key.

---

## 10. Simulation semantics

| Item | Behavior |
|------|----------|
| Step copy | “See how students would be distributed… without changing student records.” |
| Live StudentSection writes | Not performed on approve of draft (approval creates draft; live writes are separate governance path) |
| Simulate vs Run | Simulate uses same `RunAsync` persistence for scenarios |
| Accept/Reject sim | Separate endpoints under `Allocation.Run` |
| Missing permission message | UI: *“You need permission to run allocation tests.”* when `!hasPermission("Allocation.Run")` |

There is **no** `Allocation.Test` / `Allocation.Simulation` permission key.

---

## 11. Permission model

### Keys relevant to workflow

| Key | Used for |
|-----|----------|
| `Allocation.Operations.View` / `Section.View` | Workspace entry |
| **`Allocation.Run`** | Preview, Test Allocation, Generate, sandbox write, simulate accept/reject |
| `Allocation.Scenario.Review` | Mark reviewed |
| `Allocation.Approve` | Approve (+ server `canApprove`) |
| `Allocation.Reject` | Reject |
| `Allocation.Scenario.Archive` / `.Replay` / `.Compare` | matching actions |
| `Allocation.Export` | Cataloged; not wired in workspace buttons |

### Answers to investigation J–M (discovery-level)

| Q | Discovery finding |
|---|-------------------|
| J. Why Simulation reports missing allocation-test permission? | UI gates on **`Allocation.Run`**. Message wording says “allocation tests,” which can look like a missing `Allocation.Test` key even when the real gate is `Allocation.Run`. |
| K. Does Admin have required permissions? | **Must be verified live** against JWT claims for the college Admin user (Prompt 1 does not mutate). Admin legacy fallback may differ from ApplicationRole-assigned keys. |
| L. Are permissions in claims? | Requires live token inspection (Prompt 2+). Architecture: claims emitted by `JwtService` from ApplicationRolePermissions or legacy sets. |
| M. UI vs backend vs intentional? | Disable when `!Allocation.Run` is **intentional** shared gate. Possible defects: (1) Admin role missing `Allocation.Run` in ApplicationRolePermissions while UI expects it; (2) message implies a non-existent `Allocation.Test` permission; (3) `runRequest` null also disables buttons even when permission is present. |

---

## 12. Population & already-sectioned (investigation A–C)

| Q | Finding |
|---|----------|
| A. What students belong to the allocation population? | All students in `(Tenant, CourseId, GroupId, SemesterId)` from context builder, then filtered by `populationSelection`. |
| B. What does Student Number Range mean? | Inclusive ordinal filter on **full** student number strings (population), or full-number sort (grouping alias). |
| C. Already-sectioned included or excluded? | **Included** by default. Capacity seeds them into current section when capacity allows. |

---

## 13. Suspected defects / inconsistencies (code-backed; not yet fixed)

### D. Range 46–50 returns zero

**Likely root cause:** Ordinal full-string match. Values like `21CS046` / `20240046` are **not** between `"46"` and `"50"`. Last-3 digits are **not** used for population range.

| Field | Value |
|-------|--------|
| Layer | Population filter (UI + `AllocationScopeSelectionValidator`) |
| Contract | Full `StudentNumber` ordinal range |
| Impact | Operators entering last-3 style ranges get empty population |
| Proposed correction | Clarify UX / optional last-3 population mode (future prompt); do **not** invent a second Last-3 placer |
| Regression risk | Changing compare to numeric-last-3 would change Prompt 10A/20 range tests |
| Tests | Existing Prompt 10A uses alphanumeric ranges like `A10`–`A2` |

### E. Range 1–5 apparently returns all / many students

**Likely root cause:** Ordinal `"1" ≤ s ≤ "5"` includes `"15"`, `"100"`, `"2"`, `"4999"`, etc.

| Field | Value |
|-------|--------|
| Layer | Same population compare |
| Impact | Looks like “everyone” when numbers share digit prefixes |
| Proposed | UX guidance + optional numeric/last-3 filter semantics (later prompt) |

### F. How Last 3 Digits allocates

Orders by last 3; **Capacity** balances — does **not** map 001–060→A. UI “Distribute…” copy overstates.

### G. Capacity effect

Hard seats and occupancy balance dominate placement; grouping only feeds order.

### H. All Eligible vs Explicit

`null` vs explicit ids — Prompt 24B.2 gates preserved.

### I. Preview / Test disabled

`!Allocation.Run` **or** `!runRequest` (scope/population invalid / mode unavailable) **or** loading.

### Additional inconsistencies

1. **Dual `StudentNumberRange` identity** (population vs grouping).  
2. **Simulate ≡ Run persistence** vs UI “without changing records” (records = StudentSections; scenarios still persist).  
3. **Partial population + capacity** may ignore occupancy of out-of-filter students when computing remaining seats.  
4. **No remainder-to-last-section** for digit-band style expectations.

---

## 14. Defect register template (for later prompts)

For each confirmed defect, later prompts must fill:

- root cause  
- layer  
- authoritative contract  
- impact  
- proposed correction  
- regression risk  
- tests required  

Prompt 1 does **not** authorize production fixes.

---

## 15. Existing tests (read-only reference)

| Suite / file | Relevance |
|--------------|-----------|
| `AI29_1C_AllocationEngineTests.cs` | Determinism, ordering, capacity, draft no live writes |
| `AI29_1D_Prompt10A_AllocationScopeTests.cs` | Population range, targets, facets |
| `AI29_1D_Prompt20_RegressionSuiteTests.cs` | Case16 range; Case17 Last3 **order** `A100/B201/C099/D010` |
| UI `allocationPopulationFilter.test.ts`, `allocationStrategyCatalog.test.ts`, `allocationTargetSectionSelection.test.ts` | Client mirrors |
| Docs | `AI29_1C_STRATEGIES.md`, `AI29_1C_ALLOCATION_ENGINE.md`, `AI29_1D_ALLOCATION_PREVIEW.md` |

AI29.1C/1D tests **do not** assert digit-band → section placement.

---

## 16. Recommended next steps (Prompt 2+)

1. **Live Admin claim probe** — confirm presence/absence of `Allocation.Run` (and Approve/Review) in JWT; map ApplicationRole vs legacy Admin.  
2. **Live population probes** — reproduce ranges `46–50` and `1–5` against real student numbers; document actual `StudentNumber` samples.  
3. **Semantic clarification decision** (Chief Architect):  
   - Keep LastThreeDigits as **ordering + capacity** (document/correct UI copy), **or**  
   - Add an **explicit configurable banded strategy** without replacing existing LastThreeDigits.  
4. **Student Number Range UX** — prevent dual-meaning confusion; guide full-number vs last-3 entry.  
5. **Permission message** — align “allocation tests” copy with `Allocation.Run`.  
6. **Do not** create a duplicate Roll Number / Last 3 Digits strategy.

---

## 17. Investigation checklist (A–N) summary

| # | Question | Prompt 1 answer |
|---|----------|-----------------|
| A | Population membership | Context C/G/S students → population filter |
| B | Student Number Range meaning | Full-string ordinal inclusive filter (and/or grouping sort alias) |
| C | Already-sectioned | Included; seeded if capacity allows |
| D | 46–50 → zero | Ordinal full-number mismatch with last-3 style entry (suspected) |
| E | 1–5 → many/all | Ordinal over-match (suspected) |
| F | Last 3 Digits allocate | Order by last 3; place via Capacity balance |
| G | Capacity | Hard seats + lowest occupancy placement |
| H | All vs Explicit | `targetSectionIds` null vs ids |
| I | Preview/Test disabled | `Allocation.Run` and/or `runRequest` |
| J | Simulation permission message | Gates on `Allocation.Run`; wording suggests “test” permission |
| K | Admin permissions | Needs live claim verification |
| L | Claims present | Needs live token verification |
| M | Bug vs intentional | Mixed — intentional Run gate; possible claim/copy/`runRequest` issues |
| N | Complete workflow correct? | Engine path coherent; **semantics vs college digit-band expectation diverge**; range ordinal semantics surprise operators |

---

## 18. Freeze / non-goals (Prompt 1)

- No UI/API/DB production modifications  
- No second Last-3 / Roll Number strategy  
- No redesign of Allocation Engine / SectionGroup / AttendanceSessionResolver  
- Preserve AI29.1D / AI22 / AI30 / AI31 behavior  

**Prompt 1 status:** Discovery complete — ready for Prompt 2 (live semantics / permission validation) under Chief Architect direction.

# AI29.1D.24B.2 — Prompt 1 Architecture Discovery

**Date:** 2026-08-10  
**Mode:** Discovery only — no production code changes  
**Phase:** Target Section Scope & Explicit Selection Hardening

## Problem statement

When an administrator selects Academic Year → Program → Course → Group → Semester in the Enterprise Allocation Workspace, the Target Sections UI can display Sections outside the selected academic scope (for example, Finance sections while Group = Computer Applications).

Expected: Target Sections list = only Sections authoritative for the current Allocation Context scope. Explicit selection must be a first-class mode (all eligible **or** one-or-more selected).

## Current behavior (answers 1–15)

### 1. Where Academic Scope is selected

| Item | Location |
|------|----------|
| Workspace | `abhyanvaya-ui/src/components/allocation/EnterpriseAllocationWorkspace.tsx` |
| Host | `SectionsPage` Students / Allocation tab |
| Selector | `AcademicScopeSelector` (`fields`: academicYear, program, course, group, semester) |
| State | `useAcademicUi().selection` |
| Engine scope | `{ academicYearId, courseId, groupId, semesterId }` — **Program is UX cascade only**, not in `AllocationScope` |

### 2–3. Where eligible Sections are loaded / which API

| Surface | Source | Endpoint |
|---------|--------|----------|
| Target Section checkboxes | Allocation Context `context.sections` | `GET /api/allocation/context?academicYearId&courseId&groupId&semesterId` |
| Capacity table rows | Capacity occupancy (+ optional context fallback) | `GET /api/sections/capacity/occupancy?academicYearId&semesterId` |
| Global academic Section field (not Target UI) | `AcademicUiContext` → `listSections` | `GET /api/sections?...` |

Builder: `SectionAllocationContextBuilder` via `AllocationPlatformController.GetContext`.

### 4. Is the current API scoped?

| API | Year | Program | Course | Group | Semester |
|-----|------|---------|--------|-------|----------|
| Allocation Context sections | Yes | No (course.ProgramId only) | Yes | Yes | Yes |
| Capacity occupancy | Yes | No | **No** | **No** | Yes |
| `GET /api/sections` | Optional | No | Optional | Optional | Optional |

Context section query (authoritative for Target checkboxes):

```csharp
s.TenantId == tenant
&& s.AcademicYearId == scope.AcademicYearId
&& s.CourseId == scope.CourseId
&& s.GroupId == scope.GroupId
&& s.SemesterId == scope.SemesterId
```

### 5–6. `targetSectionIds` creation and storage

| Layer | Behavior |
|-------|----------|
| UI state | `useState<number[] \| null>(null)` — `null` = all eligible |
| Capacity panel | “All eligible” → `null`; “Explicit selection” → **all** context section IDs; checkboxes mutate array; empty → `[]` |
| Run/simulate request | Non-empty sorted ids; otherwise `null` |
| Backend config | `AllocationPipelineConfig.TargetSectionIds` (`null`/empty = all context sections) |
| Persistence | Scenario `ConfigJson` via existing execution/governance path |

### 7. AI29.1D.10A validation

- `AllocationScopeSelectionValidator`: explicit ids not in context → error `"Target section id {id} is not present in the Allocation Context."` — **fail-closed**, does not silently drop.
- `null`/empty target list → resolve **all** context sections (`"All eligible sections"`).
- `AllocationContextScopeApplier` filters context to resolved section set after validation.
- Engine order: Validate → Apply → pipeline.
- Covered by `AI29_1D_Prompt10A_AllocationScopeTests`.

### 8. “All eligible sections” today

`targetSectionIds === null` → UI shows all context section checkboxes as checked; run sends `null`; server uses all context sections.

### 9. “Explicit selection” today

Button sets `targetSectionIds` to **every** current context section id (starts fully selected). Unchecking converts to a subset. Zero selection → `[]`; workspace Next gate requires `null || length > 0`. UX is button-toggle, not radio + “Selected: N sections” copy from the master prompt.

### 10. Root cause — why unrelated Sections can appear

**Primary:** `AllocationCapacityPanel.load` fetches year+semester occupancy (all courses/groups), then:

```ts
const scoped = targetIds.size > 0 ? all.filter(...) : all;
```

When `context.sections` is empty (or context missing), **`all` is shown unfiltered** → Sections from other Groups/Courses appear in the capacity step UI.

**Secondary:** No `useEffect` clears `context` / `targetSectionIds` when Academic Scope changes while the administrator remains on later steps. Stale `context.sections` can remain until `loadContextBundle` runs (Step 0 Next / Refresh).

**Not primary for checkboxes:** Target checkboxes bind to `context.sections`, which is group-scoped when context is correctly loaded. Unrelated rows are most visible via the capacity occupancy fallback path and/or stale context.

### 11. Unrestricted catalog?

| Surface | Unrestricted? |
|---------|---------------|
| Target checkboxes | No (context-scoped) when context loaded |
| Occupancy fetch | **Yes** (year+semester), client-filtered; **unfiltered if filter set empty** |
| `listSections` in AcademicUiContext | Scoped; unused by Target checkboxes |

### 12. Scope change clears `targetSectionIds`?

Only inside `loadContextBundle` (`setTargetSectionIds(null)`). Changing Course/Group/Semester alone does **not** clear until Next/Refresh. **Gap for Prompt 4.**

### 13. Existing APIs sufficient?

**Yes for Target listing** — reuse Allocation Context (preferred) or scoped `GET /api/sections`.

**Optional additive (not required if UI fail-closes):** add `courseId`/`groupId` (or `sectionIds`) to occupancy endpoint. Prefer UI never displaying unfiltered occupancy first.

### 14. Combined SectionGroup representation

- Allocation Target UI selects individual `Section` rows from context.
- `SectionGroup` / TimetableSections are **not** used as Target selection units.
- Context may expose `sectionType` / timetable mapping status; no “Combined · A + B” target picker today.
- Preserve existing SectionGroup/Timetable contracts; do not invent CombinedSection entity.

### 15. N+1 / performance

- Client: parallel context/readiness/health/validation bundle (good).
- Builder: per-section readiness/health/policy loops (pre-existing; out of scope to redesign).
- Occupancy: single call then client filter (over-fetches year+semester).

## Existing authority (preserve)

| Concern | Authority |
|---------|-----------|
| Eligible Sections for allocation | Allocation Context builder |
| `targetSectionIds` acceptance | `AllocationScopeSelectionValidator` (10A) |
| Scoped apply | `AllocationContextScopeApplier` |
| Placement / scoring | Allocation Engine (unchanged) |
| Capacity calculation | Section Capacity Engine (unchanged) |
| Combined classes / attendance | SectionGroup + AttendanceSessionResolver (unchanged) |

React must **not** become eligibility authority (no hard-coded Group name filters).

## Recommended smallest change

1. **Prompt 2 (server/UI wiring):** Keep Allocation Context as sole Target Section source. Fail-closed capacity load: never show unfiltered year+semester occupancy when context section set is empty — use context capacities or error + Retry. Confirm 10A rejects cross-scope `targetSectionIds` (already). Add/extend tests for wrong Course/Group/Semester/Year/Tenant. **Prefer no new API / no DB.**
2. **Prompt 3:** Radio UX — All eligible vs Explicit selection; Selected: N; disable Continue when explicit && zero; no React eligibility rules.
3. **Prompt 4:** On Academic Year/Program/Course/Group/Semester change, clear `targetSectionIds` and reload/clear eligible list; fail-closed on load failure (drop previous list).
4. **Prompt 5:** Regression + browser validation.

Optional later: additive occupancy query params — only if capacity live rows remain incomplete after UI fix.

## APIs that can be reused

- `GET /api/allocation/context`
- Existing run/simulate `targetSectionIds` contract
- `AllocationScopeSelectionValidator` / Applier
- `GET /api/sections` (scoped) if needed as secondary
- Occupancy for capacity metrics only, filtered to context section IDs

## API changes required?

**No new endpoint required** for Target Section eligibility.  
**Optional additive** occupancy filters — document before implementing.

## Database changes required?

**None.**

## Security implications

- Server already rejects target IDs outside rebuilt Allocation Context (tenant-scoped).
- Capacity occupancy can return cross-group data within year/semester to authorized capacity managers — UI must not display that as Target eligibility.
- Empty `targetSectionIds` on server still means “all eligible” — UI must not send `[]` intending “none”; keep UI gate + document contract.

## Performance implications

- Avoid tenant-wide Section catalog + React filter.
- Avoid N+1 Section list calls; continue using context bundle.
- Clear/reload only when scope identity changes.

## Regression risks

| Area | Risk |
|------|------|
| Attendance / AttendanceSessionResolver | Must remain untouched |
| Allocation Engine algorithms | Must remain untouched |
| 10A populationSelection / targetSectionIds semantics | Preserve `null` = all |
| Capacity Engine | Do not break occupancy for other pages |
| SectionGroup / combined timetable | Preserve |
| Governance / approve | Out of scope |

## Discovery test plan

- Existing: `AI29_1D_Prompt10A_AllocationScopeTests` (target not in context rejected).
- New discovery probe test (read-only contract assertions) — see unit test artifact.

## Verdict for implementation

| Question | Answer |
|----------|--------|
| Root cause | Occupancy unfiltered fallback + missing scope-change clear of targets/context |
| Authority for eligible sections | Allocation Context (already scoped) |
| Smallest fix | UI fail-closed filter + scope-reset + Explicit UX; reuse 10A |
| New API | Not required |
| DB | None |

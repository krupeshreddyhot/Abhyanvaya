# AI-SCHED-TG.5 Prompt 1 — Teaching Group Management UX Architecture Discovery

**Workstream:** AI-SCHED-TG.5 — Teaching Group Management & Scheduling UX  
**Prompt:** 1 — Architecture Discovery & Existing UX Assessment  
**Date:** 2026-08-19  
**Type:** DISCOVERY ONLY  

**Production changes:** **None.** No database, API, domain, UI, Attendance, RBAC, or timetable behavior was modified.

**STATUS: PASS**

**Predecessor freeze:** AI-SCHED-TG.4A — **FULL PASS — FROZEN**  
(`docs/AI_SCHED_TG_4A_FINAL_ACCEPTANCE_AND_FREEZE.md`)

---

## Executive summary

| Finding | Status |
|---|---|
| Teaching Group **UI** (pages/routes/nav/clients) | **Does not exist** |
| Teaching Group **HTTP CRUD / membership / list-by-SA** | **Missing** |
| Teaching Group **assign/clear on TimetableEntry** | **Exists** (API only; UI unwired) |
| TeachingGroupSection **application boundary** | **Exists** (SoT); HTTP only via legacy `/sections` bridge |
| TimetableSectionProjector | **Exists**; sole TimetableSection writer (frozen) |
| Best UX home for TG management | Catalog → Scheduling → **new hub link** near Subject Allocation / Timetable Design |
| Timetable designer premature redesign | **Avoid**; additive TG assign only after TG management APIs exist |

---

## 1. Frozen TG.4A constraints (must not violate)

| Lock | Implication for TG.5 |
|---|---|
| `TeachingGroupSection` = section-membership SoT | UI must call application APIs that mutate SoT — never invent TimetableSection rows |
| `TimetableSection` = projection only | UI must not treat TimetableSection as editable SoT |
| `TimetableSectionProjector` = sole writer | No UI/API path that `new TimetableSection` outside projector |
| No automatic TeachingGroup creation | Create TG only via explicit admin command |
| No SubjectAllocation → TG inference | SA is parent scope; never “pick the only TG for this SA” silently |
| No UI direct DB access | React → HTTP → application services only |
| No UI direct TimetableSection mutation | Prefer TG section APIs; legacy `setTimetableSections` only as bridge after TG assigned |
| Legacy `/sections` contract preserved | Keep for compatibility; do not make it the primary TG management UX |

Approved flow:

```text
UI
 ↓
API / Application Service
 ↓
TeachingGroup / TeachingGroupSection
 ↓
TimetableSectionProjector
 ↓
TimetableSection
```

---

## 2. Existing Scheduling UI inventory

### 2.1 Navigation & hubs

| Layer | Path / file |
|---|---|
| Drawer | Catalog → `/setup` — `abhyanvaya-ui/src/layouts/MainLayout.tsx` |
| Setup hub | Scheduling card → `/setup/scheduling` — `pages/setup/SetupHub.tsx` |
| Scheduling hub | `pages/setup/scheduling/SchedulingHub.tsx` |
| Hub link SSOT | `pages/setup/scheduling/schedulingCatalogConfig.tsx` |
| Routes | `routes/AppRoutes.tsx` |

**No** Teaching Group nav item or route today.

### 2.2 Key routes for TG.5 adjacency

| Route | Page | Relevance |
|---|---|---|
| `/setup/scheduling/subject-allocations` | `SubjectAllocationPage` | Natural parent scope for listing/creating TGs |
| `/setup/scheduling/timetables` | `TimetableHubPage` | Timetable lifecycle |
| `/setup/scheduling/timetables/:id` | `TimetableDesignerPage` | Future **assign TG to entry** (additive; not redesign) |
| `/setup/sections` | `SectionsPage` | Academic Section CRUD (not TG) |
| `/setup/academic/allocation/*` | Allocation workspace | Student allocation — **do not conflate** with Teaching Groups |

### 2.3 Timetable designer / entry flows

| File | Role |
|---|---|
| `timetable/TimetableDesignerPage.tsx` | Grid workspace |
| `timetable/TimetableEntryDialog.tsx` | Create/edit entry |
| `timetable/TimetableGrid.tsx` | Cells / DnD |
| `services/schedulingService.ts` | Entry CRUD, move, copy, bulk |

**Entry UI payload today:** `dayOfWeek`, `timeSlotId`, `subjectAllocationId`, optional `roomId` / `remarks`.  
**No** `teachingGroupId`, section picker, or TG picker in the designer.

`listTimetableSections` / `setTimetableSections` exist in `sectionService.ts` but are **unused** by any page.

### 2.4 Subject Allocation UI

`SubjectAllocationPage.tsx` — MUI table + dialog CRUD; filters year/dept/course/group/semester/staff.  
Scoped to Course/Group/Semester; **no SectionId** on SA. Ideal **anchor** for “Teaching Groups for this allocation.”

### 2.5 Section selection patterns to reuse

| Component | Path | Reuse |
|---|---|---|
| `AcademicScopeSelector` | `components/academic/AcademicScopeSelector.tsx` | Scope filters for TG list/create |
| `AcademicOperationalPageShell` | `components/academic/` | Page chrome, alerts |
| `AcademicDataPanel` / `EmptyStateCard` | academic + common | Loading / empty |
| `AcademicConfirmDialog` | academic | Destructive confirms |
| Multi-section selection utilities | `utils/allocationTargetSectionSelection.ts` | Pattern for CombinedSections (adapt; do not bind to student allocation) |
| `FacultySectionAllocationPanel` | `components/sections/` | Faculty↔Section only — **not** TeachingGroup |

### 2.6 Design-system conventions

- Preferred shell for new Catalog pages: `AcademicOperationalPageShell` + `AcademicDataPanel` + shared error helpers (`getApiErrorMessage` / scheduling `errMsg`).
- Many AI30 pages still use raw MUI Stack/Alert/Dialog — acceptable to match Timetable Hub / Sections (academic kit) for TG management.
- Permissions: `PermissionAwareButton` / `PermissionDeniedAlert` where used.

### 2.7 Tenant / academic scope

- Tenant implicit via JWT/API.
- Scheduling pages use local Selects or `AcademicScopeSelector` — no separate tenant picker.
- TG create/filter should use Academic Year + Course/Group/Semester + SubjectAllocation (and Section multi-select for links).

---

## 3. Existing API / application boundaries

### 3.1 EXISTS (HTTP)

| Method | Route | Auth | Service |
|---|---|---|---|
| PUT | `api/scheduling/timetables/entries/{entryId}/teaching-group` | `CanManageSchedulingTimetable` | `ITeachingGroupApplicationService.AssignToTimetableEntryAsync` |
| DELETE | `api/scheduling/timetables/entries/{entryId}/teaching-group` | same | `ClearFromTimetableEntryAsync` |
| GET | `api/scheduling/legacy-teaching-group-conversion/entries-without-teaching-group` | same | Disposable conversion discovery |
| POST | `api/scheduling/legacy-teaching-group-conversion` | same | Disposable conversion (explicit TG; dry-run) |
| GET/PUT | `api/timetable/{timetableId}/sections` | View / Manage timetable | Legacy bridge → `ReplaceSectionsAndProjectAsync` |

### 3.2 EXISTS (application, little/no HTTP)

| Boundary | Capability |
|---|---|
| `ITeachingGroupSectionApplicationService` | Get / Replace / Add / Remove sections; `ReplaceSectionsAndProjectAsync` |
| `ITimetableSectionProjector` | Sole TimetableSection writer |
| Domain `TeachingGroup` | Type, status, capacity, membership collections, mutate helpers |
| Domain `TeachingGroupMembership` | Entity + EF — **no app/API** |

### 3.3 MISSING for TG management UX

| Capability | Gap |
|---|---|
| List TGs by SubjectAllocation | No API/service |
| Get TG detail | No API |
| Create / update / archive TG | No API/service (create must be explicit when added) |
| Dedicated HTTP for TeachingGroupSection | Only legacy `/sections` after TG assigned |
| Membership include/exclude | Entity only |
| `TeachingGroupDto` / create-update DTOs | Not shipped (sketched in TG.2 docs) |
| Dedicated TG RBAC keys | No `Scheduling.TeachingGroup.*`; uses Timetable policies today |
| UI client (`teachingGroupService.ts`) | Does not exist |

Reference (design-only, not implemented): `docs/AI_SCHED_TG_2_PROMPT_6_API_CONTRACT.md`.

---

## 4. RBAC today

| Policy | Used by |
|---|---|
| `CanViewSchedulingTimetable` | Timetable GET, sections GET |
| `CanManageSchedulingTimetable` | Timetable manage, TG assign/clear, conversion, sections PUT |

**Missing:** `CanView/ManageSchedulingTeachingGroup` and matching `PermissionKeys`.

**Recommendation for later prompts:** introduce TG-specific permissions **or** deliberately reuse Timetable manage for v1 — document the choice; do not weaken Faculty restrictions.

---

## 5. Reuse / extend / leave untouched

### Reuse

- Academic UI shell + empty/error/confirm patterns  
- `AcademicScopeSelector` for scope  
- Subject Allocation page as navigation parent / context picker  
- Backend: `ITeachingGroupApplicationService`, `ITeachingGroupSectionApplicationService`, projector, domain rules  
- Multi-section selection UX patterns (adapted)  
- Hub registration via `schedulingCatalogConfig.tsx` + `AppRoutes.tsx`

### Extend (future prompts — not this discovery)

- New TG management page(s) + API client  
- TG CRUD + list-by-SA application/API  
- Wire TeachingGroupSection HTTP to existing section application service  
- Additive TimetableEntryDialog field: select existing TG (assign API only)  
- Optionally surface `teachingGroupId` on UI `TimetableEntryDto`

### Leave untouched

- Frozen TG.4A projector / SoT / bridge semantics  
- Attendance resolver and Attendance schema  
- Student Allocation Workspace / SectionGroup as substitutes for TG  
- Premature full Timetable Designer redesign  
- Permanent timetable backfill / hosted conversion jobs  
- Direct UI writes to TimetableSection / TeachingGroupSection / TeachingGroupId

---

## 6. Recommended product placement

**Primary:** Catalog → Scheduling → new hub card **"Teaching Groups"**  
- Path suggestion: `/setup/scheduling/teaching-groups`  
- Entry context: filter/select **Subject Allocation**, then manage TGs for that SA  
- Section assignment via TG section APIs (to be added), which project TimetableSection through the frozen path  

**Secondary (later):** Timetable Designer — **additive** “Teaching Group” control on entry dialog calling existing assign/clear APIs only (after list/create exists so users can pick a TG).

**Do not** place TG management inside Sections master or student Allocation Workspace (different domain).

---

## 7. Implementation-readiness recommendation

| Track | Ready? | Notes |
|---|---|---|
| Domain / EF / SoT / projector | **Yes** | Frozen TG.4A |
| Assign TG to entry API | **Yes** | UI client missing |
| Section SoT application service | **Yes** | Needs dedicated HTTP for management UX |
| TG CRUD / list / membership APIs | **No** | Required before meaningful admin UI |
| TG RBAC | **Partial** | Timetable policies only |
| UI patterns / hub / scope | **Yes** | Reusable academic kit |
| Timetable designer TG UX | **Defer** | Avoid redesign; additive assign after CRUD |

**Recommended sequencing for TG.5 (after this discovery):**

1. Contract/API for TeachingGroup CRUD + list-by-SA + section replace (application boundaries first)  
2. UI Teaching Group Management page (hub + SA-scoped)  
3. Membership UX only if MembershipSource requires it  
4. Additive timetable entry TG assign  
5. Guards: UI never bypasses boundaries; no TimetableSection writes from UI  

**This prompt stops at discovery.** No production code changed.

---

## 8. Confirmation tests

`Abhyanvaya.Application.UnitTests/Scheduling/AiSchedTg5Prompt1UxArchitectureDiscoveryTests.cs` — source/docs inspection only.

---

**STATUS: PASS**

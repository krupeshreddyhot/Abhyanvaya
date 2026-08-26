# AI-SCHED-TG.6 Prompt 1 — Teaching Group Management & Scheduling UX Architecture Discovery

**Workstream:** AI-SCHED-TG.6  
**Prompt:** 1 — UX Architecture & Existing UI Integration Discovery  
**Date:** 2026-08-19  
**Type:** DISCOVERY ONLY — **no production code changes**  
**Status:** COMPLETE

---

## 1. Existing UI architecture

| Layer | Location | Notes |
|---|---|---|
| Router | `abhyanvaya-ui/src/routes/AppRoutes.tsx` | Nested under authenticated layout |
| Guard | `routes/ProtectedRoute.tsx` | `anyPermission` / JWT claims |
| Shell | `layouts/MainLayout.tsx` | Sidebar: **Catalog** → `/setup` |
| Catalog hub | `pages/setup/SetupHub.tsx` | Scheduling card → `/setup/scheduling` |
| Scheduling hub | `pages/setup/scheduling/SchedulingHub.tsx` + `schedulingCatalogConfig.tsx` | Card grid of modules |
| Design system | MUI throughout | Dialogs, Tables, Alerts, FormControls |
| Academic helpers | `components/academic/*` | Scope toolbar used heavily on timetable; TG page uses local SA select |
| HTTP | axios `api` client | Base URL already includes `/api` |

**Frozen backend chain (UI must consume, not recreate):**

```text
SubjectAllocation → TeachingGroup → Membership / TeachingGroupSection
  → TimetableEntry.TeachingGroupId (explicit)
  → TimetableSectionProjector → TimetableSection → Attendance
```

---

## 2. Recommended Teaching Group navigation

**Recommended home (already implemented in Prompt 3):**

```text
Catalog → Scheduling → Teaching Groups
Route: /setup/scheduling/teaching-groups
```

| Surface | Status |
|---|---|
| Scheduling catalog card `teaching-groups` | **Exists** — next to Subject Allocation |
| Dedicated sidebar item | Not required — follow catalog pattern |
| Parallel Scheduling app | **Do not introduce** |

**Risk:** `SetupHub` Scheduling card is gated by general `Scheduling.View`/`Manage`; TG-only users may reach Catalog via sidebar (TG perms in `catalogSetupPermissions`) but might not see the Scheduling card. Prefer ensuring TG View alone can open Scheduling hub or deep-link TG card — **defer to Prompt 2 UX polish**, do not invent a second nav tree.

---

## 3. Existing reusable components / conventions

| Concern | Convention | Primary files |
|---|---|---|
| Page pattern | Title + Alert messages + Table + Dialog | `TeachingGroupsPage.tsx`, `SubjectAllocationPage.tsx` |
| Confirm destructive | `AcademicConfirmDialog` | archive flows |
| Help | `ModuleHelpDrawer` + markdown docs | hub cards |
| Scope | Timetable uses `AcademicScopeToolbar`; TG uses **Subject Allocation Select** | keep SA-centric for TG (matches API `subjectAllocationId`) |
| Feedback | Inline MUI `Alert` (not toast library on TG/SA pages) | preserve |
| Forms | MUI `TextField` / `Select` / `Stack` | preserve |
| Tables | MUI `Table*` | preserve |
| Labels | `teachingGroupUi.ts` | status/type/source/capacity helpers |

**Do not introduce a second component library.**

---

## 4. Existing API clients & contracts

### 4.1 UI client today — `services/teachingGroupService.ts`

| Operation | Client method | Backend |
|---|---|---|
| List by SA | `listTeachingGroups` | `GET /scheduling/teaching-groups?subjectAllocationId=` |
| Detail | `getTeachingGroup` | `GET …/{id}` |
| Create / Update / Archive | yes | POST / PUT / POST `…/archive` |
| Memberships (raw) | `getTeachingGroupMemberships` | `GET …/{id}/memberships` |
| Sections | get / replace / add / remove | GET/PUT/POST/DELETE `…/sections` |

### 4.2 Backend available but **not yet in UI client** (TG.5 Prompt 5+)

| Operation | Backend | UI gap |
|---|---|---|
| Resolved roster | `GET …/{id}/resolved-members` | No client method / no UI |
| Add members | `POST …/{id}/memberships` | No client |
| Replace overlays | `PUT …/{id}/memberships` | No client |
| Remove member | `DELETE …/{id}/memberships/{studentId}` | No client |

### 4.3 Timetable Teaching Group assignment (TG.4 API — UI gap)

| Operation | Backend (`TimetableControllers`) | UI |
|---|---|---|
| Assign | `PUT /scheduling/timetables/entries/{entryId}/teaching-group` | **Missing** in designer |
| Clear | `DELETE …/entries/{entryId}/teaching-group` | **Missing** |
| Entry DTO `teachingGroupId` | Present on API/domain | **Not** in `TimetableEntryDialog` / `TimetableEntryDto` UI types |

### 4.4 Legacy /sections

`sectionService.setTimetableSections` exists for legacy bridge; **Teaching Groups page must not call it** (Prompt 3 guard). Section UX stays on TeachingGroupSection endpoints only.

### 4.5 Subject Allocation

`schedulingService.ts` — list/CRUD `/scheduling/subject-allocations` — TG page already uses list for scope selector.

---

## 5. Existing RBAC patterns

| Key | Constant | Usage |
|---|---|---|
| `Scheduling.TeachingGroup.View` | `PermissionKeys.SchedulingTeachingGroupView` | Route + page `canView` |
| `Scheduling.TeachingGroup.Manage` | `PermissionKeys.SchedulingTeachingGroupManage` | Create/edit/archive/sections |
| Timetable | `CanViewSchedulingTimetable` / `CanManageSchedulingTimetable` (existing scheduling keys) | Designer / entry mutations |

Pattern: JWT claims via `AuthContext.hasPermission` / `hasAnyPermission`; server remains authoritative.

**Do not weaken.** Do not expose cross-tenant data in errors.

---

## 6. Teaching Group user journeys (target UX)

| # | Journey | Current UI | Next UX work |
|---|---|---|---|
| A | View TGs for Subject Allocation | **Done** — SA select + table | Polish empty/loading |
| B | Create TG | **Done** — Dialog | Keep; no auto-create |
| C | Edit TG | **Done** — inline detail | Lifecycle-aware disable when Locked/Archived |
| D | Archive TG | **Done** — confirm dialog | Keep |
| E | View resolved membership | Partial — raw memberships only | Add `resolved-members` + provenance |
| F | Manage membership | Banner: “not yet available” | Explicit/Hybrid editors + 409 handling |
| G | View TG sections | **Done** | Keep |
| H | Add/remove TG sections | **Done** — via TG section APIs | Keep; never TimetableSection writes |
| I | Assign TG to timetable entry | **Missing** | Additive selector in `TimetableEntryDialog` |
| J | Clear TG from entry | **Missing** | Explicit clear control → DELETE API |
| K | Incompatible TG assignment | API rejects (domain message) | Surface BadRequest in dialog; no silent clear |
| L | Capacity info | Shows Expected / Max / Resolved | Display-only; server validates Max |
| M | Membership resolution state | Overlays only | Show Resolved vs Overlay tabs/panels |

---

## 7. Membership UX (consume TG.5 contract)

**Sources (UI labels only; server is authority):**

| Source | Mutation UI | Display |
|---|---|---|
| ExplicitStudents | Include set Add/Replace/Remove | Resolved = Includes |
| Hybrid | Include + Exclude overlays | Resolved = (Base ∪ Includes) − Excludes |
| Section / Combined / StudentSubject | **No mutation UI** | Resolved = derived; explain “change sections / academic enrollment” |

**Panels:**

1. **Overlays** — raw Include/Exclude rows (`GET memberships`)  
2. **Resolved roster** — `GET resolved-members` (StudentId + Provenance)  

**409 concurrency:**

```text
HTTP 409 → show Alert (“conflicting membership change…”)
  → reload memberships + resolved-members + detail
  → allow retry
```

Do not invent new semantics. Do not compute resolved set in the browser as authority.

---

## 8. Section UX

```text
UI section actions
  → Teaching Group section endpoints
  → ITeachingGroupSectionApplicationService
  → TimetableSectionProjector
  → TimetableSection
```

**UI must never:** call `setTimetableSections` from TG management; write Attendance; invent TimetableSection rows.

---

## 9. Timetable UX (minimum additive change)

| Item | Recommendation |
|---|---|
| Where | `TimetableEntryDialog.tsx` — after Subject Allocation / Faculty / Room block |
| Enabled when | Entry has SubjectAllocation; user has Manage Timetable; TG list for that SA loads |
| Disabled when | No SA; published/locked entry rules per existing designer; View-only |
| Options | TGs for entry’s SubjectAllocation (API list); show status/capacity summary |
| Incompatible | Server returns domain error (TG.4 message); show in Alert; keep prior TG until user clears/reassigns |
| Clear | Explicit “Clear Teaching Group” → DELETE teaching-group endpoint (not silent null on unrelated update) |
| Preserve | Existing entry field contract; do not redesign grid |

Extend UI DTOs with optional `teachingGroupId` / display name when API already returns them.

---

## 10. Capacity UX

| Field | UI treatment |
|---|---|
| ExpectedStudentCount | Editable planning intent (Manage) |
| MaxTeachingCapacity | Editable ceiling (Manage); null = unlimited |
| ResolvedStudentCount | **Display only** — from server detail/resolver |
| Room.Capacity | Timetable/room context only — not TG membership validation |

Warn (non-blocking display) when Resolved > Max if API still allows read; mutations that exceed Max fail with 400 — show server message.

---

## 11. Error / concurrency UX

| HTTP | UI |
|---|---|
| 400 DomainException | Alert with message |
| 404 | Not found / invisible tenant → generic not found |
| 403 | Permission Alert |
| 409 Conflict | Membership race — reload + retry (safe message only) |
| Network | Existing Alert pattern |

Never show SQL, constraint names, or other-tenant details.

---

## 12. Responsive UX

| Breakpoint | Approach |
|---|---|
| Desktop | Table + side detail (current TG page pattern) |
| Tablet | Stack detail below table; Dialogs full-width |
| Mobile | Single-column; prefer full-screen Dialog; hide dense columns behind expand |

Reuse MUI breakpoints already used on scheduling pages; no new layout framework.

---

## 13. Accessibility considerations

- Dialogs: focus trap (MUI default), labelled controls  
- Tables: header scope; archive confirms keyboard reachable  
- Status chips/labels: text, not color-only  
- Errors: associated with Alert `role="alert"` (MUI Alert)  
- Timetable TG select: clearable with explicit control (not icon-only)

---

## 14. Exact implementation sequence (recommended)

1. **Prompt 2 — Client contract completion**  
   Add `resolved-members` + membership mutation methods to `teachingGroupService.ts`; extend timetable entry types + assign/clear clients.  
2. **Prompt 3 — Membership UX**  
   Replace read-only banner with Explicit/Hybrid editors; 409 reload.  
3. **Prompt 4 — Timetable designer TG selector**  
   Additive field in `TimetableEntryDialog`; incompatible/clear flows.  
4. **Prompt 5 — Capacity / status polish + a11y / responsive pass**  
5. **Prompt 6 — Guards + acceptance**  
   Extend Prompt 3 architecture tests; no TimetableSection writes from TG UI.

---

## 15. Items explicitly NOT required (this discovery / early UX)

- Redesign of timetable grid  
- New Scheduling SPA  
- Auto-create TG from Subject Allocation  
- SA → TG inference UI  
- Direct TimetableSection editing from TG pages  
- Attendance UI changes  
- New component library  
- Schema / API redesign (consume existing)  
- Weakening RBAC  

---

## 16. Domain representation cheat-sheet (display only)

| Concept | UI representation | Authority |
|---|---|---|
| Draft / Active / Locked / Archived | Status label (`teachingGroupUi`) | Server status |
| Explicit / Include / Exclude | Overlay table Inclusion | Membership API |
| Hybrid | Overlay + Resolved tabs | Resolver API |
| Resolved roster | Ordered StudentIds + Provenance | `resolved-members` |
| Capacity warning | Resolved vs Max display | Server mutation gates |
| Room capacity | Timetable/room warnings if existing soft-warn panels | Not membership |
| Archived TG | Disable edit/membership/sections; hide from assignable list or mark disabled | Server `EnsureCanMutate` / attach rules |

---

## Appendix A — Key file index

```text
abhyanvaya-ui/src/routes/AppRoutes.tsx
abhyanvaya-ui/src/layouts/MainLayout.tsx
abhyanvaya-ui/src/pages/setup/SetupHub.tsx
abhyanvaya-ui/src/pages/setup/scheduling/SchedulingHub.tsx
abhyanvaya-ui/src/pages/setup/scheduling/schedulingCatalogConfig.tsx
abhyanvaya-ui/src/pages/setup/scheduling/TeachingGroupsPage.tsx
abhyanvaya-ui/src/pages/setup/scheduling/teachingGroupUi.ts
abhyanvaya-ui/src/pages/setup/scheduling/SubjectAllocationPage.tsx
abhyanvaya-ui/src/pages/setup/scheduling/timetable/TimetableDesignerPage.tsx
abhyanvaya-ui/src/pages/setup/scheduling/timetable/TimetableEntryDialog.tsx
abhyanvaya-ui/src/services/teachingGroupService.ts
abhyanvaya-ui/src/services/schedulingService.ts
abhyanvaya-ui/src/auth/permissionKeys.ts
Abhyanvaya.API/Controllers/Scheduling/TeachingGroupsController.cs
Abhyanvaya.API/Controllers/Scheduling/TimetableControllers.cs  (entries/.../teaching-group)
```

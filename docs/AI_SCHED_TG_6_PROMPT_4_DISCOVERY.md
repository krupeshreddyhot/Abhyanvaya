# AI-SCHED-TG.6 Prompt 4 — Discovery (Prompt 1)

**Workstream:** AI-SCHED-TG.6 Prompt 4  
**Sub-prompt:** 1 — Architecture Discovery & Existing Timetable Entry UX  
**Date:** 2026-08-19  
**Type:** DISCOVERY ONLY — **no production behavior changes**  
**Status:** COMPLETE

---

## 1. Existing timetable UI architecture

```text
Catalog → Scheduling → Timetable Design → Timetable Designer (hub)
  → /setup/scheduling/timetables          TimetableHubPage
  → /setup/scheduling/timetables/:id      TimetableDesignerPage  ← primary edit surface

Supporting read-only / projection views (out of Prompt 4 scope for selector):
  → timetable-faculty | timetable-student | timetable-room | timetable-dashboard
```

| Layer | Location | Notes |
|---|---|---|
| Routes | `abhyanvaya-ui/src/routes/AppRoutes.tsx` | `ProtectedRoute` with Timetable View/Manage |
| Hub | `timetable/TimetableHubPage.tsx` | List/create/lock; navigates to designer |
| Designer | `timetable/TimetableDesignerPage.tsx` | Grid, DnD, clipboard, lifecycle, entry dialog host |
| Entry editor | `timetable/TimetableEntryDialog.tsx` | Create / edit / delete / duplicate / clone |
| Grid display | `timetable/TimetableGrid.tsx` | Cell chips via `formatEntryCompact` |
| Selection helpers | `timetableSelection.ts`, `timetableUtils.ts` | Cell keys, compact labels |
| Undo/redo | `useTimetableHistory.ts` | Local history around create/move/delete |
| HTTP | `services/schedulingService.ts` | Single axios client; TG assign/clear already present (Prompt 2) |
| Errors | `schedulingFormUtils.errMsg` | String body preferred; no React Query |

**Frozen chain (UI must not reinvent):**

```text
SubjectAllocation → TeachingGroup (explicit)
  → TimetableEntry.TeachingGroupId (nullable, dedicated assign/clear)
  → TeachingGroupSection (SoT) → TimetableSectionProjector → TimetableSection
```

---

## 2. Relevant files / components

| Concern | File |
|---|---|
| Designer route/page | `TimetableDesignerPage.tsx` |
| Entry dialog | `TimetableEntryDialog.tsx` |
| Grid / chips | `TimetableGrid.tsx`, `timetableUtils.formatEntryCompact` |
| Hub | `TimetableHubPage.tsx` |
| Catalog card | `schedulingCatalogConfig.tsx` (`timetable-designer`) |
| Client DTOs / APIs | `schedulingService.ts` (`TimetableEntryDto`, create/update/move/copy/bulk, assign/clear TG) |
| Teaching Group list client | `teachingGroupService.listTeachingGroups(subjectAllocationId)` |
| Permissions | `permissionKeys.ts` — `Scheduling.Timetable.View` / `.Manage` |

**Confirmed gaps (UI behavior, not client):**

- `TimetableEntryDialog` does **not** reference `teachingGroupId`, `assignTeachingGroupToTimetableEntry`, or `clearTeachingGroupFromTimetableEntry`
- `TimetableGrid` / `formatEntryCompact` do **not** show Teaching Group
- Designer DnD create / paste / undo recreate **omit** TG (correct — Create/Update DTOs exclude `teachingGroupId`)

---

## 3. Existing DTO / service flow

### 3.1 Response DTO (Prompt 2 already extended)

```ts
TimetableEntryDto {
  ...
  subjectAllocationId: number
  teachingGroupId?: number | null   // optional; null/undefined = unassigned
  staffId, roomId, course/group/semester/subject names, remarks, ...
}
```

### 3.2 Ordinary write DTOs (must remain TG-free)

| DTO | Contains `teachingGroupId`? |
|---|---|
| `CreateTimetableEntryRequest` | **No** |
| `UpdateTimetableEntryRequest` | **No** |
| `UpsertTimetableEntryRequest` / bulk paste | **No** |
| `MoveTimetableEntryRequest` | **No** |
| `CopyTimetableEntryRequest` | **No** |

### 3.3 Dedicated TG assignment (Prompt 2 client — unused by UI)

| Client | HTTP |
|---|---|
| `assignTeachingGroupToTimetableEntry(entryId, { teachingGroupId })` | `PUT /scheduling/timetables/entries/{entryId}/teaching-group` |
| `clearTeachingGroupFromTimetableEntry(entryId)` | `DELETE …/entries/{entryId}/teaching-group` |

(API base path includes `/api` on axios; full path is `/api/scheduling/timetables/...`.)

### 3.4 Entry lifecycle in UI

| Flow | Where | Service | Refresh |
|---|---|---|---|
| Create (dialog) | `TimetableEntryDialog.handleSave` | `createTimetableEntry` | `onSaved` → designer `upsertEntryLocal` |
| Update (dialog) | same | `updateTimetableEntry` | `upsertEntryLocal` |
| Delete (dialog) | `handleDelete` | `deleteTimetableEntry` | `onDeleted` → `removeEntryLocal` |
| Duplicate | `handleDuplicate` | `duplicateTimetableEntry` | `onSaved` (dialog stays open) |
| Clone to day/slot | `handleClone` | `copyTimetableEntry` | `onSaved` + close |
| DnD allocation → cell | Designer `createEntryWithHistory` | `createTimetableEntry` | local + soft warnings |
| DnD entry move | `moveEntryWithHistory` | `moveTimetableEntry` | local + soft warnings |
| Paste selection | `handlePaste` | `bulkTimetableEntries` | local upserts |
| Duplicate day | `handleDuplicateDay` | `copyTimetableEntry` loop | local upserts |
| Full reload | `refreshGrid` | `getTimetableGrid` | replaces `entries` |

**Undo recreate** after delete rebuilds via `createTimetableEntry` **without** re-assigning TeachingGroup — edge case for Prompt 4 implementation (see Risks).

---

## 4. Recommended insertion point for Teaching Group selector

**Primary (minimum disruption):** `TimetableEntryDialog.tsx`

Place an additive **Teaching Group** control **after Subject allocation** (and before Faculty/Room), because:

1. Options must be scoped to the selected `subjectAllocationId` (`listTeachingGroups(saId)`).
2. Ordinary Save must continue to call Create/Update **without** `teachingGroupId`.
3. Assign/clear must run only via dedicated endpoints **after** an entry id exists.

**Recommended interaction model (for later implementation prompts — not this discovery):**

```text
Create flow:
  Save CreateTimetableEntryRequest
    → if user selected a TG and entry.id exists
      → PUT …/teaching-group
    → onSaved(updatedEntry)

Edit flow:
  Save UpdateTimetableEntryRequest (no teachingGroupId)
    → then reconcile TG:
         selected TG ≠ current → PUT assign
         cleared → DELETE clear
         unchanged → no TG call
  → onSaved with authoritative entry from last TG call or update response

Display (optional, low priority):
  formatEntryCompact / Chip secondary: show TG code/name when teachingGroupId set
  (requires name lookup map; API may only return id)
```

**Do not** put TG on:

- Room-prompt dialog (DnD schedule)
- Bulk paste payloads
- Move/copy request bodies
- A new timetable page or workflow

**Designer-level alternative:** separate “Assign TG” context-menu item — useful later, but dialog is the lowest-friction additive field aligned with Subject Allocation.

---

## 5. Existing SubjectAllocation dependency

| UI control | Role |
|---|---|
| Department / Course / Group / Semester | **Filters** for Subject Allocation list (not persisted as entry fields independently) |
| Subject allocation (required) | Persisted as `subjectAllocationId`; drives staff/room defaults |
| Subject | **Not** a separate selector — subject name comes from allocation + subject catalog map |
| Faculty / Room | Shown; room required on save; staff display/default from allocation |

Teaching Group list **must** filter by the selected Subject Allocation. Changing SA must clear or revalidate TG selection (no SA→TG inference; never auto-pick a TG).

---

## 6. Existing RBAC pattern

| Key | Constant | Usage |
|---|---|---|
| `Scheduling.Timetable.View` | `SchedulingTimetableView` | Route + view designer |
| `Scheduling.Timetable.Manage` | `SchedulingTimetableManage` | `canManage`; Draft + not frozen → editable |
| Publish / Archive | separate keys | Lifecycle buttons |

Designer: `readOnly = status !== Draft || !canManage || isFrozen`.

Backend TG assign/clear uses timetable manage policy (server authoritative). Future UI may hide TG controls when `readOnly`; must not invent TeachingGroup Manage as a substitute for Timetable Manage unless product later requires both.

---

## 7. Existing error / refresh pattern

- Errors: local `Alert` + `errMsg(e)` (prefer string response body).
- No global React Query cache; designer keeps `entries` in React state.
- Dialog save: optimistic local upsert via `onSaved(res.data)` — **does not** always call `refreshGrid`.
- Soft warnings refreshed after many mutations (`refreshSoftWarnings`).
- Hub uses `AcademicConfirmDialog` for destructive confirmations; entry dialog delete currently has **no** confirm dialog.

**Implication for TG assign:** after assign/clear, prefer returning/upserting the `TimetableEntryDto` from the dedicated endpoint response so `teachingGroupId` stays accurate without inventing local state.

---

## 8. Risks and edge cases

| Risk | Notes |
|---|---|
| Create then assign | Entry must exist before PUT assign; two-step save; failure after create leaves entry without TG — show error, keep dialog/state honest |
| Ordinary update must not clear TG | Never send `teachingGroupId: null` on Update; only DELETE clear |
| DnD / paste / undo recreate | New entries start with `teachingGroupId` null; undo after delete loses TG unless re-assign is added later |
| Copy / duplicate | Server decides whether TG is copied; UI must display response `teachingGroupId`, not assume |
| Multiple TGs per SA | Selector must list all TGs for SA; never invent one |
| Empty TG list | Allow “None” / unassigned; do not auto-create TG |
| Incompatible TG | Surface 400 message; do not force overwrite |
| Legacy null TG | Valid; chips/dialog must tolerate null/undefined |
| TimetableSection | UI must never call TimetableSection writers |
| Membership | UI must not resolve membership for scheduling |

---

## 9. Explicit confirmation — no production behavior changed

This Prompt 4 / Prompt 1 deliverable:

- [x] Did **not** modify `TimetableEntryDialog`, `TimetableDesignerPage`, `TimetableGrid`, or other timetable production components
- [x] Did **not** modify API controllers, Application, Domain, EF, or migrations
- [x] Did **not** modify Attendance, StudentSection, TimetableSection, TeachingGroupSection
- [x] Did **not** add a Teaching Group selector
- [x] Did **not** add `teachingGroupId` to Create/Update/Upsert request payloads
- [x] Added documentation + discovery architecture guards only

---

## Appendix A — Discovery checklist (A–M)

| Item | Finding |
|---|---|
| A. Designer route/page | `/setup/scheduling/timetables/:id` → `TimetableDesignerPage` |
| B. Entry dialog | `TimetableEntryDialog.tsx` |
| C. SubjectAllocation selection | Required Select in dialog; DnD palette on designer |
| D. Subject selection | Via allocation + catalog map (no standalone subject control) |
| E. Course / Group / Semester | Filter Selects in dialog |
| F. TimetableEntryDto | Includes optional `teachingGroupId` (Prompt 2) |
| G. schedulingService methods | Full entry CRUD + move/copy/dup/bulk + TG assign/clear |
| H. Create/update/upsert | Dialog create/update; designer DnD create; bulk paste upsert |
| I. Copy/duplicate/move | Dialog clone/dup; designer move DnD + day duplicate |
| J. Display | `TimetableGrid` chips via `formatEntryCompact` |
| K. Loading/error/confirm | CircularProgress on designer load; Alert + errMsg; confirm sparse in dialog |
| L. RBAC | Timetable View/Manage; readOnly when not Draft/manage/frozen |
| M. Refresh strategy | Local upsert + optional `refreshGrid` / soft warnings; no RQ invalidation |

---

## Appendix B — Recommended next implementation sequence (not started)

1. Additive TG Select in `TimetableEntryDialog` (scoped to SA; None allowed)
2. Post-create / post-update assign/clear via Prompt 2 clients
3. Optional compact chip label for assigned TG
4. Tests: no TG on Create/Update payload; assign/clear URLs; null TG; DnD create remains TG-null
5. Guards: no TimetableSection writes; no SA→TG inference; no auto TG create

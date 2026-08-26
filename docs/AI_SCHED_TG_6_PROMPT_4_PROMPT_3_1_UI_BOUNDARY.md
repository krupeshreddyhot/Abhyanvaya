# AI-SCHED-TG.6 Prompt 4 / Prompt 3.1 — UI Boundary Verification

**Workstream:** AI-SCHED-TG.6 Prompt 4 / Prompt 3  
**Sub-prompt:** 3.1 — Existing UI Boundary Verification  
**Date:** 2026-08-20  
**Type:** Discovery (boundary confirmed; implementation proceeds in Prompt 3 UX)

---

## 1. Existing dialog entry point

| Item | Location |
|---|---|
| Designer | `TimetableDesignerPage.tsx` — `/setup/scheduling/timetables/:id` |
| Dialog | `TimetableEntryDialog.tsx` |
| Open paths | Entry click / context “New entry” / edit |
| Host props | `entry`, `initial`, `readOnly`, `onSaved`, `onDeleted` |

**Decision:** Selector belongs **inside** `TimetableEntryDialog`, immediately **after Subject Allocation**. No new dialog or parallel TG scheduling page.

---

## 2. Existing timetable-entry state model

Designer holds `entries: TimetableEntryDto[]` + `editingEntry` / `entryInitial`.  
Dialog owns local form fields (department/course/group/semester filters, allocation, staff, room, day, slot, remarks).  
Refresh: `upsertEntryLocal` / `removeEntryLocal` / occasional `getTimetableGrid` — **no React Query**.

---

## 3. Existing entry DTO

`TimetableEntryDto` includes optional `teachingGroupId?: number | null` (response only).  
Create/Update/Upsert requests **omit** `teachingGroupId` (frozen).

---

## 4. Existing save lifecycle

| Action | Client |
|---|---|
| Create | `createTimetableEntry` → `onSaved` → close |
| Update | `updateTimetableEntry` → `onSaved` → close |
| Delete / Duplicate / Clone | dedicated endpoints |

**TG addition:** after create/update of fields, only if selection **changed**, call assign PUT or clear DELETE. New entries: create first (no TG in payload), then enable selector / optional assign.

---

## 5. Existing permissions

Designer: `readOnly = status !== Draft \|\| !canManage \|\| frozen`  
`canManage` = `Scheduling.Timetable.Manage`  
Dialog receives `readOnly` — TG assign/clear disabled when `readOnly`.

---

## 6. Existing refresh mechanism

`onSaved(entry)` → designer `upsertEntryLocal`. Soft warnings refreshed separately. No global cache invalidation.

---

## 7. Exact insertion point

After **Subject allocation** `FormControl`, before **Faculty**.

---

## 8. Components / helpers reused

| Asset | Use |
|---|---|
| `listCompatibleTeachingGroupsForTimetableEntry` | Options (server-authoritative) |
| `assignTeachingGroupToTimetableEntry` / `clearTeachingGroupFromTimetableEntry` | Mutations |
| `teachingGroupUi.ts` | Type/status/capacity labels |
| `getApiErrorMessage` / `getHttpStatus` | Errors + 409 |
| MUI `Select` / `Alert` / existing dialog patterns | UI |
| `timetableTeachingGroupSelectorContract.ts` | Architecture guards |

**Out of scope for grid:** optional TG chip deferred (Prompt 13).

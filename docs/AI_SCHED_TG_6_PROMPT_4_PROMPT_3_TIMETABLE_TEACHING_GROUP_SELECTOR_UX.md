# AI-SCHED-TG.6 Prompt 4 / Prompt 3 — Timetable Teaching Group Selector UX

**Workstream:** AI-SCHED-TG.6 Prompt 4  
**Sub-prompt:** 3 — TimetableEntryDialog Teaching Group Selector & Assignment UX  
**Date:** 2026-08-20  
**Type:** UI integration only  
**Status:** PASS

---

## 1. Architecture decision

Teaching Group assignment remains an **explicit secondary mutation** of `TimetableEntry`:

- Ordinary Create/Update/Upsert **never** carry `teachingGroupId`
- Assign/clear use dedicated endpoints only
- Compatible options come **only** from the server query (Prompt 2A)
- No SA→TG inference, auto-create, client compatibility filter, or TimetableSection writes

---

## 2. UI flow

```text
TimetableEntryDialog
      ↓
GET compatible-teaching-groups (after entry id exists)
      ↓
User selection (No TG / option)
      ↓
Save: update/create fields (no TG in payload)
      ↓
If selection ≠ baseline → PUT assign or DELETE clear
      ↓
Reload compatible options + upsert local designer entry
```

**New entry:** create without TG → stay open → load compatible list → user may select and save again.

---

## 3. API boundaries

| Operation | Client | HTTP |
|---|---|---|
| Compatible options | `listCompatibleTeachingGroupsForTimetableEntry` | `GET …/entries/{id}/compatible-teaching-groups` |
| Assign | `assignTeachingGroupToTimetableEntry` | `PUT …/entries/{id}/teaching-group` |
| Clear | `clearTeachingGroupFromTimetableEntry` | `DELETE …/entries/{id}/teaching-group` |

---

## 4. Explicit non-goals

This prompt does **not**:

- Redesign timetable UX / grid TG chips (deferred)
- Change database schema or backend production APIs
- Change Attendance / TimetableSection / TeachingGroup domain
- Add TG to Create/Update/Upsert
- Implement client-side compatibility
- Auto-create or infer TGs
- Modify SubjectAllocation

---

## 5. Key UX behaviors

| Case | Behavior |
|---|---|
| Archived assigned TG | Shown with warning; not auto-cleared |
| Empty compatible list | Informational Alert |
| Capacity exceeded | Warning only (selection not blocked by UI) |
| Room capacity | Not used for TG compatibility |
| 409 | Message + reload options/assignment; no auto-retry |
| View-only / frozen / non-draft | Selector disabled via `readOnly` |

---

## 6. Files

| File | Role |
|---|---|
| `TimetableEntryDialog.tsx` | Selector after Subject Allocation |
| `timetableTeachingGroupAssignmentActions.ts` | Assign/clear + 409 reload |
| `teachingGroupUi.ts` | Option label + capacity warning helpers |
| Tests / guards | Prompt 3 UX + updated Prompt 2/4 discovery guards |
| Boundary doc | `docs/AI_SCHED_TG_6_PROMPT_4_PROMPT_3_1_UI_BOUNDARY.md` |

---

## 7. Verification

| Check | Result |
|---|---|
| `tsc -b` | **PASS** |
| Prompt 3 + TG contract/discovery/UI unit tests | **PASS** |
| `npm run build` | **PASS** (recorded after run) |
| Backend / schema | **unchanged** |

---

## Acceptance checklist

- [x] Selector in TimetableEntryDialog after Subject Allocation  
- [x] Options from backend only  
- [x] Current / archived assignment displayed  
- [x] Assign PUT / Clear DELETE  
- [x] TG absent from Create/Update/Upsert  
- [x] No client compatibility / SA inference / auto-create / auto-clear  
- [x] 409 reload  
- [x] View/Manage via `readOnly`  
- [x] Capacity warning; room capacity not used  
- [x] TimetableSection / Attendance / schema untouched  
- [x] Accessibility labels / loading / empty / warning states  

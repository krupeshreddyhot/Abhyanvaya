# AI-SCHED-TG.6 Prompt 4 — Prompt 2: Teaching Group Selector Contract

**Workstream:** AI-SCHED-TG.6 Prompt 4  
**Sub-prompt:** 2 — Compatible Teaching Group Query & UI Contract  
**Date:** 2026-08-19  
**Type:** CONTRACT DESIGN + minimal frontend client preparation  
**Status:** PASS (frontend contract) — backend query endpoint **deferred dependency**

---

## 1. Purpose

Define the UI contract for obtaining Teaching Groups **compatible** with a `TimetableEntry`, without redesigning the timetable UI and without client-side compatibility filtering.

---

## 2. Architectural rules (frozen)

| Rule | Requirement |
|---|---|
| Server authority | Compatibility is decided only by the server |
| No local filter | UI must **never** load all Teaching Groups and filter locally |
| No inference | Never infer TG from SA uniqueness, Section, StudentSection, Subject, Course/Group alone, or timetable history |
| Multiplicity | One SubjectAllocation may have many TGs → API returns **0, 1, or many** |
| Assignment | Only `PUT …/entries/{entryId}/teaching-group` |
| Clear | Only `DELETE …/entries/{entryId}/teaching-group` |
| Create/Update/Upsert | Must **not** accept `teachingGroupId` |
| Isolation | No TimetableSection writes, Attendance, membership mutation, or TG creation from the selector |

---

## 3. Compatibility basis (existing domain — do not invent)

Authoritative check already used on assign:

`TeachingGroupRules.EnsureCompatibleWithTimetableEntry`

Includes (non-exhaustive of message text; exact rules live in domain):

- Same tenant
- TeachingGroup may attach to timetable entries (lifecycle/status rules via `EnsureCanAttachToTimetableEntry`)
- Matching `SubjectAllocationId`
- Matching academic scope already on the entry/TG model: `CourseId`, `GroupId`, `SemesterId`, `SubjectId`

College is **not** part of this TG↔entry contract. Do not add extra academic dimensions in the UI.

---

## 4. Approved query endpoint

**Chosen boundary** (aligned with existing Timetable controllers + assign/clear):

```http
GET /api/scheduling/timetables/entries/{entryId}/compatible-teaching-groups
```

UI axios path (base already includes `/api`):

```text
GET /scheduling/timetables/entries/{entryId}/compatible-teaching-groups
```

**Why this location:** same resource family as:

- `PUT /scheduling/timetables/entries/{entryId}/teaching-group`
- `DELETE /scheduling/timetables/entries/{entryId}/teaching-group`

**Not approved for selector population:**

- `GET /scheduling/teaching-groups?subjectAllocationId=` alone + client filter
- Legacy conversion endpoints
- Any TimetableSection / Attendance / StudentSection query

### Backend status

As of this prompt, the **GET compatible-teaching-groups** endpoint is **not yet implemented** in the API.

This prompt delivers the **frontend contract** only. Backend implementation is a **deferred dependency** for the UX wiring prompt. Do not substitute client-side filtering while waiting.

---

## 5. Response DTO (selector-shaped)

Frontend type: `CompatibleTeachingGroupOptionDto`

| Field | Notes |
|---|---|
| `id` | TeachingGroupId |
| `code` | Optional display |
| `name` | Display name |
| `type` | TeachingGroupType (byte) |
| `status` | TeachingGroupStatus (byte) |
| `resolvedStudentCount` | Server-derived |
| `expectedStudentCount` | Planning intent (optional) |
| `maxTeachingCapacity` | Teaching ceiling (optional; **not** room capacity) |
| `isAssignedToEntry` | True when option equals entry’s current `teachingGroupId` |

**Omit:** membership overlays, resolved member lists, section links, internal exclusion keys unless already needed for display (exclusion key **not** required for selector).

Empty array `[]` is a valid response (no compatible groups).

---

## 6. Current assignment

Use existing response field only:

```ts
TimetableEntryDto.teachingGroupId?: number | null
```

- `null` / `undefined` → unassigned (legacy-valid)
- Non-null → current assignment; should align with `isAssignedToEntry` on the matching option when the query returns

Do **not** modify Create/Update/Upsert request DTOs.

---

## 7. Assignment / clear contract (already in Prompt 2 client)

| Action | Client | HTTP |
|---|---|---|
| Assign | `assignTeachingGroupToTimetableEntry` | `PUT …/entries/{entryId}/teaching-group` body `{ teachingGroupId }` |
| Clear | `clearTeachingGroupFromTimetableEntry` | `DELETE …/entries/{entryId}/teaching-group` |
| List compatible | `listCompatibleTeachingGroupsForTimetableEntry` (**new**) | `GET …/entries/{entryId}/compatible-teaching-groups` |

Do not bypass these endpoints. Do not silently assign or clear.

---

## 8. Frontend files changed

| File | Change |
|---|---|
| `schedulingService.ts` | `CompatibleTeachingGroupOptionDto` + `listCompatibleTeachingGroupsForTimetableEntry` |
| `timetableTeachingGroupSelectorContract.ts` | Path helpers + architecture guard helpers |
| `AiSchedTg6Prompt4SelectorContract.test.ts` | Contract + architecture tests |
| This document | Contract specification |

**Unchanged:** `TimetableEntryDialog`, designer UI, backend, schema, Attendance, sections.

---

## 9. Tests / architecture guards

Verified:

1. Selector API is entry-scoped  
2. UI does not perform client-side TG compatibility filtering  
3. Create/Update/Upsert DTOs do not accept `teachingGroupId`  
4. Assignment uses dedicated API  
5. Clear uses dedicated API  
6. No TimetableSection API in selector contract  

Plus: dialog still not wired (this prompt stops at contract).

---

## 10. Deferred items

| Item | Owner |
|---|---|
| Implement `GET …/compatible-teaching-groups` on API using `TeachingGroupRules` / assign-load path | Backend (next TG.6 Prompt 4 sub-prompt or dedicated API prompt) |
| Wire selector UI in `TimetableEntryDialog` | Prompt 4 UX implementation |
| Optional grid chip label for TG | Later UX polish |

---

## 11. Confirmation

- [x] No timetable UI redesign  
- [x] No client compatibility filter algorithm  
- [x] No Create/Update/Upsert `teachingGroupId`  
- [x] Assign/clear remain dedicated endpoints  
- [x] No TimetableSection / Attendance / membership / TG-create from this contract  
- [x] Backend query endpoint documented as dependency (not invented via client filter)

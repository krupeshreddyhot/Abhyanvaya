# AI-SCHED-TG.6 Prompt 2 — Teaching Group UI Client Contract Completion

**Workstream:** AI-SCHED-TG.6  
**Prompt:** 2 — UI Client Contract Completion  
**Date:** 2026-08-19  
**Type:** IMPLEMENTATION — frontend API/client layer only  
**Status:** PASS

---

## 1. Existing client structure

| Client | Path | Role |
|---|---|---|
| Shared HTTP | `abhyanvaya-ui/src/api/axios.ts` | Single axios instance (`/api` base) |
| Teaching Group | `abhyanvaya-ui/src/services/teachingGroupService.ts` | TG CRUD, sections, memberships |
| Timetable / scheduling | `abhyanvaya-ui/src/services/schedulingService.ts` | Timetables, entries, grid |

No second HTTP client and no parallel TeachingGroup/timetable service were introduced.

---

## 2. Added client methods

### `teachingGroupService.ts`

| Method | HTTP | Path |
|---|---|---|
| `getResolvedTeachingGroupMembers(id)` | GET | `/scheduling/teaching-groups/{id}/resolved-members` |
| `addTeachingGroupMembers(id, payload)` | POST | `/scheduling/teaching-groups/{id}/memberships` |
| `replaceTeachingGroupMemberships(id, payload)` | PUT | `/scheduling/teaching-groups/{id}/memberships` |
| `removeTeachingGroupMember(id, studentId)` | DELETE | `/scheduling/teaching-groups/{id}/memberships/{studentId}` |

Existing methods retained: list/get/create/update/archive, raw memberships GET, sections CRUD.

**Contract safety:** resolved membership is transport-only. The client does **not** compute `Resolved = Base ∪ Includes − Excludes`.

### `schedulingService.ts`

| Method | HTTP | Path |
|---|---|---|
| `assignTeachingGroupToTimetableEntry(entryId, payload)` | PUT | `/scheduling/timetables/entries/{entryId}/teaching-group` |
| `clearTeachingGroupFromTimetableEntry(entryId)` | DELETE | `/scheduling/timetables/entries/{entryId}/teaching-group` |

---

## 3. DTO changes

### Teaching Group (mirror backend)

- `TeachingGroupMembershipInclusion` / `TeachingGroupMemberProvenance`
- `ResolvedTeachingGroupMemberDto` (`studentId`, `provenance`)
- `AddTeachingGroupMembersRequest` (`studentIds`, optional `effectiveFrom`)
- `ReplaceTeachingGroupMembershipsRequest` (`includeStudentIds`, optional `excludeStudentIds`)
- `TeachingGroupMembershipMutationResultDto` (`teachingGroupId`, `resolvedStudentCount`, `memberships`, `resolvedMembers`)

### Timetable

- `TimetableEntryDto.teachingGroupId?: number | null` — **response field only**
- `AssignTeachingGroupToTimetableEntryRequest` (`teachingGroupId: number`)
- `CreateTimetableEntryRequest` / `UpdateTimetableEntryRequest` / `UpsertTimetableEntryRequest` — **intentionally omit** `teachingGroupId` so ordinary edits cannot clear TG

---

## 4. Exact backend endpoints consumed

| Operation | Endpoint |
|---|---|
| Resolved members | `GET /api/scheduling/teaching-groups/{id}/resolved-members` |
| Add membership | `POST /api/scheduling/teaching-groups/{id}/memberships` |
| Replace overlays | `PUT /api/scheduling/teaching-groups/{id}/memberships` |
| Remove membership | `DELETE /api/scheduling/teaching-groups/{id}/memberships/{studentId}` |
| Assign TG | `PUT /api/scheduling/timetables/entries/{entryId}/teaching-group` |
| Clear TG | `DELETE /api/scheduling/timetables/entries/{entryId}/teaching-group` |

(UI axios paths omit the `/api` prefix already present on the shared client.)

---

## 5. Error handling

Unchanged: axios rejects with status + body; UI uses `getApiErrorMessage` / `getHttpStatus`.

Verified:

- **409** concurrency body is passed through for display (safe Conflict message)
- **400 / 403 / 404 / network** continue to use existing helpers
- No SQL, PostgreSQL constraint names, tenant, or internal exception details are surfaced by the client layer

---

## 6. RBAC handling

No client-side authorization replacement. Pages continue to use `AuthContext.hasPermission` + `PermissionKeys`. Server remains authoritative. This prompt adds no UI controls that invoke the new methods.

---

## 7. Tests

| File | Coverage |
|---|---|
| `src/services/aiSchedTg6Prompt2UiClientContract.test.ts` | URLs 1–6, TG response mapping, null TG, update does not send `teachingGroupId`, 409 passthrough |
| `src/pages/setup/scheduling/AiSchedTg6Prompt2UiClientContractGuard.test.ts` | No parallel client, request DTO omission, no UI wiring, no TimetableSection/Attendance writes from TG service |
| `AiSchedTg5Prompt3TeachingGroupUiGuard.test.ts` | Updated: page still must not call new membership mutation methods |

---

## 8. Build results

| Check | Result |
|---|---|
| `tsc -b` / `npm run build` (typecheck + vite) | **PASS** |
| Prompt 2 + related UI unit tests | **PASS** |
| Backend source | **unchanged** |
| Schema / migrations | **unchanged** |

---

## 9. Confirmation — no UI behavior changed

- No buttons, fields, dialogs, membership editor, or timetable TG selector added
- `TeachingGroupsPage` still shows “Membership management is not yet available”
- `TimetableEntryDialog` does not reference assign/clear TG or `teachingGroupId`

---

## 10. Confirmation — no backend / schema changes

- No API controller, Application, Domain, or Infrastructure edits
- No EF migrations or database changes
- No Attendance / StudentSection / TimetableSection modifications

---

## Architecture guards (narrow)

UI vitest guards prevent:

- Parallel TG HTTP client / auto-create / SA→TG inference helpers in `teachingGroupService`
- `teachingGroupId` on Create/Update/Upsert timetable request property lists
- Premature UI wiring of membership mutations / timetable TG assign-clear
- TimetableSection / Attendance mutation strings in the TG client

---

## Acceptance checklist

- [x] Existing `teachingGroupService` extended
- [x] No duplicate service
- [x] resolved-members client added
- [x] membership POST / PUT / DELETE added
- [x] timetable assign / clear client added
- [x] `teachingGroupId` response field supported
- [x] ordinary timetable update does not clear TG
- [x] null TG supported
- [x] Existing error handling preserved
- [x] Existing RBAC patterns preserved
- [x] No TimetableSection writes
- [x] No Attendance changes
- [x] No backend changes
- [x] No schema changes
- [x] No UI redesign
- [x] TypeScript typecheck/build passes
- [x] Relevant UI tests pass
- [x] Architecture guards pass

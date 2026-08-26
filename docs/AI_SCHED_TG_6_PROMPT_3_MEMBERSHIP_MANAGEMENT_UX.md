# AI-SCHED-TG.6 Prompt 3 — Teaching Group Membership Management UX

**Workstream:** AI-SCHED-TG.6  
**Prompt:** 3 — Membership Management UX  
**Date:** 2026-08-19  
**Type:** IMPLEMENTATION — UI only (existing Teaching Groups page)  
**Status:** PASS

---

## 1. Existing Teaching Group page reused

- Route unchanged: `/setup/scheduling/teaching-groups`
- Page: `TeachingGroupsPage.tsx`
- No duplicate Teaching Group page
- Scheduling catalog unchanged
- Timetable designer unchanged

---

## 2. Membership UX implemented

Additive panel: `TeachingGroupMembershipPanel.tsx`

Wired into the existing detail panel after Sections.

Helpers:

- `teachingGroupMembershipUi.ts` — labels, partitions, capacity warning, conflict copy
- `teachingGroupMembershipActions.ts` — POST/DELETE + authoritative reload + 409 handling

---

## 3. ExplicitStudents behavior

When `MembershipSource = ExplicitStudents` and user has Manage (and not Archived):

- Paginated student search (`getStudents`)
- Multi-select add → `addTeachingGroupMembers` (POST)
- Remove with `AcademicConfirmDialog` → `removeTeachingGroupMember` (DELETE)
- Included list = current Include overlays (intent)
- Resolved roster = server `getResolvedTeachingGroupMembers` (read-only)

Bulk **replace** deliberately **not** exposed (safer Add/Remove; backend replace exists but UX deferred to avoid accidental wipe).

---

## 4. Hybrid behavior

When `MembershipSource = Hybrid`:

| Surface | Editable? |
|---|---|
| Included overlays | Yes (remove include via DELETE) |
| Excluded overlays | Clear exclude via POST Add (ends Exclude per backend) |
| Derived (provenance Derived) | Roster read-only; **Exclude** action uses DELETE |
| Resolved | Always read-only |

Section / Combined / StudentSubject sources: mutations disabled; resolved roster view-only with guidance to edit sections/enrollment instead.

---

## 5. Resolved membership behavior

- Loaded only via `getResolvedTeachingGroupMembers`
- Count shown from Teaching Group detail `resolvedStudentCount` after reload
- UI never computes Base ∪ Includes − Excludes
- Empty resolved roster is allowed

---

## 6. API calls used

| Action | Client |
|---|---|
| Load detail | `getTeachingGroup` |
| Load overlays | `getTeachingGroupMemberships` |
| Load resolved | `getResolvedTeachingGroupMembers` |
| Add | `addTeachingGroupMembers` |
| Remove / exclude | `removeTeachingGroupMember` |
| Student search | `getStudents` (existing) |

Not used from membership UX: `replaceTeachingGroupMemberships` (deferred safer UX), TimetableSection APIs, Attendance, StudentSection writes, TeachingGroupSection APIs.

---

## 7. 409 concurrency behavior

On HTTP 409:

1. Do **not** auto-retry
2. Do **not** merge client/server silently
3. Show `MEMBERSHIP_CONFLICT_MESSAGE`
4. Reload Teaching Group + memberships + resolved members
5. Clear pending selections via state apply
6. User must review before retrying

---

## 8. RBAC behavior

- View: membership + resolved visible; no mutate controls
- Manage: search/add/remove/exclude when source is Explicit or Hybrid and not Archived
- Server remains authoritative (`ProtectedRoute` + API policies unchanged)

---

## 9. Capacity display

Chips/labels:

- Resolved students (server)
- Expected (planning intent)
- Max teaching capacity (teaching ceiling — **not** room capacity)

Warning Alert when `ResolvedStudentCount > MaxTeachingCapacity` (configured). No auto-fix.

---

## 10. Accessibility

- Heading `aria-labelledby`
- Labeled search field
- Checkbox list with `aria-label` / `aria-labelledby`
- Confirm dialog uses `AcademicConfirmDialog` accessible title/description
- Loading / empty / error / over-capacity statuses
- Mutation disables controls while in progress

---

## 11. Error handling

Uses `getApiErrorMessage` / `getHttpStatus` for 400/403/404/409/network. No SQL/constraint/tenant/stack exposure.

---

## 12. Tests

| File | Focus |
|---|---|
| `teachingGroupMembershipUi.test.ts` | helpers + add/remove/409/403/reload |
| `AiSchedTg6Prompt3MembershipUxGuard.test.ts` | page wiring, RBAC gates, no TimetableSection/Attendance, Hybrid tables |
| Updated Prompt 2/5 UI guards | membership banner removed; timetable still untouched |

---

## 13. Build results

| Check | Result |
|---|---|
| `tsc -b` | **PASS** |
| Relevant vitest suites (Prompt 2–3 TG UI) | **PASS** (50 tests) |
| `npm run build` (`tsc -b && vite build`) | **PASS** |

---

## 14. Architecture guard results

Guards assert:

- No TimetableSection / Attendance / StudentSection mutation from membership panel
- No client membership resolver
- No SA→TG inference / auto-create
- Approved `teachingGroupService` methods only
- Timetable entry dialog unchanged

---

## 15. Deferred items

| Item | Reason |
|---|---|
| `replaceTeachingGroupMemberships` bulk UI | Backend supports it; safer Add/Remove preferred per prompt |
| Student name enrichment for IDs never seen in search | No get-by-id list API used; shows `Student #id` until search caches hint |
| Timetable Teaching Group selector | Prompt 4 |

---

## 16. Confirmation — no backend / database changes

- No API/Application/Domain/Infrastructure edits
- No migrations / schema changes
- No Attendance / StudentSection / TimetableSection / TeachingGroupSection behavior changes in this prompt (section UI on the same page remains as before; membership panel does not call section APIs)

---

## Acceptance checklist

- [x] Existing Teaching Group page reused
- [x] No duplicate page
- [x] Membership source displayed
- [x] Explicit membership editing
- [x] Resolved roster from server
- [x] UI does not calculate resolved membership
- [x] Add/Remove via approved API
- [x] Replacement not exposed (deferred safely)
- [x] Hybrid semantics respected
- [x] Derived/Resolved not directly editable as rosters
- [x] 409 reload / no auto-retry / no overwrite
- [x] View/Manage permissions
- [x] Capacity semantics + warning
- [x] Room capacity not confused
- [x] Section / TimetableSection / Attendance / StudentSection untouched by membership ops
- [x] No auto TG / no SA→TG inference
- [x] A11y / loading / empty / error / duplicate-mutation prevention
- [x] Tests / typecheck / guards
- [x] No backend / DB / migrations
- [x] Documentation created

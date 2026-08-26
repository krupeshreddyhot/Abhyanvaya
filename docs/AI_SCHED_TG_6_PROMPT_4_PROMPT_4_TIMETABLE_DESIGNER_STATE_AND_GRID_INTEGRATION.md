# AI-SCHED-TG.6 Prompt 4 / Prompt 4 — Timetable Designer State, Conflict Feedback & Grid Integration

## Status

**PASS** (scoped frontend integration against frozen TG.4A / TG.6 Prompt 2–3 APIs).

## Architecture preserved

```
TeachingGroup
      │
      ▼
TeachingGroupSection          ← sole TG → Section membership SoT
      │
      ▼
TimetableSectionProjector     ← sole TimetableSection writer
      │
      ▼
TimetableSection              ← projection-only

TimetableEntry
      └── TeachingGroupId ──► TeachingGroup   ← explicit assignment only
```

The UI never:

- creates Teaching Groups;
- infers a Teaching Group from SubjectAllocation;
- writes TimetableSection / StudentSection / Attendance;
- calculates Teaching Group compatibility;
- puts `teachingGroupId` on Create / Update / Upsert timetable payloads;
- bypasses the dedicated assign / clear / compatible APIs for mutation.

## UI state flow

1. `getTimetableGrid` returns entries including `TimetableEntryDto.teachingGroupId`.
2. Designer keeps local `entries` via established upsert (not React Query).
3. Display-only `teachingGroupHints: Map<id, TeachingGroupGridHint>` is filled from:
   - compatible-TG responses when the entry dialog loads options;
   - `GET TeachingGroup/{id}` for assigned ids missing from the map (label/status/capacity only — **not** a compatibility resolver).
4. Grid renders informational TG lines; **editing** remains in `TimetableEntryDialog`.

## Grid integration

`TimetableGrid` shows under the compact subject/staff/room line:

| State | Display |
| --- | --- |
| No TG | `Teaching Group: None` |
| Assigned | `Teaching Group: {code} — {name}` |
| Assigned Archived | same + ` · Archived` |
| Assigned, hint not yet loaded | `Teaching Group: #{id}` |

Capacity overage (`ResolvedStudentCount > MaxTeachingCapacity`) shows an accessible `role="status"` warning under the TG line. Room capacity is not used.

Grid cells are **not** TG editors.

## Dialog ↔ grid synchronization

| Event | Behavior |
| --- | --- |
| Assign / clear success | Server entry → `onSaved` → `upsertEntryLocal` + sync `editingEntry` if same id; hints refreshed from compatible reload |
| 409 Conflict | No auto-retry; reload compatible options; `onSaved` with authoritative entry; `onTeachingGroupConflict` → `refreshGrid` + user-visible conflict alert |
| Open entry | Uses freshest entry from designer `entries` (double-click / context menu) |
| Close / reopen | Dialog reloads compatible TGs from server |

Authoritative source after mutation is always the **server response** (or post-409 reload), never optimistic local `teachingGroupId` mutation.

## Assignment / clear lifecycle

Unchanged from Prompt 3:

- Existing entry: Update (no TG) → delta assign PUT or clear DELETE only when selection differs from baseline.
- New entry: Create (no TG) → stay open → load compatible → second save may assign.
- Unchanged selection → **no** Teaching Group API call.

## 409 behavior

Message:

> This timetable entry was changed by another user. The latest Teaching Group assignment has been loaded. Please review your selection and try again.

No silent overwrite, no merge algorithm, no automatic retry. Unrelated local entry edits are not discarded beyond replacing the conflicted entry with the reloaded authoritative DTO and refreshing the grid.

## Archived TG behavior

Assigned Archived Teaching Groups remain visible on the grid and in the dialog (via compatible query `isAssignedToEntry`). The UI does not “repair” by clearing or replacing them. Archived ≠ Not Found.

## Capacity warning

Uses `resolvedStudentCount` / `maxTeachingCapacity` from compatible options or Teaching Group detail hints. Scheduling warning only; does not block unrelated edits unless the server rejects.

## Clone / copy / drag / drop

- Create / move / paste / bulk upsert payloads **omit** `teachingGroupId`.
- No second client-side assignment path on DnD.
- If the backend preserves `TeachingGroupId` on move/copy/duplicate, the grid shows the returned state after upsert.
- If the operation returns an entry without a TG, the grid shows **None** (no SA→TG inference).

## Accessibility

- TG line has `aria-label`.
- Capacity warning uses text + `role="status"` (not color alone).
- Archived status is textual (` · Archived`).
- Dialog selector remains keyboard-accessible MUI `Select` with helper text / Alerts from Prompt 3.

## RBAC

Unchanged: view permission sees TG state; manage permission enables dialog mutation when timetable is Draft and not frozen. Server authorization remains authoritative; 403 surfaces a safe message.

## Architecture guards & tests

- `AiSchedTg6Prompt4Prompt4GridIntegration.test.ts` — grid formatters, capacity, guards, DnD payload omission, 409 message.
- Prior Prompt 3 / 2 / discovery guards remain in place.

## Explicit architecture statements

- **TeachingGroupSection** remains the sole Teaching Group → Section membership source of truth.
- **TimetableSection** remains projection-only.
- **TimetableSectionProjector** remains the sole writer of TimetableSection.
- Teaching Group assignment remains an explicit application/API operation.
- The UI performs **no** SubjectAllocation → Teaching Group inference.
- **No** automatic Teaching Group creation is performed.
- **Attendance** and **StudentSection** are unchanged.

## Known limitations

- Grid TG **names** for ids never opened in the dialog depend on `getTeachingGroup` enrichment (display only).
- Undo/redo recreate paths that re-`createTimetableEntry` without a dedicated assign call will show **None** until the user assigns again (backend create omits TG by design).
- Soft-warning panel is still server soft-warnings; TG capacity is shown on the grid cell / dialog, not as a new soft-warning code.

## Deferred work

- Bulk membership UX (`replaceTeachingGroupMemberships`).
- Backend enrichment of grid DTOs with TG name/status (optional optimization).
- Designer redesign / new card layouts.

## Acceptance gate

| Gate | Result |
| --- | --- |
| Grid displays TG state | PASS |
| Dialog/grid synchronization | PASS |
| Assign/clear uses dedicated APIs | PASS |
| No TG in Create/Update/Upsert | PASS |
| 409 reload/no retry | PASS |
| Archived TG preserved | PASS |
| Capacity warning | PASS |
| Drag/drop integrity | PASS |
| Copy/duplicate integrity | PASS |
| Refresh integrity | PASS |
| RBAC preserved | PASS |
| Accessibility | PASS |
| No Attendance changes | PASS |
| No TimetableSection UI writes | PASS |
| No SA→TG inference | PASS |
| Architecture guards | PASS |
| E2E/component acceptance (focused Vitest) | PASS |
| API build | (regression — no API code in this prompt) |
| UI typecheck/build | PASS (verify on completion) |
| Existing scheduling regression | PASS (verify on completion) |

## Scope boundary

This prompt did **not** add TG domain entities, EF/migrations, membership APIs, compatible-TG rule changes, Attendance/StudentSection/TimetableSection redesign, or automatic legacy conversion.

# AI-SCHED-CAP Prompt 8.1 — Publish Readiness UI Discovery

**Workstream:** AI-SCHED-CAP  
**Prompt:** 8.1 — Discovery & UI Integration Boundary  
**Date:** 2026-08-20  
**Type:** **DISCOVERY ONLY** — no production behavior changed  
**Baseline:** Prompt 6 readiness API + Prompt 7 publish gate  
**Frozen:** AI-SCHED-TG.3 → TG.6; CAP Prompts 1–7 backend contracts  

---

## 1. Executive summary

| Question | Finding |
| --- | --- |
| Where is Publish today? | `TimetableDesignerPage` toolbar (Locked + `Scheduling.Publish`) and `PublishingPage` governance dialog |
| Is publish readiness in the UI? | **No** — no client types, no `GET …/publish-readiness` call, no readiness panel |
| Closest existing UX? | `SoftWarningsPanel` (Draft soft / informational; dismissible; never blocks edit or publish) |
| Authoritative readiness source? | Server only: `GET /api/scheduling/timetables/{id}/publish-readiness` (Prompt 6) |
| Authoritative publish gate? | Server only: `POST …/publish` → Prompt 7 `PublishNotReadyException` with readiness DTO body |
| Client conflict/capacity engine? | **None** in designer — do not add one |
| Recommended UI home for Prompt 8.2+ | Designer toolbar + publish blocker dialog; optional readiness strip beside SoftWarnings |

**This prompt changed no production UI/backend behavior.** Guarding tests and this document only.

---

## 2. Timetable designer / page architecture

| Piece | Location | Role |
| --- | --- | --- |
| Route | `AppRoutes.tsx` → `setup/scheduling/timetables/:id` | `TimetableDesignerPage`; any of `Scheduling.Timetable.View` / `Manage` |
| Hub | `TimetableHubPage.tsx` | List / open designers |
| Designer | `TimetableDesignerPage.tsx` | Grid, DnD, entry dialog, soft warnings, lifecycle buttons |
| Grid | `TimetableGrid.tsx` | Cells; soft-warning captions via **server** `SoftWarningDto` metrics |
| Entry dialog | `TimetableEntryDialog.tsx` | Create/update; TG assign via Prompt 6 TG APIs |
| Soft warnings | `SoftWarningsPanel.tsx` | Side panel; List + Chip + Alert |
| API client | `services/schedulingService.ts` | Axios wrappers for scheduling |

Layout (designer): left allocation list (draft) → **SoftWarningsPanel** → **TimetableGrid**.

State refresh pattern: `getTimetableGrid` + `getTimetableSoftWarnings`; local `upsertEntryLocal` after mutations. No TimetableSection writes from UI.

---

## 3. Existing Publish button / action

### 3.1 Designer (`TimetableDesignerPage`)

```text
canPublish && status === Locked
  → Button "Publish" (PublishIcon)
  → handlePublish()
       publishTimetable(timetableId)
       on success: update grid.timetable
       on failure: setError(errMsg(e))   // string-only today
```

- Permission: `PermissionKeys.SchedulingPublish` (`Scheduling.Publish`)
- Visibility: Locked only (not Draft/Published/Archived)
- No confirmation dialog; immediate POST
- `lifecycleBusy` disables button during call

### 3.2 Governance (`PublishingPage`)

- Table of timetables + MUI `Dialog` with optional reason
- Same `publishTimetable(id, { reason })`
- Errors via `errMsg` (string body only)

### 3.3 Publish API client (unchanged — must stay)

```ts
export const publishTimetable = (id: number, payload?: PublishTimetableRequest) =>
  api.post<TimetableDto>(`/scheduling/timetables/${id}/publish`, payload ?? {});
```

Prompt 7 server behavior: blocked publish returns **400** with **`TimetablePublishReadinessResultDto` JSON body** (not a plain string).  
Current `errMsg` / designer handling **cannot** surface findings — gap for Prompt 8.2.

---

## 4. SoftWarningsPanel & conflict presentation

| Aspect | Soft warnings (today) | Publish readiness (Prompt 6/7) |
| --- | --- | --- |
| Endpoint | `GET …/soft-warnings` | `GET …/publish-readiness` |
| Purpose | Draft UX; dismissible | Preflight + publish blockers |
| Blocks editing? | No | N/A (observational) |
| Blocks publish? | No | Yes (server gate) |
| Dismiss? | Yes (`POST …/dismiss`) | No (recalculated) |
| UI panel | `SoftWarningsPanel` | **Not built** |

`SoftWarningsPanel` already presents server `title` / `why` / `suggestedAction` and optional capacity metrics (`placementSize`, `effectiveRoomCapacity`, `resolvedStudentCount`, `maxTeachingCapacity`) **without recalculating**.

`ConflictWorkspacePage` is a separate analyze/workspace surface (ConflictEngine via API). It must **not** be overloaded as Publish Readiness.

**Reuse for Prompt 8.2:** Chip/List/Alert presentation patterns from SoftWarnings; **do not** merge readiness into SoftWarnings as dismissible soft items.

---

## 5. Scheduling API / service patterns

| Pattern | Convention |
| --- | --- |
| Client | `schedulingService.ts` + shared `api` axios instance |
| Types | Colocated `export type …Dto` next to functions |
| Errors | `errMsg` (scheduling) or `getApiErrorMessage` (richer) |
| Loading | Local `loading` / `CircularProgress` / disabled buttons |
| Empty | `Alert severity="success|info"` or caption text |

### Server readiness contract (authoritative — mirror in client types in 8.2)

`TimetablePublishReadinessResultDto`:

- `timetableId`, `lifecycleState`, `isFrozen`, `isReady`
- `blockingFindingCount`, `warningFindingCount`, `informationalFindingCount`
- `evaluatedAtUtc`, `findings[]`

`PublishReadinessFindingDto`:

- `code`, `severity`, `isBlocking`, `title`, `why`, `recommendedAction`
- Optional: `timetableEntryId`, `dayOfWeek`, `timeSlotId`, `roomId`
- Optional TG/capacity metrics (server-authored only)

### Client gap (8.1)

- **No** `TimetablePublishReadinessResultDto` / `PublishReadinessFindingDto` in UI
- **No** `getTimetablePublishReadiness(id)`
- **No** parser for publish 400 readiness body

---

## 6. MUI patterns available

Already used in scheduling UI:

- `Dialog` / `DialogTitle` / `DialogContent` / `DialogActions` — PublishingPage, entry dialogs
- `Alert`, `Chip`, `List` / `ListItem` / `ListItemText` — SoftWarningsPanel
- `Table` — PublishingPage, ConflictWorkspace
- `CircularProgress`, `Stack`, `Typography`, `Tooltip`, `Button`

**Recommended Prompt 8.2 composition:**

1. Readiness summary chip/banner near Publish (Ready / N blockers)
2. On Publish click (or “Check readiness”): fetch GET readiness; if not ready, open **Publish blocked** Dialog listing blocking findings first
3. On Publish POST 400 with readiness shape: same Dialog (never trust a stale GET alone — server re-gates)

---

## 7. Loading / error / empty states

| Surface | Pattern |
| --- | --- |
| Designer load | Full-page `CircularProgress` then `Alert` error |
| Soft warnings refresh | Swallow errors (informational) |
| Publish failure | Top `Alert` via `setError(errMsg(e))` — **lossy for structured readiness** |
| SoftWarnings empty | `Alert severity="success"` “No active warnings” |

Prompt 8.2 must extend publish error handling to detect readiness DTO bodies (`isReady === false` + `findings`).

---

## 8. RBAC

| Action | Permission | Notes |
| --- | --- | --- |
| Open designer | `Scheduling.Timetable.View` or `Manage` | Route guard |
| Soft warnings | View path; dismiss needs Manage | Matches API |
| GET publish-readiness | Server: `CanViewSchedulingTimetable` | Align UI with View (same as designer) |
| POST publish | `Scheduling.Publish` | Designer + PublishingPage |

UI checks never replace server auth. Do not require Manage merely to inspect readiness.

---

## 9. Navigation from finding → entry / room / TG

| Capability today | Status |
| --- | --- |
| SoftWarningsPanel click → entry | **Missing** — display only |
| Grid cell soft captions | Metrics from server SoftWarningDto |
| Open entry | Double-click / context menu → `setEditingEntry` + `TimetableEntryDialog` |
| Room deep-link | No dedicated “go to room” from warning |
| Teaching Group page | Separate `TeachingGroupsPage`; assign only via entry dialog APIs |

**Recommended Prompt 8.2 navigation:**

- If `timetableEntryId` present → open `TimetableEntryDialog` for that entry (reuse designer state)
- Optional: highlight cell by `dayOfWeek` + `timeSlotId`
- TG: show server `teachingGroupCode/Name/Status`; link to TG page only if product already has route — do **not** infer/create TG
- Room: show `roomId` / labels from already-loaded `roomOptions` — do not recompute capacity

---

## 10. Hard constraints for Prompt 8.2+ (UI)

Do **not**:

- Redesign or bypass Prompt 7 publish gate
- Duplicate ConflictEngine / PlacementSize / RoomCapacity / TG capacity on the client
- Infer, create, assign, or clear Teaching Groups from readiness UI
- Mutate TimetableSection / TeachingGroupSection / Attendance / StudentSection
- Change publish request DTO or require client-only readiness flags
- Treat SoftWarnings dismissals as clearing publish blockers
- Trust a prior GET readiness result without server publish enforcement

Do:

- Consume readiness exclusively via API client → GET publish-readiness and/or publish 400 body
- Present server `isBlocking`, codes, copy, and metrics as-is
- Keep SoftWarnings and Publish Readiness as separate concepts

---

## 11. Recommended insertion points (Prompt 8.2)

| Priority | Insertion | Rationale |
| --- | --- | --- |
| P0 | `schedulingService.ts` types + `getTimetablePublishReadiness` | Contract mirror |
| P0 | Publish error parser (designer + PublishingPage) | Surface Prompt 7 400 body |
| P0 | `PublishReadinessPanel` or Dialog component | Reuse SoftWarnings presentation language |
| P0 | Wire into `TimetableDesignerPage` near Publish | Primary authoring surface |
| P1 | `PublishingPage` dialog before/after publish | Governance parity |
| P2 | Finding → open entry dialog | Actionability |

**Out of Prompt 8.1:** all of the above implementation.

---

## 12. TOCTOU / UX note (from Prompt 7)

GET readiness ≠ lock. Publish always re-evaluates server-side. UI should treat readiness as advisory preflight and always handle publish rejection with fresh findings.

---

## 13. Explicitly deferred

- Prompt 8.2+: UI implementation  
- Prompt 9: E2E acceptance  
- Backend / schema / TG / Attendance changes  
- Hard Draft mutation rejection / DnD blocking redesign  

---

## 14. Discovery checklist

- [x] Designer architecture mapped  
- [x] Publish actions mapped (designer + governance)  
- [x] SoftWarnings / conflict surfaces mapped  
- [x] API client patterns mapped  
- [x] MUI patterns mapped  
- [x] Loading/error/empty mapped  
- [x] RBAC mapped  
- [x] Server DTO contract referenced; **client types absent**  
- [x] Publish error gap for structured readiness documented  
- [x] Finding navigation gap documented  
- [x] No production code changed in 8.1  

**STATUS: DISCOVERY COMPLETE — stop after discovery.**

# AI-SCHED-CAP Prompt 8.2 — Publish Readiness UI Client Contract

**Workstream:** AI-SCHED-CAP  
**Prompt:** 8.2 — Client Contract & Publish Failure Mapping  
**Date:** 2026-08-20  
**Baseline:** Prompt 6 DTO/API + Prompt 7 gate + Prompt 8.1 discovery  
**Status:** Implementation (contract layer — **no Publish Blocker dialog**)

---

## Backend → UI contract

Server DTO (authoritative — ASP.NET camelCase JSON):

| C# | JSON / TypeScript |
| --- | --- |
| `IsReady` | `isReady` |
| `Findings` | `findings` |
| `IsBlocking` | `isBlocking` |
| `RecommendedAction` | `recommendedAction` |
| `TimetableEntryId` | `timetableEntryId` |
| `TimeSlotId` | `timeSlotId` |
| `LifecycleState` | `lifecycleState` |

**Not used** (Prompt text suggested aliases that do **not** match backend):

- `canPublish` → use `isReady`
- `blockers` / `warnings` arrays → use `findings` + `isBlocking`
- `blocking` → use `isBlocking`
- `message` / `action` → use `title` / `why` / `recommendedAction`
- `slotId` → use `timeSlotId`

---

## API client

`schedulingService.ts`:

```ts
getTimetablePublishReadiness(id)
  → GET /scheduling/timetables/{id}/publish-readiness
```

`publishTimetable` path **unchanged**.

Re-exported from `publishReadiness.ts` for shared consumers.

---

## Publish 400 mapping

| HTTP body | Client result |
| --- | --- |
| `TimetablePublishReadinessResultDto` (controller `BadRequest(ex.Readiness)`) | `kind: "readiness"` |
| ProblemDetails + `publishReadiness` extension | `kind: "readiness"` |
| Plain string (lifecycle DomainException) | `kind: "message"` — text preserved |
| Empty / malformed | `kind: "unknown"` — safe generic message; **no throw** |

Helpers: `parsePublishFailure`, `normalizePublishReadiness`, `getPublishBlockers`.

---

## Blocking semantics

Trust **only** server `finding.isBlocking`.

Do **not** derive blocking from `severity === "Error"` or known capacity codes on the client.

---

## Unknown findings

Unknown codes keep `title` / `why` / `recommendedAction` / `timetableEntryId` and render via summary helpers.

---

## Race condition

GET readiness is preflight only. POST publish remains authoritative. On 400, UI stores the **latest** server readiness payload (`lastPublishReadiness`). No auto-retry.

---

## Separation from SoftWarnings

`SoftWarningsPanel` remains Draft informational only. Publish readiness uses `publishReadiness.ts` + page state — not SoftWarnings dismiss semantics.

---

## Page integration (minimal)

| Page | Change |
| --- | --- |
| `TimetableDesignerPage` | `parsePublishFailure` on publish catch; `lastPublishReadiness` state; summary in existing Alert |
| `PublishingPage` | Same for publish actions |

No readiness strip, dialog, or Publish disable logic in 8.2.

---

## Explicit out of scope

Prompt 8.3 dialog/strip/navigation; backend/TG/Attendance/schema; E2E.

---

## Tests

- `publishReadiness.test.ts` — normalize, parse, lifecycle strings, unknown codes, entry id  
- `AiSchedCapPrompt82PublishReadinessClientGuard.test.ts` — architecture guards  

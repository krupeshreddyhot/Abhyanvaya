# AI-SCHED-CAP Prompt 8.3 — Publish Readiness Blocker UX

**Workstream:** AI-SCHED-CAP  
**Prompt:** 8.3 — Blocker UX, Finding Navigation & Designer/Publishing Integration  
**Date:** 2026-08-20  
**Baseline:** Prompt 8.2 client contract  
**Status:** Implementation  

---

## UX architecture

```text
GET /publish-readiness  (preflight UX)
POST /publish           (authoritative gate — Prompt 7)
        │
        ├── success → clear readiness panel state
        └── 400 readiness → parsePublishFailure → PublishReadinessPanel
```

Shared presentation:

`PublishReadinessPanel` used by:

- `TimetableDesignerPage`
- `PublishingPage`

---

## Component structure

`PublishReadinessPanel`

- Status: Ready / Cannot publish / Loading / Error / Not evaluated
- Blockers via `getPublishBlockers` (**server `isBlocking` only**)
- Generic finding fallback for unknown codes
- Optional metrics/context from server fields only
- **Re-check** → GET publish-readiness
- **View entry** when `timetableEntryId` present

---

## SoftWarnings separation

| SoftWarningsPanel | PublishReadinessPanel |
| --- | --- |
| Draft informational | Publication eligibility |
| Dismissible | Recalculated / not dismissed |
| Never blocks edit | Explains publish blockers |

---

## Finding navigation

- Designer: opens existing `TimetableEntryDialog` for the entry
- PublishingPage: navigates to `/setup/scheduling/timetables/{id}?entryId=`
- Designer consumes `entryId` query once, then clears it
- No navigation button when `timetableEntryId` is missing (no inference)

---

## Publish failure / concurrency

- POST remains authoritative; GET is preflight only
- Publish button disabled only while request in progress (`lifecycleBusy` / `acting`)
- **Not** disabled by cached `isReady`
- Blocked POST replaces displayed readiness with latest server payload
- No automatic retry

---

## Accessibility

- Section `aria-labelledby="publish-readiness-heading"`
- Status/alert regions with `role` + `aria-live`
- Re-check / View entry have accessible names
- Severity chips + text labels (not color alone)

---

## Responsive

Findings stack vertically; panel is full-width on small screens (`maxWidth` on designer only).

---

## Tests

- `PublishReadinessPanel.test.ts` — labels, metrics, blocking filter
- `AiSchedCapPrompt83PublishReadinessBlockerUxGuard.test.ts` — architecture

---

## Browser E2E

**NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE**

---

## Explicit non-goals

No backend/API/schema/TG/Attendance changes; no client publish gate; no SoftWarnings replacement.

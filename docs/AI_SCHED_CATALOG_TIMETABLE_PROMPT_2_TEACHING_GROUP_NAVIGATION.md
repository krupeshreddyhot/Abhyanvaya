# AI-SCHED-CATALOG/TIMETABLE — Prompt 2 (P1-1)  
# Teaching Groups Navigation Fix

**Workstream:** AI-SCHED-CATALOG/TIMETABLE  
**Prompt:** 2 — P1-1 Teaching Groups Navigation Fix  
**Date:** 2026-08-21  
**Type:** Implementation + focused verification  
**Final status: PASS**

---

## Root cause

The Scheduling hub card correctly linked to the canonical route:

`/setup/scheduling/teaching-groups` → `TeachingGroupsPage`

The UI `ProtectedRoute` for that path required **only**:

- `Scheduling.TeachingGroup.View`
- `Scheduling.TeachingGroup.Manage`

When those claims were absent (typical tenant Administrator JWT resolved from `ApplicationRole` permissions **without** seeded TG keys), `ProtectedRoute` redirected to **`/dashboard`**.

Meanwhile the **API** policy `CanViewSchedulingTeachingGroup` (`AddSchedulingViewPolicy` in `Program.cs`) already accepts:

- TG View / Manage **or**
- `Scheduling.View` / `Scheduling.Manage`

So the hub card looked correct, but the UI route guard was stricter than the API and produced the Dashboard fall-through.

This was **not** a wrong card `to` path and **not** a missing route registration.

---

## Canonical route (unchanged)

| Item | Value |
| --- | --- |
| Path | `/setup/scheduling/teaching-groups` |
| Page | `TeachingGroupsPage` |
| Hub card | `schedulingCatalogConfig.tsx` key `teaching-groups` |

No second Teaching Groups page/module was created.

---

## Before / after

| | Before | After |
| --- | --- | --- |
| Card `to` | `/setup/scheduling/teaching-groups` | Same |
| UI route permissions | TG View/Manage only | TG View/Manage **+** Scheduling View/Manage (aligned with API) |
| User with Scheduling.View | Click → `/dashboard` | Click → Teaching Groups page |
| User with neither Scheduling nor TG | Click → `/dashboard` | Same (denied) |
| TG Manage (create/edit) | Still requires `Scheduling.TeachingGroup.Manage` | Unchanged |

---

## Authorization behavior

- Route protection retained via `ProtectedRoute`.
- Permission **identifiers** unchanged.
- Unauthorized users still denied (`Navigate` to `/dashboard`).
- Manage actions on the page still require `Scheduling.TeachingGroup.Manage`.
- API policies unchanged; UI view gate now matches API view policy.

---

## Files changed

| File | Change |
| --- | --- |
| `abhyanvaya-ui/src/routes/AppRoutes.tsx` | Expand Teaching Groups `anyPermission` to match API view policy |
| `abhyanvaya-ui/src/pages/setup/scheduling/TeachingGroupsPage.tsx` | Align `canView` with the same permission set |
| `abhyanvaya-ui/src/pages/setup/scheduling/AiSchedCatalogTimetableP1TeachingGroupNavigation.test.ts` | New focused navigation tests |
| `abhyanvaya-ui/src/pages/setup/scheduling/AiSchedTg5Prompt3TeachingGroupUiGuard.test.ts` | Expect Scheduling.View/Manage on route |
| `Abhyanvaya.Application.UnitTests/Scheduling/AiSchedTg5Prompt2ArchitectureGuardTests.cs` | Allowlist new P1-1 nav test filename (no TG surface expansion) |
| `docs/AI_SCHED_CATALOG_TIMETABLE_PROMPT_2_TEACHING_GROUP_NAVIGATION.md` | This document |

---

## Production behavior changed

Users who already have `Scheduling.View` or `Scheduling.Manage` (and can open the Scheduling hub / Subject Allocation) can now open **Teaching Groups** from the hub card instead of being redirected to Dashboard.

No CAP/TG domain, projector, TimetableEntry, Attendance, StudentSection, schema, or API policy changes.

---

## Migration

**None.** No schema change required.

---

## Browser E2E

**NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE**

---

## Verification checklist

| Criterion | Status |
| --- | --- |
| Card navigates to existing Teaching Groups page | PASS (route + link aligned) |
| Dashboard no longer destination for Scheduling-capable users | PASS |
| Existing TG page functionally unchanged | PASS |
| Authorization intact (unauthorized → Dashboard) | PASS |
| No duplicate TG module | PASS |
| No API/schema/CAP/TG architecture changes | PASS |
| Focused navigation tests | **PASS** (6 new + updated TG.5/TG.6 UI guards) |
| Regression | CAP/TG: **328 Passed**; scheduling UI Vitest: **107 Passed** |
| UI build | **PASS** |
| API build | N/A (UI-only) |
| Documentation | PASS |

**STOP** after Prompt 2 (P1-1). Do not start P1-2.

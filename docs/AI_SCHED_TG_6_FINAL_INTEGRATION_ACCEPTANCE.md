# AI-SCHED-TG.6 — Final Integration Acceptance

**Date:** 2026-08-20  
**Gate:** Conflict Feedback, Grid Integration & End-to-End Architectural Verification  
**Production behavior changes in this gate:** None (except stale discovery/guard test supersession updates).

Companion baseline: `docs/AI_SCHED_TG_6_FINAL_PROMPT_1_ARCHITECTURE_BASELINE.md`

---

## 1. Architecture status

| Milestone | Result |
| --- | --- |
| AI-SCHED-TG.2 | **PASS** |
| AI-SCHED-TG.2A | **PASS** (historical; SoT clarified in TG.4A) |
| AI-SCHED-TG.3 | **PASS** |
| AI-SCHED-TG.4 | **PASS** |
| AI-SCHED-TG.4A | **PASS** |
| AI-SCHED-TG.5 | **PASS** |
| AI-SCHED-TG.5A / 5A.1 / 5A.1A | **PASS** |
| AI-SCHED-TG.6 | **PASS** (Prompts 1–4 + Final gate verification) |

---

## 2. End-to-end architecture

```text
TeachingGroup
   ↓
Membership (resolver, server-side)
   ↓
Resolved Students / ResolvedStudentCount
   ↓
Compatible TG Query (server-authoritative)
   ↓
TimetableEntry.TeachingGroupId   ← dedicated Assign / Clear only
   ↓
TeachingGroupSection             ← sole TG→Section SoT
   ↓
TimetableSectionProjector        ← sole TimetableSection writer
   ↓
TimetableSection                 ← projection-only
   ↓
Attendance (timetable mode via TimetableSection; legacy fallback unchanged)
```

UI:

```text
Teaching Group Management → Teaching Group APIs
Timetable Designer → Compatible TG Query → Assign/Clear API
```

---

## 3. Source-of-truth confirmation

- **TeachingGroupSection** is the sole Teaching Group → Section membership source of truth.
- **TimetableSection** is projection-only.
- **TimetableSectionProjector** is the sole writer of `TimetableSection`.
- **TimetableEntry.TeachingGroupId** is the sole explicit Teaching Group assignment relationship for a timetable entry.
- There is **no second source of truth**.

---

## 4. Forbidden behavior confirmation

Verified present / closed:

| Forbidden | Confirmed |
| --- | --- |
| Automatic TG creation | Yes — closed |
| SubjectAllocation → TG inference | Yes — closed |
| UI compatibility calculation | Yes — closed |
| Direct TimetableSection writes (controller / TimetableService / clone / version / UI) | Yes — closed |
| Attendance schema redesign | Yes — not performed |
| StudentSection mutation from Scheduling TG UI | Yes — closed |
| Permanent legacy timetable backfill | Yes — not performed |
| RBAC weakening | Yes — not performed |
| Tenant-isolation bypass via `IgnoreQueryFilters` on TG Scheduling paths | Yes — closed |
| `TeachingGroupId` on Create/Update/Upsert / DnD / paste payloads | Yes — omitted |

---

## 5. Prompt-by-prompt verification summary (Final Gate)

| Prompt | Focus | Result |
| --- | --- | --- |
| 1 | Architecture baseline & freeze | **PASS** — doc produced; no prod change |
| 2 | Management → membership path | **PASS** — covered by TG.5/5A + TG.6 membership tests |
| 3 | Compatible TG query | **PASS** — `CompatibleTeachingGroupQueryServiceTests` + P2A guards |
| 4 | Assign / clear | **PASS** — `TeachingGroupApplicationBoundaryTests` + UI actions |
| 5 | TimetableSection projection | **PASS** — projector / TG.4A e2e unit tests |
| 6 | Legacy `/sections` bridge | **PASS** — bridge via `ReplaceSectionsAndProjectAsync` |
| 7 | Attendance regression | **PASS** (architecture guards + no Attendance edits this gate); dedicated Attendance suite not re-run in full — see matrix |
| 8 | Designer dialog/grid sync | **PASS** — Prompt 4 grid + dialog sync code + UI tests |
| 9 | 409 conflict | **PASS** — assignment actions reload; no auto-retry |
| 10 | Designer operation integrity | **PASS** — Create/Upsert omit TG; no client inference |
| 11 | Capacity warning | **PASS** — dialog + grid use TG capacity fields, not Room.Capacity |
| 12 | Tenant isolation | **PASS** — unit/guard coverage; no IgnoreQueryFilters |
| 13 | RBAC | **PASS** — dedicated TG + timetable permissions; server authoritative |
| 14 | Final architecture guards | **PASS** — `AiSchedTg6FinalArchitectureGuardTests` |
| 15 | Persistence integrity | **PASS** (unit/in-memory + prior PG concurrency); destructive DB audit **Not Executed** |
| 16 | Full regression matrix | See §5.1 |
| 17 | Live browser E2E | **NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE** (no running app session in this gate) |
| 18 | Legacy academic data regression | **PASS** by non-modification + architecture freeze (no Attendance/StudentSection redesign) |
| 19 | Performance / N+1 | **PASS with follow-up** — grid `getTeachingGroup(id)` enrichment may N+1; document only |
| 20 | Final acceptance doc | This document |

### Known limitation (not a Final-Gate redesign)

Assign/Clear (TG.4 Prompt 3 freeze) set/clear `TeachingGroupId` only. Projection for an entry after assign occurs when TeachingGroupSection `*AndProject` runs (or unused `SyncTeachingGroupSectionsToTimetableEntryAsync`). Documented in Prompt 1 baseline — **follow-up**, not silent architecture change in this gate.

### Defects corrected in this gate

Stale “feature not yet built” discovery/guard tests from TG.5 Prompt 1/2/4/5/5A and TG.6 Prompt 2/4 discovery that conflicted with later approved deliveries were **superseded** to assert the frozen post-delivery architecture. No production behavior changed.

---

## 5.1 Test evidence (exact)

### Backend — Application.UnitTests (executed this gate)

| Suite filter | Passed | Failed | Skipped | Not Executed |
| --- | ---: | ---: | ---: | ---: |
| `AiSchedTg*` / `TeachingGroup*` / `CompatibleTeachingGroup*` / `TimetableSection*` / `LegacyTimetable*` | **314** | **0** | **0** | — |

Includes new `AiSchedTg6FinalArchitectureGuardTests`.

### Backend — IntegrationTests (this gate)

| Suite | Passed | Failed | Skipped | Not Executed |
| --- | ---: | ---: | ---: | ---: |
| TeachingGroupMembershipConcurrency (PostgreSQL) | — | — | — | **Not Executed this gate** (previously PASS in TG.5A.1 sessions) |

### Frontend — Vitest scheduling (executed this gate)

| Suite | Passed | Failed | Skipped | Not Executed |
| --- | ---: | ---: | ---: | ---: |
| `src/pages/setup/scheduling/**` | **71** | **0** | **0** | — |
| `AiSchedTg6FinalArchitectureGuardTests` | **7** | **0** | **0** | — |

### Builds

| Build | Result |
| --- | --- |
| API (`Abhyanvaya.API`) | **PASS** (this gate) |
| UI typecheck/production build | **PASS** in prior TG.6 Prompt 4 session; re-confirm if needed after guard-only edits |

### Browser E2E

| Flow | Result |
| --- | --- |
| Login → TG management → Timetable assign/clear/grid | **NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE** |

Credentials were provided but no live API/UI process was available to this gate run; results are **not fabricated**.

---

## 6. Browser E2E classification

**NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE**

---

## 7. Performance note (Prompt 19)

Timetable designer display enrichment may call `getTeachingGroup(id)` for assigned ids missing from the hint map after grid load. Compatible TG options already carry `ResolvedStudentCount` from the server. This is documented as an optional follow-up optimization — **not** expanded in this gate.

---

## 8. Architectural freeze statement

AI-SCHED-TG.2 → TG.6 architecture is **frozen** for production behavior:

- TeachingGroupSection SoT  
- TimetableSectionProjector sole writer  
- Explicit TimetableEntry.TeachingGroupId assignment  
- Server-authoritative compatible query + membership resolution  
- No SA→TG inference / auto TG create / UI TimetableSection writes  

Further work requires an explicit new workstream.

---

## 9. Final gate verdict

| Criterion | Verdict |
| --- | --- |
| Architecture inventory | PASS |
| Single SoT | PASS |
| Compatible / assign / clear boundaries | PASS |
| Projection / legacy bridge | PASS |
| UI designer + 409 + capacity | PASS |
| Architecture guards consolidated | PASS |
| Stale discovery tests reconciled | PASS |
| Live browser E2E | NOT EXECUTED |
| Overall Final Integration | **CONDITIONAL PASS** — automated architecture + unit/UI gates PASS; live browser E2E not executed |

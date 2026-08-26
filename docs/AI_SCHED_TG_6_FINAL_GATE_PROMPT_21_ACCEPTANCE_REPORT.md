# AI-SCHED-TG.6 Final Gate Prompt 21 — Acceptance Report

**Date:** 2026-08-20  
**Companion:** `docs/AI_SCHED_TG_6_FINAL_GATE_PROMPT_21_ASSIGNMENT_PROJECTION_CONSISTENCY.md`

---

## J. Final verdict

# FINAL PASS — ARCHITECTURALLY FROZEN

---

## A. Existing-flow audit

See consistency document §A. Pre-correction assign/clear set `TeachingGroupId` only (TG.4 P3 freeze) and left TimetableSection stale when SoT sections already existed.

## B. Projection consistency decision

**CORRECTED — minimum change implemented**

- Assign → `SyncTeachingGroupSectionsToTimetableEntryAsync` → single SaveChanges  
- Clear → `ClearTimetableEntryProjectionAsync` (soft-delete entry projection) → null FK → single SaveChanges  
- Projector remains sole writer; no SaveChanges inside projector  
- TeachingGroupSection SoT unchanged on clear  

## C. Attendance verification

| Scenario | Result |
| --- | --- |
| A — TG with sections → projected TimetableSection | **PASS** (unit) |
| B — TG section replacement → obsolete soft-deleted, current projected | **PASS** (unit + shared TG) |
| C — Multiple sections | **PASS** (unit) |
| D — No TG / after clear → no active projection; legacy fallback architecture intact | **PASS** (unit + resolver source guard) |
| E — Projection drift / GET must not create TG; Attendance does not mutate Scheduling | **PASS** (resolver source guards) |

## D. PostgreSQL audit (read-only)

| Check | Result |
| --- | --- |
| Cross-tenant TimetableEntry↔TeachingGroup | **0 violations** |
| Orphan TeachingGroupId | **0 violations** |
| Duplicate TeachingGroupSection | **0 violations** |
| Duplicate active TimetableSection | **0 violations** |
| TeachingGroupSection tenant vs TG | **0 violations** |

Suite: `AiSchedTg6FinalGatePrompt21PostgreSqlIntegrityAuditTests` (non-destructive SELECT).

## E. Security

| Check | Result |
| --- | --- |
| Cross-tenant assign rejected | **PASS** (unit) |
| No IgnoreQueryFilters on TG Scheduling paths | **PASS** (architecture guards) |
| Lifecycle Draft-only assign/clear preserved | **PASS** (EnsureDraft) |
| RBAC unchanged (server authoritative) | **PASS** (no weakening) |

## F. Concurrency

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| `TeachingGroupMembershipConcurrency` (PostgreSQL) | **7** | **0** | **0** |

Assign/clear use existing `ConcurrencyExceptionHelper` / 409 mapping (TG.5A). No second error-mapping mechanism.

## G. Architecture guards

| Suite | Passed | Failed |
| --- | ---: | ---: |
| `AiSchedTg6FinalArchitectureGuardTests` | **7** | **0** |
| `AiSchedTg4APrompt8ArchitectureGuardTests` | included in TG filter | **0** |
| Updated TG.4 application architecture guards | **PASS** | |

Sole writer: `TimetableSectionProjector` only.

## H. Regression

| Suite | Passed | Failed | Skipped | Not Executed |
| --- | ---: | ---: | ---: | ---: |
| Backend TG filter (`AiSchedTg*` / `TeachingGroup*` / `CompatibleTeachingGroup*` / `TimetableSection*` / `LegacyTimetable*` / `TimetableEntryTeachingGroup*`) | **321** | **0** | **0** | — |
| Prompt 21 assignment projection unit tests | **7** (in filter) | **0** | **0** | — |
| PostgreSQL membership concurrency | **7** | **0** | **0** | — |
| PostgreSQL integrity audit | **1** | **0** | **0** | — |
| Frontend scheduling Vitest | **71** | **0** | **0** | — |
| Full Attendance suite (entire Academic Attendance) | — | — | — | **Not Executed** (focused TimetableSection/Attendance resolver verification PASS) |
| API build | **PASS** | | | |
| UI production build | — | — | — | **Not Executed this session** (Vitest + prior TG.6 builds PASS) |

## I. Browser E2E

**NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE**

## Architecture freeze confirmation

- TeachingGroupSection remains SoT  
- TimetableSection remains projection-only  
- TimetableSectionProjector remains sole writer  
- No SA→TG inference / auto TG create  
- No Attendance / StudentSection schema change  
- No Create/Update/Upsert TeachingGroupId  
- No IgnoreQueryFilters bypass  
- No hosted reconciler / permanent backfill  

**AI-SCHED-TG.6 is FINAL PASS — ARCHITECTURALLY FROZEN.**

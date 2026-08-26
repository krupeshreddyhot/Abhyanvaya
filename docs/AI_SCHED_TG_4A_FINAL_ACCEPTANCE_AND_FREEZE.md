# AI-SCHED-TG.4A — Final Acceptance and Architectural Freeze (Prompt 10)

**Workstream:** AI-SCHED-TG.4A — Legacy TimetableSection Bridge & TeachingGroup Projection  
**Prompt:** 10 — Architectural Freeze  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.4A Prompt 9 (PASS — E2E acceptance)

**Type:** Freeze / documentation / guardrails **only** — no new production behavior.

**STATUS = FULL PASS — FROZEN**

---

## 1. Frozen end-state architecture

```text
                    SubjectAllocation
                           │
                           ▼
                    ┌──────────────┐
                    │ TeachingGroup│
                    └──────┬───────┘
                           │
                           ▼
                 ┌───────────────────┐
                 │TeachingGroupSection│
                 │  SOURCE OF TRUTH  │
                 └─────────┬─────────┘
                           │
                     projection
                           │
                           ▼
                 ┌───────────────────┐
                 │ TimetableSection  │
                 │    PROJECTION     │
                 └─────────┬─────────┘
                           │
                           ▼
                    TimetableEntry
                           │
                           ▼
                       Attendance
```

**Academic Section** = enrollment / grouping construct.  
**TeachingGroup** = scheduling construct.  
They must not be conflated.

---

## 2. Explicit freeze locks

| # | Lock | Status |
|---|---|---|
| F1 | `TeachingGroupSection` is the **sole** section-membership source of truth | **LOCKED** |
| F2 | `TimetableSection` is **projection-only** | **LOCKED** |
| F3 | `TimetableSectionProjector` is the **only** writer of `TimetableSection` entities | **LOCKED** |
| F4 | No automatic TeachingGroup creation (GET/startup/Attendance/SA lookup) | **LOCKED** |
| F5 | No SubjectAllocation → TeachingGroup inference | **LOCKED** |
| F6 | No Attendance schema changes from this workstream | **LOCKED** |
| F7 | No UI redesign from this workstream | **LOCKED** |
| F8 | No permanent legacy timetable backfill / hosted reconciler | **LOCKED** |
| F9 | Legacy `PUT/GET /api/timetable/{id}/sections` external contract remains compatible | **LOCKED** |
| F10 | Clone / schedule-version must **not** become direct TimetableSection writers | **LOCKED** |
| F11 | Architecture Guards prevent future SoT / projection violations | **LOCKED** |
| F12 | Any future UI must use existing application boundaries (no DbSet / inference bypass) | **LOCKED** |

---

## 3. Prompt 10 confirmation checklist (18 gates)

| # | Confirmation | Result | Evidence |
|---|---|---|---|
| 1 | TeachingGroupSection is SoT | **PASS** | P3–P5 services + P8 guards |
| 2 | TimetableSection is projection | **PASS** | Projector + P8 sole-writer scan |
| 3 | Legacy `/sections` API compatible | **PASS** | P5 bridge + P9 S14 |
| 4 | No application TimetableSection bypass | **PASS** | P8 controller/app scans |
| 5 | No automatic TeachingGroup creation | **PASS** | P5–P9 reject/create guards |
| 6 | No SA → TG inference | **PASS** | TG / conversion / bridge guards |
| 7 | Combined TG supports multiple Sections | **PASS** | P9 S2 / projector multi-section |
| 8 | TG without Sections remains valid | **PASS** | P9 S3 / Custom type zero sections |
| 9 | Tenant isolation intact | **PASS** | P9 S6 + no IgnoreQueryFilters on TG ops |
| 10 | Existing RBAC intact | **PASS** | P9 S13–S14 |
| 11 | Attendance unchanged | **PASS** | Resolver still reads TimetableSections |
| 12 | Legacy Attendance fallback intact | **PASS** | P9 S10 |
| 13 | TimetableEntry.TeachingGroupId invariant | **PASS** | TG.4 mutation invariants + P5 require TG |
| 14 | Clone/version correct | **PASS** | CloneEntry keeps TG id; no TimetableSection writes |
| 15 | UI not redesigned | **PASS** | `sectionService.ts` contract unchanged |
| 16 | No permanent timetable backfill | **PASS** | P7 disposable conversion only (scoped) |
| 17 | No production academic data changed | **PASS** | No master-data migrations in 4A |
| 18 | All required tests pass | **PASS** | P9 gates + P10 freeze guards |

Live production browser E2E remains **DATA UNAVAILABLE** (honest; not invented PASS) — same as Prompt 9.

---

## 4. Approved mutation paths (frozen)

```text
Legacy PUT /sections
  → SectionManagementService.SetTimetableSectionsAsync
  → require TimetableEntry.TeachingGroupId
  → ITeachingGroupSectionApplicationService.ReplaceSectionsAndProjectAsync
  → TeachingGroupSection SoT
  → TimetableSectionProjector
  → TimetableSection
  → single SaveChanges (boundary)

Explicit TG assign
  → ITeachingGroupApplicationService
  → TimetableEntry.TeachingGroupId only

Disposable conversion (dev/admin, explicit only)
  → ILegacyTimetableTeachingGroupConversionService
  → never hosted / never GET / never startup
```

---

## 5. FORBIDDEN after freeze

- Direct `new TimetableSection` outside `TimetableSectionProjector`
- Controller / TimetableService / Clone / Version writing TimetableSection
- Auto-create TeachingGroup on read paths
- Infer TeachingGroup from SubjectAllocation
- Permanent EF backfill migration for legacy timetable sections
- Attendance or StudentSection mutation from scheduling projection code
- Future UI inventing TeachingGroup ids or skipping application services

Guard suites that enforce this:

- `AiSchedTg4APrompt8ArchitectureGuardTests`
- `AiSchedTg4APrompt10ArchitecturalFreezeTests`
- Complementary TG.4 / TG.4A architecture guards (P3–P7)

---

## 6. Prompt 10 production delta

**None.** Documentation + freeze guardrail tests only.

---

## 7. Verdict

**STATUS = FULL PASS — FROZEN**

AI-SCHED-TG.4A is architecturally frozen on the SoT / projection model above.

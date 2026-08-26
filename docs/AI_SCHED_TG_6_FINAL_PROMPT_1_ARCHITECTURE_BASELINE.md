# AI-SCHED-TG.6 Final Prompt 1 — Architecture Baseline & Change Freeze

**Date:** 2026-08-20  
**Scope:** Inventory only — no production behavior changes in this prompt.  
**Rule:** Change freeze unless a verified architecture-contract violation is found.

---

## 1. Milestone inventory (docs + code present)

| Milestone | Primary evidence | Status in tree |
| --- | --- | --- |
| AI-SCHED-TG.2 | `docs/AI_SCHED_TG_2_*` (decision, schema, membership, API, timetable, governance) | Documented architecture — PASS (baseline) |
| AI-SCHED-TG.2A | `docs/AI_SCHED_TG_2A_*` (capacity/membership, TimetableSection SoT clarification → later superseded by TG.4A TeachingGroupSection SoT) | Documented — PASS (historical) |
| AI-SCHED-TG.3 | Domain + EF (`TeachingGroupDomainTests`, `TeachingGroupEfModelIntegrityTests`) | Implemented — PASS |
| AI-SCHED-TG.4 | `TimetableEntry.TeachingGroupId`, `TeachingGroupApplicationService` assign/clear | Implemented — PASS |
| AI-SCHED-TG.4A | `TeachingGroupSection` SoT, `TimetableSectionProjector`, legacy `/sections` bridge | Implemented — PASS |
| AI-SCHED-TG.5 | Management API + UI foundation | Implemented — PASS |
| AI-SCHED-TG.5A / 5A.1 / 5A.1A | Membership integrity, concurrency, persistence mapping | Implemented — PASS |
| AI-SCHED-TG.6 Prompts 1–4 | UI contracts, membership UX, compatible query, dialog/grid TG state | Implemented — PASS |

---

## 2. Frozen architecture decisions (verified present)

### 2.1 Teaching Group → Section → Projection chain

```text
TeachingGroup
    │
    └── TeachingGroupSection          ← sole TG→Section membership SoT
              │
              ▼
    TimetableSectionProjector         ← sole TimetableSection writer
              │
              ▼
       TimetableSection               ← projection-only
```

**Evidence**

- `TeachingGroupSectionApplicationService` → `*AndProjectAsync` → `ITimetableSectionProjector`
- `TimetableSectionProjector` is the only Application production type that constructs `new TimetableSection`
- Controllers / `TimetableService` / clone / version paths do not write `TimetableSection` (TG.4A Prompt 8 guards)

### 2.2 Timetable entry relationship

```text
TimetableEntry.TeachingGroupId
              │
              ▼
        TeachingGroup
```

**Evidence:** domain FK + `TeachingGroupApplicationService.AssignToTimetableEntryAsync` / `ClearFromTimetableEntryAsync`; Create/Update/Upsert DTOs omit `TeachingGroupId`.

### 2.3 Membership

```text
TeachingGroup
      ↓
Membership Resolver (server)
      ↓
ResolvedStudentCount / Resolved Members
```

**Evidence:** `TeachingGroupMembershipResolver`; management/membership/compatible APIs expose resolved counts from server; UI does not compute membership.

### 2.4 UI boundaries

```text
Teaching Group Management  →  Teaching Group APIs
Timetable Designer         →  Compatible TG Query → Assign / Clear API
```

**Evidence**

- `TeachingGroupsPage` + `teachingGroupService.ts`
- `timetableTeachingGroupAssignmentActions.ts` + dedicated scheduling endpoints
- No SA→TG inference; no TG auto-create; no UI TimetableSection writes

### 2.5 Second source of truth

**Confirmed: none.**

- Membership SoT for TG↔Section: `TeachingGroupSection` only
- Assignment SoT for entry↔TG: `TimetableEntry.TeachingGroupId` only
- `TimetableSection` is derived projection, not an alternate membership editor for TG mode

---

## 3. Security / isolation invariants (spot-check)

| Rule | Result |
| --- | --- |
| No `IgnoreQueryFilters()` in Application Scheduling TG production services | PASS |
| Dedicated assign/clear boundary | PASS |
| Create/Update/Upsert omit `TeachingGroupId` (C# + TS) | PASS |
| Legacy `PUT /api/timetable/{id}/sections` → `ReplaceSectionsAndProjectAsync` | PASS |
| Attendance / StudentSection not mutated by TG assign/clear | PASS (by design; not redesigned) |

---

## 4. Known gap (documented — not treated as silent redesign in Prompt 1)

| Gap | Detail | Classification |
| --- | --- | --- |
| Assign/Clear does not invoke `TimetableSectionProjector` | TG.4 Prompt 3 freeze defined assign/clear as **FK-only**. Projection runs on TeachingGroupSection `*AndProject` mutations (and `SyncTeachingGroupSectionsToTimetableEntryAsync` exists but is unused in production). Ordering: assign after sections already linked does not auto-project that entry until a section AndProject or an explicit entry sync. | **Known limitation / follow-up** under TG.4 freeze — **not** a Prompt-1 production change. Final Prompt 5/7 will verify whether acceptance requires a minimal projector call on assign/clear. |

No second SoT was introduced. No Prompt-1 production edits.

---

## 5. Change freeze statement

Until a later Final Gate prompt proves a contract violation with a failing gate:

- Do **not** redesign Teaching Group architecture
- Do **not** weaken validation
- Do **not** add `TeachingGroupId` to Create/Update/Upsert
- Do **not** redesign Attendance / StudentSection
- Corrective changes must be minimal + regression-tested

---

## 6. Prompt 1 outcome

| Item | Result |
| --- | --- |
| Architecture inventory complete | PASS |
| Single SoT confirmed | PASS |
| Production code changed | **None** |
| Proceed to Prompt 2+ verification | YES |

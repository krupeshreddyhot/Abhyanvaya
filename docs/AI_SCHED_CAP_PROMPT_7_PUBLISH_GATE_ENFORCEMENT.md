# AI-SCHED-CAP Prompt 7 — Publish Gate Enforcement & Transactional Safety

**Workstream:** AI-SCHED-CAP  
**Prompt:** 7 — Publish Gate Enforcement  
**Date:** 2026-08-20  
**Baseline:** Prompt 5 contract + Prompt 6 `ITimetablePublishReadinessService`  
**Status:** Implementation (gate only — **no UI**)

---

## Existing publish flow (preserved)

```text
POST api/scheduling/timetables/{id}/publish
        │
        ▼
CanPublishScheduling (unchanged)
        │
        ▼
TimetableLifecycleService.PublishAsync
        │
        ├── Load timetable (tenant-scoped)
        ├── Existing lifecycle DomainException checks
        │     • Frozen
        │     • Locked OR approved ScheduleVersion
        │     • Published scope uniqueness
        │
        ├── ★ Prompt 7 gate ★
        │     ITimetablePublishReadinessService.EvaluatePublishReadinessAsync
        │           ├── IsReady = false → PublishNotReadyException (structured findings)
        │           │                      NO status change / NO SaveChanges / NO history
        │           └── IsReady = true  → continue
        │
        ├── Status → Published (+ ScheduleVersion publish fields)
        ├── SaveChanges (existing concurrency helper)
        └── Change history RecordAsync(Publish)
```

Publish readiness is evaluated **server-side immediately before** the existing publish mutation, and a blocked readiness result **prevents the publish mutation from occurring**.

---

## Dependency

`PublishAsync` injects and consumes **only**:

- `ITimetablePublishReadinessService` (Prompt 6)

It does **not** duplicate:

- ConflictEngine / capacity rules / PlacementSize / RoomCapacity / TG capacity / blocker classification

---

## Blocker behavior

Authoritative classification remains Prompt 5 / Prompt 6:

| Finding | Blocks publish |
| --- | --- |
| Critical integrity | Yes |
| `ROOM_CAPACITY` | Yes |
| `TEACHING_GROUP_CAPACITY_EXCEEDED` | Yes |
| Lifecycle readiness codes from Prompt 6 | Yes (when gate reached) |
| Warning | No |
| Non-capacity Error | No |

Existing lifecycle `DomainException` messages are preserved for Frozen / NotEligible / ScopeConflict (thrown **before** the readiness call).

Archived timetables that pass those legacy checks are blocked by readiness (`LIFECYCLE_ARCHIVED`) with structured findings.

---

## HTTP / business error mapping

| Condition | Response |
| --- | --- |
| Lifecycle DomainException | `400 BadRequest` + message string (unchanged) |
| Publish readiness blocked | `400 BadRequest` + **`TimetablePublishReadinessResultDto` body** (`PublishNotReadyException`) |
| Missing / cross-tenant | `404` |
| Uncaught `PublishNotReadyException` | Global handler → ProblemDetails type `publish-not-ready` with `publishReadiness` extension |

Route and auth for publish are unchanged. Clients are **not** required to call GET `publish-readiness` first; the server re-evaluates on publish.

GET `publish-readiness` remains read-only preflight.

---

## Transaction boundary

```text
Readiness BLOCK → return rejection → NO SaveChanges caused by publish
```

Verified by tests: blocked publish never calls `IUnitOfWork.SaveChangesAsync` and never records publish history.

Successful publish retains existing SaveChanges + history behavior.

---

## Concurrency / TOCTOU

- Readiness evaluation is **not** a database lock.
- Existing `ConcurrencyExceptionHelper.SaveChangesAsync` remains authoritative for persistence races.
- Scenario: User A evaluates readiness → User B mutates → User A publishes: publish re-evaluates readiness at gate time; SaveChanges concurrency still applies.
- No broad locking invented in Prompt 7.

---

## TG / TimetableSection preservation

Publish gate evaluates capacity via readiness only. Publishing does **not**:

- infer / create / assign / clear TeachingGroup
- mutate TeachingGroupSection / membership / Attendance / StudentSection
- construct or write TimetableSection (projector remains sole writer)

---

## Tests

- `AiSchedCapPrompt7PublishGateTests` — clean publish, Critical / ROOM / TG blockers, warning & non-capacity Error, lifecycle contracts, no-mutation assertions, tenant miss
- `AiSchedCapPrompt7ArchitectureGuardTests` — gate ordering, no duplicate engines, no TG/Attendance/TimetableSection writes, API structured rejection

---

## Deferred

- Prompt 8: Publish Readiness UI  
- Prompt 9: Final E2E acceptance  
- Hard Draft mutation rejection / DnD blocking  

---

## Limitations

- Lifecycle DomainExceptions (Frozen / NotEligible / Scope) remain string `BadRequest` (pre-existing contract), not readiness DTO payloads.
- TOCTOU between readiness and SaveChanges relies on existing concurrency — no serializable lock added.

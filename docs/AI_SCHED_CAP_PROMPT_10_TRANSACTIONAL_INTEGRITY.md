# AI-SCHED-CAP Prompt 10 — Cross-Layer Consistency & Transactional Integrity Gate

**Workstream:** AI-SCHED-CAP  
**Prompt:** 10 — Cross-Layer Consistency & Transactional Integrity  
**Date:** 2026-08-20  
**Baseline:** Prompt 9 Final Integration Acceptance (PASS)  
**Type:** Consistency / transactional integrity hardening — **no architecture redesign**  
**Final recommendation: PASS**

---

## 1. Authoritative rules (unchanged)

| Rule | Owner |
| --- | --- |
| `TeachingGroupSection` = section membership SoT | TG.4A |
| `TimetableSection` = projection | TG.4A |
| `TimetableSectionProjector` = sole projection writer (no `SaveChanges`) | TG.4A / CAP |
| `PublishReadinessService` = read-only readiness evaluation | CAP Prompt 6 |
| `PublishAsync` = authoritative publish mutation + gate | CAP Prompt 7 |

No Attendance schema changes. No StudentSection mutation from Scheduling. No client-side readiness decision engine.

---

## 2. Required flows (verified)

### 2.1 Teaching Group assign (single transaction)

```text
Stage TimetableEntry.TeachingGroupId
        ↓
Sync TeachingGroupSection SoT → TimetableSection (projector; no commit)
        ↓
ConcurrencyExceptionHelper.SaveChangesAsync  (exactly once)
```

Verified in `TeachingGroupApplicationService.AssignToTimetableEntryAsync` + Prompt 10 tests/guards.

### 2.2 Teaching Group clear (single transaction)

```text
ClearTimetableEntryProjectionAsync  (projector; no commit)
        ↓
Stage TeachingGroupId = null
        ↓
ConcurrencyExceptionHelper.SaveChangesAsync  (exactly once)
```

Verified in `ClearFromTimetableEntryAsync` + Prompt 10 tests/guards.

### 2.3 Publish gate (no partial publish)

```text
EvaluatePublishReadinessAsync
        ↓
if !IsReady → PublishNotReadyException  (NO status mutation, NO SaveChanges)
        ↓
if IsReady  → Status = Published + SaveChanges once
```

Preserved from Prompt 7; re-asserted by Prompt 10.

### 2.4 Publish readiness GET

`GET …/publish-readiness` → `EvaluatePublishReadinessAsync` only.  
No UoW, no TG create/infer, no section repair, no Attendance / StudentSection writes.

---

## 3. Production behavior changed

| Change | Why |
| --- | --- |
| `ConcurrencyConflictException.ForSchedulingModule()` | Established conflict message for scheduling entities |
| `ConcurrencyExceptionHelper.ClassifyConcurrencyConflict` | Maps `Timetable` / `TimetableEntry` / `TimetableSection` / `TeachingGroup` / `TeachingGroupSection` / `TeachingGroupMembership` / `ScheduleVersion` / `SubjectAllocation` / `Room` concurrency to `ForSchedulingModule()` instead of the attendance-module default |

Assign / clear / publish / readiness paths were already transactionally correct; they were **not** redesigned.

**Schema:** No migrations. Prefer existing schema (no new RowVersion columns in this prompt).

---

## 4. Concurrency / conflict mapping

| Signal | Mapping |
| --- | --- |
| EF `DbUpdateConcurrencyException` on scheduling entities | `ConcurrencyConflictException` (`ForSchedulingModule`) via `ConcurrencyExceptionHelper` |
| PostgreSQL unique_violation on approved membership index | `ConcurrencyConflictException` via `TeachingGroupMembershipPersistenceExceptionMapper` (TG.5) |
| Cross-tenant TG assign | `KeyNotFoundException` / zero mutation |
| Non-Draft TG assign | Lifecycle `DomainException` / zero mutation |

**Residual (documented, no schema change):** Timetable / TimetableEntry entities do not currently declare optimistic concurrency tokens. Silent last-write-wins at the row level remains a platform-wide scheduling residual; when PostgreSQL/EF concurrency exceptions **do** occur on scheduling entities, they now map to the established scheduling conflict response. Membership uniqueness already prevents silent duplicate current-membership overwrite.

---

## 5. Tests added

| Suite | Coverage |
| --- | --- |
| `AiSchedCapPrompt10TransactionalIntegrityTests` | Assign/clear atomicity (one SaveChanges; failed commit leaves no durable partial state); projector SaveChanges prohibition; publish blocked → zero mutation; publish ready → one SaveChanges; readiness GET / service zero mutation; no repair in readiness/analyzer; membership unique mapping; scheduling concurrency classifier; tenant isolation; lifecycle rejection |
| `AiSchedCapPrompt10ArchitectureGuardTests` | Assign/clear source order; sole projector writer; publish gate order; concurrency classifier presence; GET non-mutation; documentation |

Existing Prompt 6/7/9 and TG.4A–TG.6 guards remain in force and were not weakened.

---

## 6. Explicit non-goals

- Architecture redesign / second ConflictEngine / capacity stack
- Client-side publish decision logic
- Attendance / StudentSection mutation from Scheduling
- Schema / migration for RowVersion (deferred unless product requires)
- UI changes

---

## 7. Evidence checklist

| Item | Result |
| --- | --- |
| Files changed | Domain concurrency factory; `ConcurrencyExceptionHelper`; Prompt 10 tests; deliverable doc |
| Production behavior changed | Scheduling EF concurrency → `ForSchedulingModule()` message (assign/clear/publish flows unchanged) |
| Tests added | `AiSchedCapPrompt10TransactionalIntegrityTests` + `AiSchedCapPrompt10ArchitectureGuardTests` (**20** new) |
| Regression CAP+TG+TeachingGroup | **463 Passed** |
| Scheduling filter | **351 Passed** |
| Architecture / CAP guards | **264 Passed** |
| Prompt 10 suite | **20 Passed** |
| API build | **0 errors** |
| UI build | **PASS** (`tsc -b && vite build`) |
| Migration status | **None** |
| Final | **PASS** |

---

## 8. Stop

Prompt 10 is the terminal corrective gate after Prompt 9. **STOP** after this prompt.

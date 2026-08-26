# AI-SCHED-CAP Prompt 6 — Publish Readiness Application Service & Read-Only API

**Workstream:** AI-SCHED-CAP  
**Prompt:** 6 — Publish Readiness Application Service  
**Date:** 2026-08-20  
**Baseline:** Prompt 5 Publish Readiness Contract  
**Status:** Implementation (read-only — **no PublishAsync gate**)

---

## Architecture boundary

```text
GET /api/scheduling/timetables/{id}/publish-readiness
        │
        ▼
ITimetablePublishReadinessService
TimetablePublishReadinessService
        │
        ├── Lifecycle preconditions (read-only mirror of PublishAsync)
        ├── IConflictAnalysisRunner → ConflictAnalyzer → ConflictEngine
        ├── Capacity metrics via ConflictAnalysisContext
        │     (IPlacementSizeResolver / IRoomCapacityEvaluator already in context)
        └── Deterministic ordering + Level-3 classification
```

**Does not** call `ConflictDetectionService` (avoids SaveRun persistence).  
**Does not** mutate Timetable / TG / TimetableSection / Attendance / ScheduleVersion.

---

## Service contract

`ITimetablePublishReadinessService.EvaluatePublishReadinessAsync(timetableId)`

Returns `TimetablePublishReadinessResultDto`:

- `IsReady`, `LifecycleState`, `IsFrozen`, counts, `Findings[]`

Finding: `PublishReadinessFindingDto` with code, severity, `IsBlocking`, title/why/action, optional capacity/TG metrics.

---

## API

`GET api/scheduling/timetables/{timetableId}/publish-readiness`  
Auth: `CanViewSchedulingTimetable`  
404 when timetable missing for tenant.

---

## Blocker classification (Prompt 5)

Blocking when:

1. `ConflictSeverity.Critical`, or  
2. Code `ROOM_CAPACITY` / `TEACHING_GROUP_CAPACITY_EXCEEDED`, or  
3. Lifecycle precondition findings (`LIFECYCLE_*`)

Non-capacity Errors and Warnings are non-blocking.

---

## Lifecycle reuse

Read-only mirror of `TimetableLifecycleService.PublishAsync` checks:

| Code | Condition |
| --- | --- |
| `LIFECYCLE_FROZEN` | `IsFrozen` |
| `LIFECYCLE_NOT_ELIGIBLE` | Not Locked and ScheduleVersion not Approved |
| `LIFECYCLE_ARCHIVED` | Status Archived |
| `LIFECYCLE_PUBLISHED_SCOPE_CONFLICT` | Another Published non-frozen TT in AY+Dept |

`PublishAsync` itself is **unchanged** (Prompt 7 enforces gate).

---

## Tenant / security

- `ITimetableRepository.GetByIdAsync(TenantId, …)`  
- Scope conflict query filtered by `TenantId`  
- No `IgnoreQueryFilters`  
- View permission only for readiness inspection

---

## Deterministic ordering

`blocking → severity → code → entry → day → slot → room → teachingGroup`

---

## Performance

Reuses ConflictAnalyzer batching (distinct TGs, masters). No speculative cache.  
Does not persist readiness results.

---

## Deferred

- Prompt 8: Publish Readiness UI  
- Prompt 9: Final E2E acceptance  
- Hard Draft mutation rejection  

**Prompt 7 note:** `PublishAsync` now consumes this service as the publish gate (see `AI_SCHED_CAP_PROMPT_7_PUBLISH_GATE_ENFORCEMENT.md`). This document remains the read-only service contract.

---

## Tests / guards

- `AiSchedCapPrompt6PublishReadinessServiceTests`  
- `AiSchedCapPrompt6ArchitectureGuardTests`  

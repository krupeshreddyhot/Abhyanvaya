# AI-SCHED-TG.6 Final Gate Prompt 21 — Assignment Projection Consistency

**Date:** 2026-08-20  
**Workstream:** AI-SCHED-TG.6 Final Gate — Prompt 21  
**Status:** CORRECTED — minimum projection orchestration on assign/clear

---

## A. Existing-flow audit (pre-correction)

### PUT assign

```text
TimetableControllers.AssignTeachingGroup
   ↓
ITeachingGroupApplicationService.AssignToTimetableEntryAsync
   ↓
EnsureDraft + Load TG (tenant filters) + EnsureCompatibleWithTimetableEntry
   ↓
TimetableEntry.TeachingGroupId = TG
   ↓
SaveChanges once
   ↓
(no TimetableSectionProjector call — STALE GAP)
```

### DELETE clear

```text
TimetableControllers.ClearTeachingGroup
   ↓
ClearFromTimetableEntryAsync
   ↓
EnsureDraft
   ↓
TimetableEntry.TeachingGroupId = null
   ↓
SaveChanges once
   ↓
(no projection clear — STALE GAP)
```

### Critical scenario (Prompt 21 §5)

Initial: TG-01 → Sections 5,6; Entry E-100 TeachingGroupId = null  
Assign E-100 → TG-01  

**Before correction:** TeachingGroupId set; TimetableSection for E-100 remained empty → Attendance Timetable mode would not see Sections 5/6 until a later `*AndProject` section mutation.

**After correction:** TeachingGroupId set + entry projection staged via existing `SyncTeachingGroupSectionsToTimetableEntryAsync` + single SaveChanges → TimetableSection E-100 → 5,6.

---

## B. Projection consistency decision

**CORRECTED — minimum change implemented**

### Reason

TG.4 Prompt 3 froze assign/clear as FK operations. TG.4A delivered `ITimetableSectionProjector.SyncTeachingGroupSectionsToTimetableEntryAsync` for entry-scoped sync but it was unused in production assign/clear. Final Gate Prompt 21 requires synchronous consistency so Attendance never consumes TeachingGroupId without matching TimetableSection when SoT sections exist.

### Clear semantics (frozen TG.4A interpretation)

When TeachingGroupId is cleared:

- **TeachingGroupSection** SoT is **not** mutated.
- **Other entries** sharing the TG keep their projections.
- **This entry’s** TG-derived TimetableSection rows are **soft-deleted** (projection no longer applicable).
- Legacy Attendance fallback remains available for entries with TeachingGroupId = null.

### Post-correction flow

```text
Validate
  → Stage TeachingGroupId mutation
  → Stage projection via ITimetableSectionProjector
       (SyncTeachingGroupSectionsToTimetableEntryAsync | ClearTimetableEntryProjectionAsync)
  → Single SaveChanges (ConcurrencyExceptionHelper)
```

Projector remains persistence-agnostic (no SaveChanges).

Sole TimetableSection writer remains TimetableSectionProjector (`new TimetableSection` only there).

---

## C. Code changes (minimal)

| File | Change |
| --- | --- |
| `ITimetableSectionProjector` | + `ClearTimetableEntryProjectionAsync` |
| `TimetableSectionProjector` | Soft-delete all rows for one entry via empty `ProjectEntryAsync` |
| `TeachingGroupApplicationService` | Inject projector; assign → sync entry; clear → clear projection; one commit |

No new APIs, tables, UI, Attendance schema, hosted services, or IgnoreQueryFilters.

---

## D. Tests added

`AiSchedTg6FinalGatePrompt21AssignmentProjectionTests`:

- Assign projects SoT sections onto entry
- Clear soft-deletes entry projection; SoT intact
- Shared TG section replace projects all bound entries
- Idempotent assign/sync
- Cross-tenant assign rejected; no projection
- Attendance read path consumes projected sections; resolver does not create TG
- Architecture: projector no SaveChanges; assign service no `new TimetableSection`

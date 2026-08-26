# AI-SCHED-CAP Prompt 4 — Conflict Presentation, Classification & Actionable Scheduling Feedback

**Workstream:** AI-SCHED-CAP  
**Prompt:** 4 — Conflict Presentation  
**Date:** 2026-08-20  
**Baseline:** CAP Prompt 1–3A + frozen AI-SCHED-TG.3→TG.6  
**Status:** Implementation

---

## Existing conflict sources

| Source | Role |
| --- | --- |
| `ConflictEngine` + rules | Authoritative detect-only conflict analysis |
| `TimetableSoftValidationService` | Draft designer soft warnings |
| `IPlacementSizeResolver` | PlacementSize precedence |
| `IRoomCapacityEvaluator` | Effective room capacity / ROOM_CAPACITY |
| `TeachingGroupCapacityExceededRule` | TG MaxTeachingCapacity vs Resolved |

---

## Presentation model

`ISchedulingConflictPresentationComposer` / `SchedulingConflictPresentationComposer`

Produces presentation-ready `SoftWarningDto` fields:

- `Title`, `Why`, `SuggestedAction`, `Severity` (Critical/Error/Warning/Information)
- Rule-specific metrics (PlacementSize, EffectiveRoomCapacity, ResolvedStudentCount, MaxTeachingCapacity, TG identity)

Deterministic ordering: Severity desc → Code → EntryId → Day → Slot → Room → TG.

---

## Severity mapping

Underlying ConflictEngine severities are preserved for capacity rules (`Error`).

Presentation severity names align with `ConflictSeverity` enum names. Draft remains soft — `ConflictSummary.BlocksEditing = false`; mutations are not rejected.

---

## ROOM_CAPACITY

Meaning: PlacementSize &gt; EffectiveRoomCapacity.

Exposes: PlacementSize, RoomCapacity, CapacityMarginPercent, EffectiveRoomCapacity, PlacementSizeSource.

Action: select a larger room or adjust placement configuration — no automatic room change.

---

## TEACHING_GROUP_CAPACITY_EXCEEDED

Meaning: ResolvedStudentCount &gt; MaxTeachingCapacity (positive Max only).

Exposes: ResolvedStudentCount, MaxTeachingCapacity, TeachingGroup code/name/status.

Independent of room capacity. Archived TGs remain labeled; not cleared.

---

## PlacementSize / RoomCapacityEvaluator dependency

Composer **consumes** evaluator outputs; does not recalculate PlacementSize or room math.

SoftValidation and ConflictEngine capacity rules share the same evaluators and presentation copy helpers.

---

## Draft behavior

Create / Move / Copy / Paste / Duplicate / DnD remain allowed when warnings exist.

No Publish Gate. No hard mutation rejection.

---

## UI integration (additive)

- Extended `SoftWarningDto` consumed by `SoftWarningsPanel` (title/why/action/metrics)
- `TimetableGrid` shows capacity captions from soft warnings only
- `TimetableEntryDialog` shows soft-warning feedback + server `isOverMaxTeachingCapacity`
- No client PlacementSize / EffectiveRoomCapacity / TG capacity recalculation

---

## Architecture guards

Guards 1–10 in `AiSchedCapPrompt4ArchitectureGuardTests`.

---

## Deferred

- Publish Gate / publish readiness
- Hard Draft mutation rejection
- Automatic room or TG selection

---

## Test evidence

- `AiSchedCapPrompt4ConflictPresentationTests`
- `AiSchedCapPrompt4ArchitectureGuardTests`
- CAP Prompt 1–3A regression + scheduling/ConflictEngine/SoftValidation suites

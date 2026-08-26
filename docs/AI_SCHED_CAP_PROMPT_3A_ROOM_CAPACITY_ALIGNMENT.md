# AI-SCHED-CAP Prompt 3A — Room Capacity Rule Consistency & Soft Validation Alignment

**Workstream:** AI-SCHED-CAP  
**Prompt:** 3A — Room Capacity Alignment  
**Date:** 2026-08-20  
**Baseline:** Prompt 3 PlacementSize + capacity validation  
**Status:** Implementation

---

## Objective

Eliminate the inconsistency where ConflictEngine applied `RoomCapacityMarginPercent` but SoftValidation compared raw `Room.Capacity` to PlacementSize.

Both paths now share one authoritative evaluator.

---

## Shared abstraction

`IRoomCapacityEvaluator` / `RoomCapacityEvaluator`  
`Abhyanvaya.Application/Scheduling/Capacity/RoomCapacityEvaluator.cs`

```text
EffectiveRoomCapacity = Room.Capacity × (1 − RoomCapacityMarginPercent / 100)
Exceeded ⇔ PlacementSize > EffectiveRoomCapacity
```

Unset PlacementSize → not evaluable → no ROOM_CAPACITY finding.

---

## Integration

| Path | Wiring |
| --- | --- |
| `RoomCapacityExceededRule` | `context.RoomCapacityEvaluator.Evaluate(...)` |
| `TimetableSoftValidationService` | `_roomCapacityEvaluator.Evaluate(...)` + thresholds from `IConflictRuleConfigurationService` (same source as ConflictAnalyzer) |

PlacementSize remains `IPlacementSizeResolver` (Prompt 3).  
Teaching Group capacity remains separate (`TEACHING_GROUP_CAPACITY_EXCEEDED`).

---

## Preserved

- TG architecture / projector / Attendance / TeachingGroupId semantics  
- PlacementSize precedence (Resolved 0 valid)  
- Draft soft; no publish gate; no hard mutation rejection  
- No schema migration  

---

## Tests / guards

- `AiSchedCapPrompt3ARoomCapacityAlignmentTests` — margin matrix + engine/soft agreement  
- `AiSchedCapPrompt3AArchitectureGuardTests` — single evaluator; no duplicated inline math  

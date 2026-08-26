# AI-SCHED-CAP Prompt 3 — Placement Size Resolution & Capacity Validation Integration

**Workstream:** AI-SCHED-CAP  
**Prompt:** 3 — Placement Size & Capacity Validation  
**Date:** 2026-08-20  
**Type:** **IMPLEMENTATION** (Draft soft / detect-only)  
**Contract baseline:** `docs/AI_SCHED_CAP_PROMPT_2_CAPACITY_AND_CONFLICT_CONTRACT.md`  
**Frozen:** AI-SCHED-TG.3 → TG.6 Teaching Group / projection / Attendance boundaries

---

## Objective

Implement the first executable capacity-validation layer using the locked Prompt 2 contract:

1. Shared authoritative **PlacementSize** resolver
2. **ROOM_CAPACITY** evaluates against PlacementSize (not a private Subject.ExpectedCapacity copy)
3. New soft rule **TEACHING_GROUP_CAPACITY_EXCEEDED** (Resolved vs MaxTeachingCapacity)
4. Draft remains soft; **no publish gate**; **no hard DnD/mutation rejection**

---

## Existing architecture reused

| Component | Role |
| --- | --- |
| `ConflictEngine` / `IConflictRule` | Detect-only pipeline; new rule registered alongside existing rules |
| `ConflictAnalyzer` | Batch-loads masters; extended with TG + resolved counts |
| `ConflictAnalysisContext` | Shared snapshot + `ResolvePlacementSize` |
| `TimetableSoftValidationService` | Soft warnings for draft UI; same PlacementSize + TG capacity codes |
| `ITeachingGroupMembershipResolver` | Authoritative ResolvedStudentCount (tenant-scoped) |
| `TeachingGroup` domain capacity semantics | Max null = unset; Max ≤ 0 invalid for evaluation |

**Not changed:** TeachingGroup model, TeachingGroupSection SoT, TimetableSectionProjector sole writer, Attendance, Create/Update TG inference rules, Publish lifecycle gates.

---

## Placement Size resolution contract

**Abstraction:** `IPlacementSizeResolver` / `PlacementSizeResolver`  
**Location:** `Abhyanvaya.Application/Scheduling/Capacity/PlacementSizeResolver.cs`

```text
ResolvedStudentCount   (available, including 0)
        ↓
ExpectedStudentCount   (if > 0)
        ↓
Subject.ExpectedCapacity (if > 0)
        ↓
Unset
```

| Signal | Semantics |
| --- | --- |
| `ResolvedStudentCount = 0` | **Valid** PlacementSize; **do not** fall through |
| `ResolvedStudentCount` missing | Unavailable (null); try Expected |
| `ExpectedStudentCount ≤ 0` | Unset |
| `Subject.ExpectedCapacity ≤ 0` | Unset |
| `TeachingGroupId = null` | No membership resolve; use Subject.ExpectedCapacity if > 0 |

Result carries `Source`: `ResolvedStudentCount` | `ExpectedStudentCount` | `SubjectExpectedCapacity` | `Unset`.

---

## Room Capacity integration

`RoomCapacityExceededRule` (`ROOM_CAPACITY`):

```text
PlacementSize > Room.Capacity × (1 − margin%)
```

- Uses `ConflictAnalysisContext.ResolvePlacementSize(entry)`
- If PlacementSize unset → skip (no invented default)
- Does **not** read `MaxTeachingCapacity`
- Soft validation path uses the same resolver

---

## Teaching Group Capacity rule

`TeachingGroupCapacityExceededRule` (`TEACHING_GROUP_CAPACITY_EXCEEDED`):

```text
ResolvedStudentCount > MaxTeachingCapacity
```

when:

- Entry has `TeachingGroupId`
- TG loaded for current tenant
- `MaxTeachingCapacity` is a **positive** integer
- Resolved count is **available** (dictionary key present)

Null max → no conflict. Zero max → skip (invalid domain value). Zero resolved → no exceed.

Category: `ConflictCategory.Other` (no enum expansion). Severity: existing Error pattern (detect-only).

---

## Draft soft-validation behavior

Create / Move / Copy / Duplicate / Update **do not** reject solely for capacity.

Conflicts surface via:

- ConflictEngine results
- SoftWarningDto codes (`ROOM_CAPACITY`, `TEACHING_GROUP_CAPACITY_EXCEEDED`)

Both rules may fire independently on the same entry.

---

## Legacy TeachingGroupId=null behavior

Allowed. No inference, no auto-create, no membership resolve.

PlacementSize falls through to Subject.ExpectedCapacity when > 0.

---

## Tenant isolation

`ConflictAnalyzer` and soft validation load TeachingGroups with:

```csharp
g.TenantId == tenantId && tgIds.Contains(g.Id)
```

No `IgnoreQueryFilters()`. Cross-tenant TG ids do not populate context maps; PlacementSize then falls through to Subject.ExpectedCapacity.

---

## Performance considerations

- Distinct `TeachingGroupId`s only (not per-entry TG query)
- One TG master query + `ResolveCountAsync` per distinct loaded TG (same pattern as compatible TG query)
- Subjects/rooms already batch-loaded by ConflictAnalyzer
- No redesign of scheduling query architecture

---

## Tests

| Suite | Coverage |
| --- | --- |
| `AiSchedCapPrompt3PlacementSizeAndCapacityTests` | PlacementSize matrix (incl. Resolved=0), TG capacity matrix, independent dual fire, legacy null TG, tenant isolation, assigned-TG-only |
| `AiSchedCapPrompt3ArchitectureGuardTests` | Guards 1–11 |
| Existing Phase2B / Phase2B5 / SoftValidation | Regression with PlacementSize wiring |

---

## Architecture guards

| Guard | Assertion |
| --- | --- |
| 1 | Single `IPlacementSizeResolver` registered |
| 2 | ROOM_CAPACITY uses PlacementSize |
| 3 | TEACHING_GROUP_CAPACITY_EXCEEDED uses MaxTeachingCapacity / Resolved |
| 4 | Room vs TG capacity stay separate |
| 5 | No SA→TG inference |
| 6 | No automatic TeachingGroup creation |
| 7 | Draft capacity remains soft |
| 8 | No publish blocking |
| 9 | No TimetableSection writes outside projector |
| 10 | No Attendance changes in CAP files |
| 11 | No TeachingGroup architecture model change; tenant filter present |

---

## Deferred Publish Gate

**Not implemented.** Level 3 publish readiness / critical integrity / error capacity publish blocking remain future prompts.

---

## Deferred hard mutation rejection

**Not implemented.** Capacity must not hard-fail Create/Move/Copy/Duplicate/Update.

---

## Known limitations

1. Soft validation / ConflictAnalyzer resolve membership per distinct TG (resolver API has no batch count method).
2. When TG id is set but TG is missing from tenant scope, Resolved/Expected unavailable → Subject fallback.
3. TG capacity rule requires Resolved availability; does not invent Resolved from Expected.
4. UI continues to consume existing soft/conflict channels only — no new capacity dialogs.

---

## Completion checklist

- [x] Shared PlacementSize resolver
- [x] ResolvedStudentCount=0 semantics
- [x] Expected / Subject Expected fallbacks
- [x] ROOM_CAPACITY uses resolver
- [x] TEACHING_GROUP_CAPACITY_EXCEEDED implemented
- [x] Room vs TG capacity separate
- [x] Draft soft; no publish gate; no hard DnD rejection
- [x] TeachingGroupId=null valid; no TG inference/create
- [x] Tenant isolation verified (tests + analyzer filter)
- [x] No TimetableSection / Attendance / TG architecture violations
- [x] Documentation completed

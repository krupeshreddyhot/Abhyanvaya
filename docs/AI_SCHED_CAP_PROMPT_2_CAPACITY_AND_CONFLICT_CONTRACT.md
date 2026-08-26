# AI-SCHED-CAP Prompt 2 — Capacity & Conflict Contract

**Workstream:** AI-SCHED-CAP  
**Prompt:** 2 — Capacity & Conflict Contract  
**Date:** 2026-08-20  
**Type:** **CONTRACT / ARCHITECTURE DESIGN ONLY** — no production behavior changes  
**Status:** **PASS** (contract locked for Prompt 3 implementation)

**Frozen:** AI-SCHED-TG.3 → TG.6 (Teaching Group / projection / Attendance boundaries).  
**Baseline:** `docs/AI_SCHED_CAP_PROMPT_1_SCHEDULING_CAPABILITY_ARCHITECTURE_DISCOVERY.md`

---

## 1. Executive decision summary

| Decision | Contract |
| --- | --- |
| Extension surface | **Reuse** existing `ConflictEngine` + `TimetableSoftValidationService` + future Publish Readiness — **no parallel conflict subsystem** |
| PlacementSize for **room fit** | **ResolvedStudentCount → ExpectedStudentCount → Subject.ExpectedCapacity** (first available) |
| Physical vs teaching capacity | **Room.Capacity ≠ MaxTeachingCapacity** — never merge |
| Draft mutations | Continue to **permit** scheduling conflicts (soft / detect-only) unless listed hard invariants |
| Publish | Introduce **Publish Gate (Level 3)** for approved **blocking** conflicts — **not implemented in Prompt 2** |
| Legacy entries (`TeachingGroupId = null`) | Remain valid; PlacementSize falls through to Subject.ExpectedCapacity; **no TG inference/create** |
| TG.2A historical note | TG.2A Draft PlacementSize order was Expected→Resolved→Subject. **CAP supersedes that order for room-fit PlacementSize only**, because Resolved is the authoritative roster size for physical seating. Membership / MaxTeachingCapacity semantics from TG.5 remain unchanged. |

---

## 2. PlacementSize definition

**PlacementSize** is the integer headcount used to compare a timetable entry against **physical** `Room.Capacity` (room-fit / `ROOM_CAPACITY` family).

It is **not**:

- the Teaching Group roster SoT (that remains membership → ResolvedStudentCount);
- MaxTeachingCapacity;
- a persisted TimetableEntry column.

**Conceptual function (contract only):**

```text
PlacementSize(entry) → int?   // null = unavailable / cannot evaluate room-fit size
```

---

## 3. PlacementSize precedence

### Authoritative order (CAP)

```text
1. If TimetableEntry.TeachingGroupId is set
      AND ResolvedStudentCount is available
      → use ResolvedStudentCount

2. Else if TeachingGroupId is set
      AND ExpectedStudentCount is provided (see §3.D–E)
      → use ExpectedStudentCount

3. Else if Subject.ExpectedCapacity is provided
      → use Subject.ExpectedCapacity

4. Else → PlacementSize unavailable (null)
```

### A–K answers

| # | Question | Contract answer |
| --- | --- | --- |
| A | What is “available” ResolvedStudentCount? | TeachingGroupId set; membership resolver returns successfully for that TG (tenant-scoped). Count is an integer ≥ 0. |
| B | Is ResolvedStudentCount = 0 valid? | **Yes.** Empty roster is a valid resolved state. Prefer it over Expected when available. |
| C | Is ExpectedStudentCount = 0 valid? | **Treat 0 as unset** for PlacementSize (same as null). Planning “zero students” is not a seating signal. |
| D | Does null mean “not provided”? | **Yes** for ExpectedStudentCount and Subject.ExpectedCapacity. |
| E | When use ExpectedStudentCount? | Only when TeachingGroupId is set **and** ResolvedStudentCount is **unavailable** (resolver failure — rare) **or** CAP implementation phase explicitly documents a “planning-only preview” mode. **Default production path:** Resolved first whenever available. If Resolved is available (including 0), **do not** fall back to Expected for room-fit. |
| F | When use Subject.ExpectedCapacity? | TeachingGroupId **null** (legacy), **or** TG path exhausted without a PlacementSize, **and** Subject.ExpectedCapacity is non-null and > 0. Treat ExpectedCapacity ≤ 0 as unset. |
| G | TeachingGroupId null? | No TG inference. PlacementSize = Subject.ExpectedCapacity if provided; else unavailable. |
| H | Archived TeachingGroup? | If still assigned: Resolved (or Expected fallback per E) still applies for **display/evaluation** of existing assignment. Reassignment rules remain TG.4 lifecycle (Archived not newly assignable). Do not silently clear TG. |
| I | Membership resolution cannot produce a value? | PlacementSize skips Resolved; try Expected (if provided); then Subject.ExpectedCapacity; else unavailable. Surface as **capacity evaluation unavailable** — do not invent a count. |
| J | Can PlacementSize be unavailable? | **Yes** (`null`). Room-fit rule: **skip / no capacity finding** (not a silent 0). |
| K | Legacy entries use Subject.ExpectedCapacity? | **Yes** — preserve current ROOM_CAPACITY subject-based behavior for TG-null entries. |

**Clarification on E vs default:** For Prompt 3 implementation, **always prefer Resolved when TeachingGroupId is set and resolver succeeds**, including Resolved = 0. Expected is only for TG planning when Resolved is unavailable, or for soft “plan vs room” advisory if a later prompt adds a separate code (not required in Prompt 3 MVP).

---

## 4. Capacity semantics

| Signal | Meaning | Used for |
| --- | --- | --- |
| `TeachingGroup.MaxTeachingCapacity` | Optional teaching/planning ceiling for the TG | Membership mutations (existing hard); TG teaching-capacity conflict |
| `Room.Capacity` | Physical seats | Room-fit vs PlacementSize |
| `ResolvedStudentCount` | Authoritative roster size | PlacementSize (primary); MaxTeachingCapacity check |
| `ExpectedStudentCount` | Optional planning estimate | PlacementSize fallback; planning advisories |
| `Subject.ExpectedCapacity` | Legacy/fallback expected size | PlacementSize for legacy / last fallback |

**Do NOT merge these concepts.**

### A. Teaching Group capacity (teaching ceiling)

```text
IF MaxTeachingCapacity is set (non-null, > 0)
AND ResolvedStudentCount > MaxTeachingCapacity
→ TEACHING_GROUP_CAPACITY_EXCEEDED
```

| Missing MaxTeachingCapacity | Result |
| --- | --- |
| null / unset | **Pass** (no teaching-ceiling evaluation) |

| Severity (contract) | Soft (Draft) | Publish gate |
| --- | --- | --- |
| Error | Soft warning / ConflictEngine Error finding | **Blocking** |

Membership APIs already hard-block adds that would exceed MaxTeachingCapacity — CAP does not weaken that.

### B. Room capacity (physical fit)

```text
IF PlacementSize is available
AND Room.Capacity is known
AND PlacementSize > EffectiveRoomCapacity
→ ROOM_CAPACITY (enhanced to use PlacementSize)
```

**EffectiveRoomCapacity** = existing ConflictEngine margin behavior:

```text
Room.Capacity * (1 - RoomCapacityMarginPercent/100)
```

(Keep current threshold infrastructure.)

| PlacementSize unavailable | Result |
| --- | --- |
| null | **No room-capacity finding** (unavailable — not pass-as-zero) |

| Soft vs Publish | Soft (Draft) | Publish |
| --- | --- | --- |
| PlacementSize > EffectiveRoomCapacity | Soft warning + ConflictEngine Error | **Blocking** |

### C. Missing / unknown matrix

| Condition | Soft / ConflictEngine | Publish |
| --- | --- | --- |
| PlacementSize null | No size-based ROOM_CAPACITY finding | Does not block solely for missing size |
| Room missing from context | Skip room rules for entry (existing pattern) | Same |
| MaxTeachingCapacity unset | No TG ceiling finding | No block for TG ceiling |
| Resolved = 0 | Valid PlacementSize 0 → room-fit OK unless other rules | OK |

**Never** silently assume PlacementSize = Room.Capacity or invent headcount.

---

## 5. Conflict taxonomy

### Reuse existing rules (do not duplicate)

| Code | Domain | Severity today | Blocking at Publish? | Draft soft? | Notes |
| --- | --- | --- | --- | --- | --- |
| `FACULTY_DOUBLE_BOOKING` | Faculty | Critical | **Yes** | Soft warning / engine Critical | Keep; entry-level |
| `ROOM_DOUBLE_BOOKING` | Room | Critical | **Yes** | Soft / engine Critical | Keep |
| `ROOM_CAPACITY` | Room | Error | **Yes** | Soft / engine Error | **Enhance input** to PlacementSize; same code |
| `STUDENT_GROUP_OVERLAP` | Student | Critical | **Yes** | Soft / engine Critical | Keep; Course/Group/Semester overlap — not TG membership student-id |
| Soft `DUPLICATE_FACULTY_SESSION` | Faculty | Warning | No | Soft only | Align messaging with engine |
| Soft `DUPLICATE_ROOM_SESSION` | Room | Warning | No | Soft only | Align with ROOM_DOUBLE_BOOKING |

### New / renamed CAP rule (if needed)

| Code | Domain | Severity | Publish block? | When |
| --- | --- | --- | --- | --- |
| `TEACHING_GROUP_CAPACITY_EXCEEDED` | TeachingGroup | Error | **Yes** | Resolved > MaxTeachingCapacity when Max set |

**Do not** add a second ROOM_CAPACITY rule. Extend the existing rule’s size input.

**Do not** add SubjectAllocation→TG inference rules.

### Per-conflict template fields (authoritative)

For each code above the contract requires implementers to preserve:

- **code** — stable string  
- **domain** — Faculty / Room / Student / TeachingGroup / Calendar  
- **severity** — Information / Warning / Error / Critical (existing enum)  
- **blocking status** — Soft-only | Publish-blocking | (Hard mutation only if §7)  
- **affected entity** — TimetableEntry (+ related entry id when pairwise)  
- **authoritative source** — ConflictEngine rule / SoftValidation shared PlacementSize helper  
- **explanation** — human-readable; no tenant leakage  
- **Draft** — findings allowed; mutations not blocked by engine Critical (except §7)  
- **Publish** — per blocking column  
- **Informational only** — Warning/Information never block Publish  

---

## 6. Severity model

| Severity | Soft designer | Conflict workspace | Publish gate |
| --- | --- | --- | --- |
| Information | Show | Show | Allow |
| Warning | Show | Show | Allow |
| Error | Show | Show | **Block** if rule marked publish-blocking |
| Critical | Show | Show | **Block** |

All **Critical** scheduling integrity rules in §5 are publish-blocking.  
All **Error** capacity rules in §5 (`ROOM_CAPACITY`, `TEACHING_GROUP_CAPACITY_EXCEEDED`) are publish-blocking.

---

## 7. Soft vs hard vs publish gate

### LEVEL 1 — INFORMATIONAL / SOFT (default Draft)

Scheduler **continues**. SoftValidation + ConflictEngine detect-only.

Examples: capacity warning, preference mismatches, non-blocking advisories.

### LEVEL 2 — HARD MUTATION BLOCK

**Only** where an existing invariant already requires rejection. CAP Prompt 2 does **not** convert all Critical findings into mutation rejects.

| Already hard (preserve) | CAP Prompt 2 |
| --- | --- |
| EnsureDraft / Frozen / RBAC | Unchanged |
| TG compatibility on assign / entry SA change | Unchanged |
| Membership exceeds MaxTeachingCapacity | Unchanged |
| Cross-tenant TG/Room | Unchanged (not found / forbidden) |

| Not hard in Draft (contract) | Reason |
| --- | --- |
| Faculty/room double booking | Existing AI30 Draft UX allows construction with soft/engine findings |
| ROOM_CAPACITY | Soft + publish gate first |

**Future optional Level 2** for double-booking may be a later CAP prompt — **out of Prompt 3 MVP**.

### LEVEL 3 — PUBLISH GATE

```text
Draft → Readiness Validation → blocking? → NOT READY
                              → warnings only → READY → Published
```

Blocking = Critical scheduling integrity + Error capacity rules listed in §5–§6.

---

## 8. Publish policy (contract — not implemented)

### Conceptual readiness

**Request (conceptual):** `GET/POST …/timetables/{id}/publish-readiness` (exact route in Prompt 3+).

**Response (conceptual):**

```text
{
  isReady: bool,
  blockingConflicts: ConflictFindingDto[],
  warnings: ConflictFindingDto[],
  affectedEntryIds: number[],
  affectedRoomIds: number[],
  evaluatedAt: timestamp,
  placementSizeSourceByEntryId?: { entryId, source, value }  // optional diagnostics
}
```

**Determinism:** Same timetable state → same readiness result (same rule set + thresholds + PlacementSize).

**Publish:** Must refuse when `isReady = false`. Exact API wiring deferred to Prompt 3/4.

---

## 9. Legacy entry policy

`TimetableEntry.TeachingGroupId = null`:

| Action | Allowed? |
| --- | --- |
| Auto-create TG | **No** |
| Infer TG from SubjectAllocation | **No** |
| Mutate TeachingGroupSection | **No** |
| Mutate Attendance / StudentSection | **No** |
| PlacementSize | Subject.ExpectedCapacity if set; else unavailable |
| ROOM_CAPACITY | Existing subject-based semantics (via PlacementSize fallback) |

Legacy remains a **first-class** Draft/Published state until an explicit conversion workstream (out of CAP).

---

## 10. Mutation policy

| Operation | ConflictEngine today | CAP Prompt 2 contract |
| --- | --- | --- |
| create / update / move / copy / duplicate / bulk | No hard conflict reject | **Remain soft** (Level 1); refresh soft warnings after mutation |
| clone / version | No conflict check | **Remain soft**; optional post-clone analysis later |
| assign/clear TG | Projection sync (TG.6 P21) | After assign, soft/engine may re-evaluate ROOM_CAPACITY / TG capacity |

**Draft construction continues to permit conflicts.** Hard mutation rejection of double-booking is **not** in Prompt 3 MVP.

---

## 11. Shared conflict semantics

```text
        PlacementSizeResolver (conceptual shared helper)
                    │
     ┌──────────────┼──────────────┐
     ▼              ▼              ▼
SoftValidation  ConflictEngine  PublishReadiness
```

**Rule:** One PlacementSize algorithm; three consumers.  
No independent “soft PlacementSize” vs “engine PlacementSize”.

Room margin thresholds remain ConflictEngine threshold service (existing).

---

## 12. Tenant / security contract

| Concern | Rule |
| --- | --- |
| Tenant isolation | Ambient query filters; **no IgnoreQueryFilters** in CAP paths |
| Room lookup | Tenant-scoped SchedulingRooms only |
| TG / Resolved | Tenant-scoped TeachingGroup + MembershipResolver |
| Cross-tenant conflict visibility | **None** — other-tenant resources not found / not listed |
| Authorization | Existing Scheduling Timetable View/Manage; Publish remains SchedulingPublish (or existing publish permission) |
| Error messages | No tenant ids / foreign resource leakage |

---

## 13. Performance considerations

| Risk | Guidance (Prompt 3+) |
| --- | --- |
| Resolve membership per grid cell | Soft path: reuse counts from TG DTO / batch ResolveCount for distinct TeachingGroupIds on the timetable |
| Large timetable ConflictEngine run | Keep existing analyzer batching; do not N+1 room/TG loads inside rules |
| Publish readiness | Single ConflictEngine analysis pass + PlacementSize batch map |
| Caching | Optional per-request cache of PlacementSize by TeachingGroupId |

**Do not** optimize in Prompt 2.

---

## 14. Attendance boundary

Confirmed:

- No Attendance schema changes  
- No Attendance mutation from capacity/conflict  
- No StudentSection mutation  
- Scheduling owns conflict/capacity  
- Attendance continues to consume TimetableSection projection + published/locked entry context  

---

## 15. Explicit non-goals

- Parallel conflict engine  
- Client-side PlacementSize / compatibility  
- SA→TG inference / auto TG create  
- TimetableSection writes outside projector  
- Hard-fail all Critical findings on every DnD  
- Merging MaxTeachingCapacity with Room.Capacity  
- Permanent legacy backfill  
- Attendance redesign  
- Implementing Publish gate / engine changes in this prompt  

---

## 16. Recommended Prompt 3 implementation scope

**AI-SCHED-CAP Prompt 3 — PlacementSize + ROOM_CAPACITY enhancement (MVP)**

1. Shared `PlacementSize` resolver (application helper) implementing §3.  
2. Update `RoomCapacityExceededRule` + `TimetableSoftValidationService` ROOM_CAPACITY to consume PlacementSize.  
3. Add `TEACHING_GROUP_CAPACITY_EXCEEDED` ConflictEngine rule (Error).  
4. Soft warnings aligned to same codes/messages.  
5. Unit/architecture tests for PlacementSize cases A–K and legacy TG-null.  
6. **Do not** yet: Publish gate API, hard mutation blocks for double-booking, UI redesign.  

**Prompt 4 (suggested):** Publish readiness endpoint + PublishAsync gate using Level 3 policy.

---

## Unresolved decisions (explicit — do not block Prompt 3 MVP)

| Item | Default for MVP | Needs Chief Architect if changed |
| --- | --- | --- |
| Hard-block Draft double-booking | **Off** | Product UX |
| Separate soft code for Expected-vs-Room when Resolved exists | **Not in MVP** | Only if planning preview required |
| Publish readiness route shape | Deferred to Prompt 4 | API naming |

---

## Prompt 2 verification

| Check | Result |
| --- | --- |
| Production code modified | **None** |
| Schema / APIs / UI / Attendance | **None** |
| TG freeze intact | **Yes** |
| ConflictEngine remains extension surface | **Yes** |
| Contract document | This file |

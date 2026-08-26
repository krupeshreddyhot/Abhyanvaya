# AI-SCHED-CAP Prompt 1 — Scheduling Capability Architecture Discovery

**Workstream:** AI-SCHED-CAP  
**Prompt:** 1 — Scheduling Capacity, Room & Conflict Intelligence Architecture Discovery  
**Date:** 2026-08-20  
**Type:** **DISCOVERY ONLY** — no production behavior changes  
**Status:** **PASS**

Frozen predecessor: AI-SCHED-TG.3 → AI-SCHED-TG.6 (architecturally frozen). This prompt does **not** reopen Teaching Group architecture.

---

## 1. Current architecture (high level)

```text
SubjectAllocation
       │
       ▼
TeachingGroup  (explicit create / manage — frozen)
       │
       ├── TeachingGroupSection     ← section membership SoT (frozen)
       ├── Resolved Membership      ← server MembershipResolver (frozen)
       └── TimetableEntry.TeachingGroupId  ← dedicated assign/clear (frozen)
                    │
                    ├── RoomId → Room (SchedulingRoom)
                    ├── DayOfWeek
                    ├── TimeSlotId → TimeSlot (period)
                    └── StaffId
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
   TimetableSoftValidationService   ConflictEngine (detect-only)
   (designer soft warnings)         + ConflictDetectionService
              │                         │
              ▼                         ▼
        SoftWarningsPanel         Conflict workspace / analytics / BG job
```

**Attendance Timetable mode** reads Published/Locked entries + `TimetableSection` projection (read-only). No Attendance write-back from room/time edits.

---

## 2. Existing entities

| Concern | Entity | Table / notes |
| --- | --- | --- |
| Room | `Room` | `SchedulingRoom` — `Capacity`, `Code`, `Name`, `RoomType`, `Status`, `IsActive`, `FloorId` → Building → Campus |
| Timetable | `Timetable` | Status Draft/Locked/Published/Archived; `IsFrozen` |
| Entry | `TimetableEntry` | DayOfWeek, TimeSlotId, RoomId, StaffId, SubjectAllocationId, TeachingGroupId? |
| Period | `TimeSlot` | PeriodNumber, Start/End, SlotKind, optional DayOfWeek |
| TG | `TeachingGroup` | MaxTeachingCapacity, ExpectedStudentCount; ResolvedStudentCount derived |
| TG↔Section | `TeachingGroupSection` | SoT (frozen) |
| Projection | `TimetableSection` | Projection-only; sole writer `TimetableSectionProjector` |
| Conflict run | `ConflictDetectionRun` / findings | Persisted detection results |

---

## 3. Existing services

| Service | Role |
| --- | --- |
| `CampusFacilityService` | Room CRUD; Capacity > 0 on create |
| `TimetableService` | Entry create/update/move/copy/duplicate/bulk; EnsureDraft; TG compatibility on mutations; **no conflict hard-fail** |
| `TeachingGroupApplicationService` | Explicit assign/clear + Prompt 21 projection sync |
| `TeachingGroupMembershipResolver` | Authoritative ResolvedStudentCount |
| `TimetableSoftValidationService` | Soft warnings for designer (non-blocking) |
| `ConflictEngine` | Plugin rule runner — **detect-only, never mutates** |
| `ConflictDetectionService` / `ConflictAnalyzer` | Run analysis, persist findings |
| `TimetableLifecycleService` | Publish / freeze — **no conflict/capacity gate today** |
| `ScheduleVersionService` / `TimetableCloneService` | Clone entries; **no conflict checks** |
| `AttendanceSessionResolver` | Timetable-mode context; reads Room + TimetableSection |

---

## 4. Existing APIs

| Area | Route surface |
| --- | --- |
| Rooms | `api/scheduling/rooms` (+ availability, features, rules) |
| Timetables / entries | `api/scheduling/timetables/*` including soft-warnings, TG assign/clear, compatible-TG |
| Conflicts | `api/scheduling/conflicts/*` (Phase 2B) |
| Teaching Groups | `api/scheduling/teaching-groups/*` |
| Legacy sections | `PUT api/timetable/{id}/sections` → TG section SoT + projector |

---

## 5. Existing UI

| Surface | Path / notes |
| --- | --- |
| Rooms | `RoomsPage`, availability/features/rules, `RoomTimetablePage` |
| Timetable designer | `TimetableDesignerPage` + soft warnings panel + entry dialog |
| TG capacity cue | Dialog/grid warning when Resolved > MaxTeachingCapacity (informational) |
| Conflict workspace | Conflict dashboard / analytics / rules pages |
| Client | `schedulingService.ts` |

---

## 6. Existing conflict behavior

### Dedicated ConflictEngine: **YES**

Location: `Abhyanvaya.Application/Scheduling/Conflicts/`  
Nature: **detect-only**. Create/update/move/copy/clone/version **do not** call the engine to hard-reject.

### Hard vs soft

| Path | Behavior |
| --- | --- |
| Entry mutations (`TimetableService`) | Hard: Draft/lifecycle + TG compatibility only. Overlaps allowed. |
| Soft warnings | Designer informational; dismissible |
| ConflictEngine rules | Critical/Error/Warning findings for workspace/BG — not mutation gates |
| Publish | No conflict/capacity prerequisite today |

### Representative rules

| Code | Domain | Severity (engine) |
| --- | --- | --- |
| `FACULTY_DOUBLE_BOOKING` | Faculty | Critical |
| `ROOM_DOUBLE_BOOKING` | Room | Critical |
| `ROOM_CAPACITY` | Room | Error — uses **Subject.ExpectedCapacity** vs Room.Capacity (+ margin) |
| `STUDENT_GROUP_OVERLAP` | Student/group | Critical |
| Soft `ROOM_CAPACITY` | Soft validation | Warning — same Subject.ExpectedCapacity signal |
| Soft `DUPLICATE_FACULTY_SESSION` / `DUPLICATE_ROOM_SESSION` | Soft | Warning |

**Gap:** Neither soft path nor `ROOM_CAPACITY` engine rule uses TeachingGroup `ResolvedStudentCount` / PlacementSize today.

---

## 7. Existing room behavior

- Authoritative master: `Room` / `SchedulingRoom` via `CampusFacilityService`
- Capacity is physical seat count; create rejects `Capacity <= 0`
- Features, availability, allocation rules already exist
- Timetable placement stores `TimetableEntry.RoomId`
- Room capacity vs expected headcount is already partially implemented (**Subject.ExpectedCapacity** only)

---

## 8. Teaching Group integration points (frozen)

```text
TeachingGroupSection = SoT
TimetableSectionProjector = sole TimetableSection writer
TimetableEntry.TeachingGroupId = explicit assign/clear
ResolvedStudentCount = MembershipResolver (server)
```

**Do not** in CAP:

- Infer TG from SubjectAllocation  
- Auto-create TG  
- Write TimetableSection outside projector  
- Redesign TG management UI  
- Client-side compatibility  

**CAP may extend:**

- Conflict rules / soft validation that **consume** resolved count + MaxTeachingCapacity + Room.Capacity  
- Optional publish policy later  
- Designer presentation of capacity/scheduling conflicts (reuse SoftWarnings / Conflict UI)

---

## 9. Attendance dependencies

| Change | Effect on Attendance |
| --- | --- |
| Draft room/day/period edits | No effect until Published/Locked |
| Published freeze | Read-only designer |
| Timetable mode resolve | Uses entry RoomId + TimetableSection projection |
| Room/time move | Does not mutate Attendance rows |

**CAP must not** redesign Attendance schema or membership rules.

---

## 10. Lifecycle behavior

| State | Edit entries? | Notes |
| --- | --- | --- |
| Draft | Yes (if manage + not frozen) | Soft warnings + designer |
| Locked | No (EnsureDraft) | Review gate before publish |
| Published | No | Publish currently has no conflict gate |
| Archived | No | |
| IsFrozen | No | Academic admin unlock |

---

## 11. Versioning behavior

- Schedule versions and clone copy entries (including TeachingGroupId) via `CloneEntry`
- **Conflicts are not evaluated** during clone/version creation
- Conflict detection runs against a chosen timetable/run via Conflict APIs / background analyzer — not automatically on clone

---

## 12. Architectural Q&A (from code)

| # | Question | Answer |
| --- | ---: | --- |
| 1 | Authoritative room source? | `Room` / `SchedulingRoom` + `CampusFacilityService` |
| 2 | Where should room capacity validation live? | Extend `TimetableSoftValidationService` + `RoomCapacityExceededRule` (and optional future Publish policy); keep Room master separate from TG MaxTeachingCapacity |
| 3 | Where should overlap validation live? | Already in ConflictEngine + soft duplicates; hard-fail only if a future CAP policy explicitly gates mutations or Publish — not reinvented in UI |
| 4 | Conflict engine exists? | **Yes** — detect-only plugin engine |
| 5 | Multiple TGs share a slot? | **Yes** at model (no unique constraint); engine flags room/staff/group overlaps |
| 6 | One TG, multiple entries? | **Yes** |
| 7 | Conflict levels? | Entry-centric: Faculty, Room, Student (group/semester/subject), Calendar. No student-id membership conflict yet; no dedicated TG-overlap rule |
| 8 | Resolved student count? | `ITeachingGroupMembershipResolver.ResolveCountAsync` |
| 9 | Capacity calculated? | Room vs Subject.ExpectedCapacity (soft + engine). TG MaxTeachingCapacity enforced on **membership** mutations; UI warns on TG vs resolved. PlacementSize→Room **not** wired |
| 10 | Extend vs duplicate? | Soft warnings, ConflictEngine rules, Conflict workspace UI, Timetable designer panels, Rooms APIs — **extend these** |

---

## 13. Recommended architecture boundary for CAP

```text
                  (frozen) TeachingGroup + Resolver
                              │
                              ▼
                    PlacementSize signal
                 (Resolved → Expected → Subject.Expected)
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
     Capacity Conflict                   Scheduling Conflict
     (Room.Capacity vs size)             (overlap / availability)
              │                               │
              └────────── ConflictEngine ─────┘
                              │
                    Soft warnings (designer)
                    Optional Publish policy (later)
```

**Single extension surface:** application Conflict + SoftValidation layers.  
**Not:** new TG SoT, new TimetableSection writer, Attendance redesign, client conflict engine.

---

## 14. Explicit out-of-scope (Prompt 1 and CAP overall unless later approved)

- Reopening AI-SCHED-TG architecture  
- SubjectAllocation → TG inference / auto TG create  
- Direct TimetableSection writes  
- Attendance / StudentSection schema changes  
- Permanent legacy backfill / startup reconciler  
- Redesign of TG management UI  
- Implementing Prompt 2 in this prompt  

---

## 15. Risks

| Risk | Mitigation |
| --- | --- |
| Dual capacity signals (Subject.ExpectedCapacity vs TG Resolved) confuse users | Define PlacementSize precedence in Prompt 2 contract; keep Room.Capacity physical |
| Hard-fail on every DnD would break existing Draft UX | Prefer soft + ConflictEngine first; hard-fail only at explicit policy points |
| Publish without conflict gate | Document as current gap; CAP Prompt 2+ may propose optional publish gate |
| N+1 when resolving TG counts for full grid | Reuse resolver carefully; batch if needed later |
| Background ConflictValidation job vs designer soft path drift | Keep shared rule semantics / codes aligned |

---

## 16. Recommended Prompt 2 scope

**AI-SCHED-CAP Prompt 2 — Capacity & Conflict Contract (design only or thin contract):**

1. Define **PlacementSize** precedence: ResolvedStudentCount → ExpectedStudentCount → Subject.ExpectedCapacity  
2. Specify Capacity Conflict vs Scheduling Conflict taxonomy and severities  
3. Contract: soft vs hard vs publish-gate (without implementing hard mutation fails unless approved)  
4. Map extensions onto `ROOM_CAPACITY` rule + soft validation + optional new TG-aware rule codes  
5. Explicitly keep Room.Capacity ≠ MaxTeachingCapacity (teaching vs physical)  
6. Freeze TG boundaries again in the contract  

**Do not implement** engine changes until Prompt 2 contract is accepted.

---

## 17. Discovery verification

| Check | Result |
| --- | --- |
| Production code modified | **None** |
| Schema / migrations | **None** |
| Frozen TG architecture intact | **Yes** |
| New TimetableSection writer | **None** |
| Attendance mutation | **None** |

---

## Prompt 1 outcome

| Item | Result |
| --- | --- |
| Status | **PASS** |
| Next | AI-SCHED-CAP Prompt 2 — Capacity & Conflict Contract |
| Implementation | **STOPPED** (discovery only) |

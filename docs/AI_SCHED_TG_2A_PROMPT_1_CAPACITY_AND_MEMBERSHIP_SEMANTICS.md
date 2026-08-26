# AI-SCHED-TG.2A Prompt 1 — Teaching Group Capacity & Membership Semantics

**Workstream:** AI-SCHED-TG.2A  
**Prompt:** 1 — Capacity & membership semantics clarification  
**Date:** 2026-08-17  
**Type:** DESIGN ONLY  

**Supersedes ambiguous capacity naming in AI-SCHED-TG.2** (`PlannedCapacity` / `MaxCapacity`) with unambiguous terms below.

**No production code, schema, API, or UI was changed in this prompt.**

---

## 1. Executive Summary

AI-SCHED-TG.2 introduced `PlannedCapacity`, which conflated planning intent, teaching limits, and (sometimes) live headcount. This document splits those concerns into three teaching-group concepts plus the existing room master:

| Term | Role |
|---|---|
| **ResolvedStudentCount** | Derived headcount from membership resolution |
| **ExpectedStudentCount** | Optional persisted planning intent |
| **MaxTeachingCapacity** | Optional persisted operational teaching ceiling |
| **Room.Capacity** | Existing physical room constraint (unchanged) |

**Authoritative student identity** remains membership resolution — never a manually typed “student count” field.

**STATUS: PASS** — semantics are unambiguous for AI-SCHED-TG.3 schema/implementation.

---

## 2. Existing AI-SCHED-TG.2 assumptions

From TG.2 design artifacts:

- Model: `SubjectAllocation → TeachingGroup → TimetableEntry`
- TeachingGroup ≠ Section
- Schema proposed `PlannedCapacity` + optional `MaxCapacity`
- Room validation compared `Room.Capacity` to `PlannedCapacity` (fallback `Subject.ExpectedCapacity`)
- Membership: dynamic (Section/StudentSubject) and/or explicit rows
- Capacity splits must not create Sections

---

## 3. Capacity semantic problem

`PlannedCapacity` could be read as:

1. “How many students we expect,”  
2. “How many we allow,” or  
3. “How many are in the group now.”

Mixing these causes wrong validation (e.g. blocking publish because expected ≠ resolved, or treating room size as teaching-group size).

**Decision:** Retire `PlannedCapacity` as a domain name. Do not persist ResolvedStudentCount.

---

## 4. Canonical terminology

### A. Names (APPROVED)

| Canonical name | Replaces |
|---|---|
| `ExpectedStudentCount` | Ambiguous “planned size” / part of `PlannedCapacity` |
| `MaxTeachingCapacity` | `MaxCapacity` / “teaching capacity” half of `PlannedCapacity` |
| `ResolvedStudentCount` | Live count (never stored as SoT) |
| `Room.Capacity` | Existing (`Abhyanvaya.Domain.Entities.Scheduling.Room`) |

Equivalent names are **not** approved for persistence. DTOs may expose display aliases only if they map 1:1 to these terms.

### B. Classification

| Value | Persisted? | Derived? | Optional? | Authoritative for | Advisory for |
|---|---|---|---|---|---|
| ResolvedStudentCount | **No** | **Yes** | N/A (always computable) | Membership truth; attendance roster size | — |
| ExpectedStudentCount | **Yes** | No | **Yes** (null/0 = unset) | Planning / designer hints | Soft schedule warnings |
| MaxTeachingCapacity | **Yes** | No | **Yes** (null = unset) | TG capacity errors vs resolved | Soft warnings vs expected |
| Room.Capacity | Yes (Room) | No | No (room master) | Physical placement constraint | — |
| Subject.ExpectedCapacity | Yes (Subject) | No | Yes | Legacy fallback only when TG expected/max unset | Soft ROOM_CAPACITY |

**Authoritative chain for “who is in the class”:** membership resolver → ResolvedStudentCount.  
**Never** use ExpectedStudentCount as a second student roster.

---

## 5. Persisted vs derived values

### Persisted on TeachingGroup (TG.3 schema impact)

```text
ExpectedStudentCount  int?   -- null = unset
MaxTeachingCapacity   int?   -- null = unset
```

Remove `PlannedCapacity` and rename/replace `MaxCapacity` → `MaxTeachingCapacity`.

### Derived at runtime

```text
ResolvedStudentCount = count(distinct students from MembershipResolver(TeachingGroup))
```

---

## 6. Membership authority

| Store | Authority |
|---|---|
| `StudentSection` | Academic section membership only |
| `StudentSubject` | Subject enrollment (electives / subject pool) — validated in **full academic scope** |
| `TeachingGroupMembership` | Explicit operational Include/Exclude rows |
| TeachingGroup section links | Which Sections feed dynamic resolution |

**Invariant:** Creating/updating TeachingGroup membership **must not** insert/update/delete `StudentSection`.

### Dynamic resolution

- SectionDerived / CombinedSections → current `StudentSection` for linked sections  
- StudentSubject source → students with `StudentSubject` for Subject **and** Student in Tenant + Course/Group/Semester (+ AcademicYear as applicable via Student/enrollment context)

### Explicit resolution

- Materialized `TeachingGroupMembership` Includes (− Excludes for Hybrid)

### Full academic-scope validation (electives and explicit adds)

Before accepting a student into a TG (explicit or rule-derived eligibility), validate:

| Dimension | Required |
|---|---|
| TenantId | Match TeachingGroup.TenantId |
| AcademicYear | Match TG.AcademicYearId (via allocation / student active year rules) |
| CourseId | Match TG.CourseId |
| GroupId (curriculum) | Match TG.GroupId |
| SemesterId | Match TG.SemesterId |
| SubjectId | Match TG.SubjectId (StudentSubject and/or allocation) |

**StudentId + SubjectId alone is insufficient.**

College is implied by tenant/college context of the current user and student record; do not allow cross-college membership within a tenant if multi-college tenants exist — follow existing student/tenant scoping conventions.

---

## 7. Room capacity relationship

```text
TeachingGroup
    → membership → ResolvedStudentCount   (who)
    → ExpectedStudentCount                (plan)
    → MaxTeachingCapacity                 (teach ceiling)
TimetableEntry.RoomId
    → Room.Capacity                       (physical)
```

- Room capacity **never** creates Sections or TeachingGroups automatically.  
- Capacity-split TeachingGroups are **explicit domain operations**.  
- Scheduling compares room to the **placement size signal** defined in §8 (not to Section.MaximumStrength).

---

## 8. Validation matrix

### Severity definitions

| Level | Meaning |
|---|---|
| **OK** | No issue |
| **Warning** | Soft validation / conflict finding; Draft allowed; Publish may proceed unless policy hardens |
| **Error** | Blocks membership save or blocks Publish (as specified) |

Align with existing AI30 pattern: room capacity is primarily **soft** (`ROOM_CAPACITY` soft + conflict), not a create-time hard fail — unless Publish policy below says otherwise.

### Size signal used vs Room

When validating a TimetableEntry’s room:

```text
PlacementSize =
  ExpectedStudentCount
  if unset → ResolvedStudentCount
  if both unset/0 → Subject.ExpectedCapacity
  if still unset → skip room-size check
```

MaxTeachingCapacity is **not** a room input; it constrains membership/teaching.

### Cases

#### Case 1 — 10 students, Room 40

- Resolved=10, Expected unset/10, Max unset, Room=40  
- PlacementSize=10 ≤ 40 → **OK**  
- Publish: **OK**

#### Case 2 — 30 students, Room 40

- Same → **OK**

#### Case 3 — 60 students, Room 40 (single TG)

- PlacementSize=60 > 40 → **Warning** at schedule/validate; **Error on Publish** (hard publish gate for overcrowding)  
- Membership: **OK** (room not involved at membership-time)  
- Recommendation: capacity-split into multiple TGs (manual/assisted) — **not** auto Section creation

#### Case 4 — Split TG-01=40, TG-02=30

- Two TGs; each entry’s room checked independently  
- If TG-01 room 40 and PlacementSize 40 → **OK**  
- Mutually exclusive membership (§11) across the split set  

#### Case 5 — Expected=60, Resolved=45, Room=50

- PlacementSize uses Expected=60 > Room=50 → **Warning** schedule; **Error on Publish** (plan exceeds room)  
- Membership: Resolved 45 vs Max (unset) → **OK**  
- Optional advisory: Expected ≫ Resolved → **Warning** “expected exceeds resolved”

#### Case 6 — Expected=45, Resolved=60, Room=50

- PlacementSize=45 (expected set) ≤ 50 → room check **OK** on expected path  
- **Additionally:** Resolved=60 > Room=50 → **Warning** schedule; **Error on Publish** (actual cohort overcrowds room)  
- If MaxTeachingCapacity unset: Resolved vs Expected advisory **Warning**  
- Membership-time: if Max unset, allow 60 with **Warning**; if Max set see Case 7

#### Case 7 — MaxTeachingCapacity=40, Resolved=45

- Membership add that would make Resolved>Max → **Error** (membership-time)  
- Existing over-max state → **Error** on Validate/Publish until fixed  
- Room not required for this decision

### Summary table

| Decision | Authoritative inputs | Membership-time | Schedule-time | Publish |
|---|---|---|---|---|
| Who is in TG | Membership resolver | — | — | — |
| Teaching ceiling | MaxTeachingCapacity vs Resolved | Error if exceed | Error if exceed | Error |
| Room fit | PlacementSize & Resolved vs Room.Capacity | N/A | Warning if over | **Error if over** |
| Plan vs reality | Expected vs Resolved | Warning optional | Warning | Warning (not hard unless also room/max fail) |
| Create Sections from room | — | **Forbidden** | **Forbidden** | **Forbidden** |

**Design decision:** Room overcrowding is **soft while drafting**, **hard at Publish** for production safety. This is slightly stricter than today’s create-entry soft-only behavior and must be implemented explicitly in TG.3+ publish validation.

---

## 9. Capacity split semantics

1. Operator (or API `capacity-split`) creates N TeachingGroups under one SubjectAllocation.  
2. Each gets `Type = CapacitySplit`, shared `ExclusionGroupKey`, own `ExpectedStudentCount` / `MaxTeachingCapacity` / membership partition.  
3. Each TimetableEntry references exactly one TG.  
4. Room per entry validated independently.  
5. **Never** creates academic Sections.

---

## 10. Elective / lab semantics

| Kind | Membership | Capacity fields |
|---|---|---|
| Elective | StudentSubject (± explicit) with full scope validation | Expected/Max optional; Resolved derived |
| Laboratory | Hybrid/Explicit subset; often parent Section link | Max often = batch size; Exclusion vs other lab batches of same activity |
| Lecture vs Lab same subject | Different `InstructionalActivityKind` → not mutually exclusive |

---

## 11. Mutually-exclusive Teaching Group invariant

### VALID

Same student in Lecture TG and Lab TG for Computer Applications.

### INVALID

Same student in two CapacitySplit TGs of the same mutually exclusive set.

### Identification (KISS — APPROVED)

Persist optional:

```text
InstructionalActivityKind  // Lecture | Laboratory | Tutorial | Seminar | Other
ExclusionGroupKey          // string/guid nullable
```

**Rule:** A student may appear in at most one **Active/Locked** TeachingGroup that shares the same:

`(TenantId, SubjectAllocationId, ExclusionGroupKey)`

when `ExclusionGroupKey` is non-null.

Capacity-split API **must** assign the same ExclusionGroupKey to all siblings.

Lecture vs Lab: different `InstructionalActivityKind` and **null or different** ExclusionGroupKey → allowed together.

Do not build a general constraint solver beyond this key.

---

## 12. Full academic-scope validation

Reaffirmed: Tenant, AcademicYear, Course, Group (curriculum), Semester, Subject — plus college/tenant conventions — before membership acceptance. StudentSubject alone is not enough.

---

## 13. Examples (quick)

| Scenario | Expected | Max | Resolved | Room | Result |
|---|---|---|---|---|---|
| French 10 / room 40 | 10 | null | 10 | 40 | OK |
| CA 30 / room 40 | 30 | null | 30 | 40 | OK |
| CA 60 / room 40 single | 60 | null | 60 | 40 | Warn draft / Error publish |
| Split 40+30 | 40 & 30 | 40 & 30 | 40 & 30 | 40 & 40 | OK if exclusive membership |
| Expected 60 / Resolved 45 / Room 50 | 60 | null | 45 | 50 | Warn; Error publish (expected>room) + check resolved |
| Expected 45 / Resolved 60 / Room 50 | 45 | null | 60 | 50 | Error publish (resolved>room) |
| Max 40 / Resolved 45 | — | 40 | 45 | — | Error membership/validate |

---

## 14. Explicit architecture decisions

1. Replace `PlannedCapacity` with `ExpectedStudentCount` + `MaxTeachingCapacity`.  
2. `ResolvedStudentCount` is derived only.  
3. Room.Capacity remains Room master; independent of TG.  
4. PlacementSize preference: Expected → Resolved → Subject.ExpectedCapacity.  
5. Room overcrowd: warn in draft, **error on publish**.  
6. MaxTeachingCapacity breach: **error** at membership/validate/publish.  
7. Mutual exclusion via `ExclusionGroupKey` (+ activity kind for lecture/lab clarity).  
8. TeachingGroup must not mutate StudentSection.  
9. Elective validation uses full academic scope.

---

## 15. Rejected alternatives

| Alternative | Why |
|---|---|
| Keep single PlannedCapacity | Ambiguous |
| Persist ResolvedStudentCount | Dual SoT vs membership |
| Auto-create Sections when room too small | Forbidden |
| Hard-fail room on every entry save | Too rigid vs AI30 draft UX; publish gate enough |
| Infer exclusion from Type alone without key | Fragile for mixed Custom types |

---

## 16. Impact on AI-SCHED-TG.3

Schema must use:

- `ExpectedStudentCount` (nullable int)  
- `MaxTeachingCapacity` (nullable int)  
- `InstructionalActivityKind`  
- `ExclusionGroupKey` (nullable)  

Not `PlannedCapacity`. Update domain contract, DTOs, validation, and conflict rules accordingly. TG.2 docs remain historical; **2A Prompt 1 is authoritative for capacity naming**.

---

## 17. Open questions

| # | Question | Status |
|---|---|---|
| 1 | Should Expected>Room alone block publish when Resolved≤Room? | **RESOLVED:** Yes (Case 5) — plan must fit room at publish |
| 2 | Deep snapshot of resolved roster at Lock? | Deferred to TG.2 Prompt 7 default (Yes) — not capacity-blocking |

No unresolved capacity semantics remain.

---

## 18. Final readiness recommendation

Capacity and membership semantics are unambiguous for implementation.

### STATUS: **PASS**

Reasons: terminology split approved; persist vs derive clear; validation matrix covers required cases; mutual exclusion defined without overengineering; TG.3 impact explicit; no production changes made.

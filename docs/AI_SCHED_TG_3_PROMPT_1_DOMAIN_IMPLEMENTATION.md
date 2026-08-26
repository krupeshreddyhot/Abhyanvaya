# AI-SCHED-TG.3 Prompt 1 — Teaching Group Domain Implementation

**Workstream:** AI-SCHED-TG.3  
**Prompt:** 1 — Domain entities & domain model  
**Date:** 2026-08-17  

**STATUS: PASS**

---

## Summary

Implemented the approved Teaching Group **domain model** (entities, enums, pure invariants) with unit tests. No EF migration, DbContext registration, TimetableEntry FK wiring, APIs, or UI changes in this prompt.

---

## Files changed / added

| File | Action |
|---|---|
| `Abhyanvaya.Domain/Enums/Scheduling/TeachingGroupType.cs` | Added |
| `Abhyanvaya.Domain/Enums/Scheduling/TeachingGroupMembershipSource.cs` | Added |
| `Abhyanvaya.Domain/Enums/Scheduling/TeachingGroupStatus.cs` | Added |
| `Abhyanvaya.Domain/Enums/Scheduling/TeachingGroupActivityKind.cs` | Added |
| `Abhyanvaya.Domain/Enums/Scheduling/TeachingGroupMembershipInclusion.cs` | Added |
| `Abhyanvaya.Domain/Entities/Scheduling/TeachingGroup.cs` | Added |
| `Abhyanvaya.Domain/Entities/Scheduling/TeachingGroupSection.cs` | Added |
| `Abhyanvaya.Domain/Entities/Scheduling/TeachingGroupMembership.cs` | Added |
| `Abhyanvaya.Domain/Entities/Scheduling/TeachingGroupRules.cs` | Added |
| `Abhyanvaya.Application.UnitTests/Scheduling/TeachingGroupDomainTests.cs` | Added |
| `docs/AI_SCHED_TG_3_PROMPT_1_DOMAIN_IMPLEMENTATION.md` | Added |

---

## Entities added

| Entity | Role |
|---|---|
| `TeachingGroup` | Operational cohort under SubjectAllocation |
| `TeachingGroupSection` | TG ↔ Section link (not student membership) |
| `TeachingGroupMembership` | Explicit Include/Exclude operational membership |

---

## Entities / concepts reused

| Existing | Reuse |
|---|---|
| `BaseEntity` | Id, TenantId, audit, soft-delete |
| `SubjectAllocation` | Navigation + required SubjectAllocationId |
| `Section` / `StudentSection` / `StudentSubject` | Not duplicated; membership does not mutate them |
| `TimetableEntry` | Unchanged this prompt (TeachingGroupId deferred to migration/bridge prompts) |
| Scheduling enum style (`: byte`) | Followed |

---

## Fields on TeachingGroup

- Scope: AcademicYearId, CourseId, GroupId, SemesterId, SubjectId, SubjectAllocationId, optional SectionGroupId  
- Type, MembershipSource, Status, ActivityKind  
- Code?, Name, DisplayOrder  
- ExpectedStudentCount?, MaxTeachingCapacity?  
- ExclusionGroupKey?  
- EffectiveFrom / EffectiveTo?, Notes?  
- Collections: Sections, Memberships  
- BaseEntity audit/tenant/IsDeleted  

**Not persisted:** ResolvedStudentCount (static `ComputeResolvedStudentCount`)  
**Not on TG:** Room.Capacity / PlannedCapacity  

---

## Domain invariants

| Invariant | Enforcement |
|---|---|
| SectionDerived → exactly 1 Section | `TeachingGroupRules.ValidateSectionLinks` |
| CombinedSections → ≥ 2 Sections | same |
| Elective → no Section links | same |
| CapacitySplit → ExclusionGroupKey required | `ValidateCapacitySplitExclusionKey` |
| Locked/Archived mutation rules | `EnsureCanMutate` / `EnsureCanAttachToTimetableEntry` |
| Expected ≤ Max when both set | `SetCapacity` |
| Resolved ≤ Max | `EnsureResolvedWithinMaxCapacity` |
| Mutual exclusion by ExclusionGroupKey | `EnsureStudentNotInMutuallyExclusiveGroup` |
| Lecture (null key) + Lab (key) compatible | same (null key skips exclusion) |
| Cross-tenant membership | `EnsureSameTenant` |
| Lifecycle transitions | `TransitionTo` |

---

## Rejected duplicate concepts

- No PlannedCapacity  
- No TeachingGroup = Section / SectionGroup  
- No student master fields on membership  
- No Room.Capacity copy onto TG  
- No DbContext in entities  
- No TimetableSection entity changes  
- No unique SubjectAllocation → single TG assumption  

---

## Timetable behavior

**Unchanged.** `TimetableEntry.TeachingGroupId` not added in this prompt to avoid EF model drift before the dedicated migration/bridge prompts. Attachability is enforced via `TeachingGroup.EnsureCanAttachToTimetableEntry()` for domain tests.

---

## Tests

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| `TeachingGroupDomainTests` | **17** | **0** | **0** |

Covers required scenarios 1–15 (plus elective optional-section and Expected>Max).

---

## Deviations from TG.2A

| Item | Notes |
|---|---|
| `TeachingGroupActivityKind` | Added as approved companion to ExclusionGroupKey (Lecture vs Lab) |
| `Custom` type | Included as approved optional type |
| `TimetableEntry.TeachingGroupId` | Deferred (documented) — not a semantic deviation |
| EF / DbContext / migration | Explicitly out of scope for Prompt 1 |

---

## Architecture decisions

1. Domain-first entities with pure `TeachingGroupRules` (DIP / testability).  
2. Capacity naming matches 2A Prompt 1 exactly.  
3. Many TGs per SubjectAllocation by design (no unique constraint in domain).  
4. Mutual exclusion is key-based, not Type-global.  

---

## Next prompts

- **TG.3 Prompt 2+:** EF configurations + migration (`SchedulingTeachingGroup*`) + optional `TimetableEntry.TeachingGroupId`  
- Then membership resolver, legacy bridge, attendance  

---

## Confirmation

No database migration was created. No production timetable/attendance/API/UI behavior changed.

# AI-SCHED-TG.2 — Final Architecture Decision & Implementation Readiness

**Workstream:** AI-SCHED-TG.2  
**Prompt:** 8 — Architecture review & readiness gate  
**Date:** 2026-08-17  

**Reviewed against:** codebase (Scheduling, Academic bridges, Attendance resolver), ADL/docs AI30/AI29, tenant BaseEntity, RBAC PermissionKeys, EF configurations, PostgreSQL conventions.

**No production code/schema/API/UI changes in AI-SCHED-TG.2.**

---

## Architecture consistency checks

| Check | Result |
|---|---|
| Duplicated domain ownership | Avoided — TG in Scheduling; Section remains Academic |
| Section/TG confusion | Explicit separation in contract |
| Duplicate student membership | SectionDerived dynamic; explicit only when needed |
| Tenant leakage | TenantId + filters required |
| Cross-scope membership | Forbidden by invariant |
| Hidden migration for test TT data | **Rejected** (pre-prod disposable) |
| Unnecessary compatibility forever | TimetableSection sync transitional only |
| Timetable redesign | Designer reused; entry gains FK only |
| Room→Section creation | Forbidden |
| API/domain boundary | DTO/services only |
| Direct EF exposure | Forbidden |
| Governance bypass | Forbidden |
| Attendance regression | Legacy preserved; Timetable mode extended |
| Circular deps | Allocation → TG → Entry (one way) |
| Naming | `SchedulingTeachingGroup*` tables; Type enums |
| Lifecycle | TG Draft/Active/Locked/Archived ↔ Timetable statuses |
| Audit | BaseEntity + events |

**Inconsistencies discovered (current product, not TG.2 defects):**

1. Operational cohort today is side-mapped via TimetableSection while entry is allocation-centric.
2. Attendance Timetable mode cannot resolve elective-only rosters.
3. ROOM_CAPACITY uses Subject.ExpectedCapacity, not cohort planned size.
4. No lab-batch / capacity-split cohort construct.

These are **addressed by the approved TG design**, not by weakening existing modules.

---

## A. APPROVED decisions

1. Introduce **TeachingGroup** as scheduling-layer aggregate.
2. **SubjectAllocationId required** on TeachingGroup.
3. **TimetableEntry.TeachingGroupId required** in clean production model.
4. Hybrid membership: dynamic for section/subject-derived; explicit for subsets/labs/splits.
5. **Never** auto-create Sections from room capacity.
6. Students may join many TGs across subjects; same-subject CapacitySplit double-membership default **forbidden**.
7. Membership **immutable** when referencing timetable is Published/Locked.
8. Pre-production: **no backfill** of disposable timetable test data.
9. TimetableSection becomes **compatibility sync** from TeachingGroupSection.
10. Dedicated permissions `Scheduling.TeachingGroup.View/Manage`.
11. Attendance: Entry → TG → students; Legacy fallback unchanged.
12. PlannedCapacity drives room validation when TG present.

---

## B. REJECTED alternatives

- TeachingGroup = Section / SectionGroup / SubjectAllocation  
- Nullable TeachingGroupId forever  
- Roster JSON on TimetableEntry  
- Infer cohort from room/name/code  
- Parallel Timetable/Governance/Attendance stacks  
- Migrating disposable test timetables as first-class requirement  

---

## C. Remaining open questions

| # | Question | Default if Architect silent |
|---|---|---|
| 1 | Freeze StudentSubject roster snapshot at first Publish? | **Yes** |
| 2 | Interim permission fallback before key provisioning? | Map Manage→`Scheduling.Timetable.Manage` |
| 3 | Hard-block Publish on zero members always vs only AttendanceMandatory? | **Only when AttendanceMandatory** |
| 4 | Clone timetable: reuse vs deep-copy TGs? | **Reuse** Active TGs |

These defaults are sufficient for implementation; not fundamental schema blockers.

---

## D–N. Final contracts (summary)

### D. TeachingGroup entity

Tenant-scoped; AcademicYear/Course/Group/Semester/Subject; required SubjectAllocationId; Type; MembershipSource; Status; Code/Name/DisplayOrder; PlannedCapacity/MaxCapacity?; EffectiveFrom/To; Notes; BaseEntity audit/soft-delete.

### E. TeachingGroupMembership

Explicit Include/Exclude rows with effective dating; not used to mirror full SectionDerived rosters.

### F. TimetableEntry relationship

Required `TeachingGroupId`; allocation must match; Restrict delete.

### G. Section relationship

Via `TeachingGroupSection`; never replaces StudentSection.

### H. SubjectAllocation relationship

1 allocation → many TeachingGroups (splits/labs).

### I. Attendance relationship

Resolver reads TG membership; SectionIds from TeachingGroupSection; Legacy intact.

### J. Lifecycle

Draft → Active → Locked → Archived (coordinated with Timetable Published/Locked).

### K. Governance

Membership edits blocked when published; changes require unlock/new version per existing TT governance.

### L. Authorization

`Scheduling.TeachingGroup.View/Manage` + existing TT Approve/Publish; tenant filters mandatory.

### M. Database

`SchedulingTeachingGroup`, `SchedulingTeachingGroupSection`, `SchedulingTeachingGroupMembership` + `SchedulingTimetableEntry.TeachingGroupId`.

### N. API boundary

`api/scheduling/teaching-groups` + timetable DTO field; Application services; no EF leakage.

---

## O. Implementation sequence (AI-SCHED-TG.3 onward)

1. **TG.3** — Domain entities + EF configurations + migration (clean schema) + unit tests for invariants.  
2. **TG.4** — Application services: CRUD, membership resolver, derive/combine/split.  
3. **TG.5** — API controllers + auth policies + validation.  
4. **TG.6** — TimetableEntry integration + TimetableSection sync + soft/conflict capacity.  
5. **TG.7** — AttendanceSessionResolver extension + regression.  
6. **TG.8** — UI: TG admin + designer picker.  
7. **TG.9** — Governance lock hooks + acceptance/Architecture Guard.

---

## IMPLEMENTATION READINESS

# **READY**

Fundamental domain and schema ambiguity is resolved. Remaining items are operational defaults documented in §C.

---

## Files inspected (TG.2 aggregate)

Domain: `TimetableEntry`, `Timetable`, `SubjectAllocation`, `Room`, `TimetableSection`, `SectionGroup`, `SectionGroupMember`, `StudentSubject`, `BaseEntity`, `TimetableStatus`, `ScheduleVersionStatus`, `PermissionKeys`  
Application: `AttendanceSessionResolver`, `TimetableSoftValidationService`, room conflict rules  
Infrastructure: `TimetableEntryConfiguration`, ApplicationDbContext scheduling sets  
Docs: AI_SCHED_TG_1 assessment, AI30 attendance/subject allocation, AI29 section docs  
UI routes: scheduling hub / timetable designer / subject allocations (read-only inspection prior)

---

## Confirmation

**AI-SCHED-TG.2 produced design artifacts only. No production code, schema, API, or UI was changed.**

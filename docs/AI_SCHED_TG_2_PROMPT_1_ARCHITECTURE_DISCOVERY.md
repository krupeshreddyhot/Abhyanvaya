# AI-SCHED-TG.2 Prompt 1 — Architecture Discovery & Teaching Group Domain Boundary

**Workstream:** AI-SCHED-TG.2  
**Prompt:** 1 — Architecture discovery (design only)  
**Date:** 2026-08-17  
**Depends on:** `docs/AI_SCHED_TG_1_EXISTING_SCHEDULING_TEACHING_GROUP_ASSESSMENT.md`  

**No production code, schema, API, or UI was changed.**

---

## CURRENT STATE

### A. Current scheduling domain model

Abhyanvaya Scheduling (AI30) is a tenant-scoped bounded context:

| Layer | Location |
|---|---|
| Domain | `Abhyanvaya.Domain/Entities/Scheduling/*`, `Enums/Scheduling/*` |
| Academic bridge | `TimetableSection`, `SectionGroup`, `SectionGroupMember`, `StudentSection` |
| Application | `Abhyanvaya.Application/Scheduling/*` |
| API | `Abhyanvaya.API/Controllers/Scheduling/*` |
| UI | `abhyanvaya-ui/src/pages/setup/scheduling/*` |
| Persistence | `SchedulingTimetable`, `SchedulingTimetableEntry`, etc. |

Core aggregates: AcademicYear/Term, WorkingDay, Holiday, Campus/Building/Floor/Room, TimeSlot/TimeSlotSet/Template, SubjectAllocation, Timetable/TimetableEntry, ScheduleVersion, governance/approval/history, conflict engine, optimization.

### B. Current relationships

```text
SubjectAllocation
  → AcademicYear, Subject, Staff, Course, Group (curriculum), Semester, Department
  → PreferredRoom?, WeeklyHours, LabRequired, attendance flags
  ✗ no Section, no student roster

TimetableEntry
  → Timetable, TimeSlot, SubjectAllocation, Staff, Room
  → denormalized CourseId, GroupId, SemesterId, SubjectId, DepartmentId
  ✗ no SectionId, no TeachingGroupId

TimetableSection (junction)
  → TimetableId, TimetableEntryId?, SectionId

SectionGroup / SectionGroupMember
  → combined academic sections (A+B+C) with membership history

StudentSection → administrative Section membership
StudentSubject → simple subject enrollment
Room.Capacity / Subject.ExpectedCapacity → soft + conflict ROOM_CAPACITY
```

### C. Where the model incorrectly assumes Section ≈ Teaching Group

| Assumption in practice | Why it is wrong |
|---|---|
| Operational class ≈ mapped Sections via TimetableSection | Electives may have no Section |
| Combined teaching only as multi-Section | Valid, but incomplete for electives/labs/splits |
| Attendance Timetable mode enriches SectionIds only | Misses StudentSubject-only cohorts |
| Capacity pressure has no cohort split construct | Tempts colleges to create fake Sections |
| Designer places SubjectAllocation without explicit cohort | Membership reconstructed from side maps / UI |

`TimetableEntry` itself does **not** store SectionId — the incorrect assumption is in **product/ops usage**, not a literal FK.

### D. Reusable functionality

- SubjectAllocation (faculty ↔ subject ↔ academic scope)
- Timetable Designer, projections, soft validation, conflicts, optimization
- ScheduleVersion + approval/publish/freeze/archive/history/clone
- TimetableSection / SectionGroup for **section-linked** cases
- StudentSection, StudentSubject
- Room capacity validation patterns
- AttendanceSessionResolver Legacy + Timetable modes (extend, do not replace)
- BaseEntity tenant/audit/soft-delete; Scheduling permission keys

### E. Must NOT duplicate

Timetable Designer, Governance, Validation, Optimization, Subject Allocation, Section Management/Allocation, Campus/Room/TimeSlot masters, Attendance APIs/schema.

### F. Attendance dependencies that must remain intact

- Legacy: Course → Group → Semester → Subject → Period
- Timetable mode optional enrichment (`AttendanceSessionResolver`)
- Additive SectionIds from TimetableSections
- Manual override; no forced timetable usage
- Doc: `docs/AI30_PHASE2B_ATTENDANCE_RESOLUTION.md`

### G. Tenant / security / audit conventions

- `BaseEntity`: `Id`, `TenantId`, `CreatedDate/By`, `UpdatedDate/By`, `IsDeleted`
- Query filters / explicit `TenantId == currentUser.TenantId`
- Scheduling permissions (`Scheduling.View/Manage`, `Scheduling.Timetable.*`, Approve/Publish/…)
- Soft delete; Restrict deletes on FKs for catalog resources; Cascade Timetable→Entries

### H. Partial TeachingGroup equivalents

| Entity | Partial? | Gap |
|---|---|---|
| TimetableSection | Section bridge only | No elective/lab/split |
| SectionGroup | Combined sections | Section-only |
| SubjectAllocation | Teaching assignment | No membership |
| TimetableEntry | Placement | No cohort |
| Curriculum Group | Specialization | Not teaching cohort |

**No TeachingGroup entity exists.**

### I. Assumptions that block required scenarios

| Scenario | Blocking assumption |
|---|---|
| Elective groups | Cohort requires Section |
| Laboratory batches | No subset-of-Section cohort |
| Capacity-based groups | No multi-cohort under one subject without Sections |
| Combined sections | Partially OK via SectionGroup |
| Section subsets | No subset membership |
| Subject-specific cohorts | Student may only be “in Section” for all subjects |

### J. Proposed TeachingGroup bounded context

```text
Scheduling bounded context (extend, do not fork)
  SubjectAllocation
       ↓ owns teaching assignment
  TeachingGroup (+ membership)
       ↓ owns operational cohort
  TimetableEntry
       ↓ placement (when/where)
  Attendance resolution (read TG membership additively)
```

Academic Section context remains separate (StudentSection, section capacity, AI29 allocation).

---

## DESIRED STATE

- Explicit TeachingGroup as authoritative operational cohort for timetable slots.
- Types covering SectionDerived, Combined, Elective, Lab, CapacitySplit, StudentSubset.
- TimetableEntry references TeachingGroupId.
- Room capacity constrains placement; never creates Sections.
- Students may belong to many Teaching Groups across subjects.
- Attendance resolves students via TeachingGroup when timetable mode applies; Legacy unchanged.

---

## GAPS

1. No TeachingGroup / membership tables.
2. No TimetableEntry → TeachingGroup FK.
3. Attendance resolves sections only, not explicit student rosters.
4. No capacity-split cohort model.
5. No lab-batch cohort model.
6. TimetableSection is write-side cohort proxy (insufficient).

---

## ARCHITECTURAL DECISIONS REQUIRED

| # | Decision | Proposed default (for Prompt 2+) |
|---|---|---|
| 1 | New aggregate vs extend SectionGroup | **New TeachingGroup** in Scheduling |
| 2 | Membership materialization | Hybrid: section-derived dynamic; subsets explicit |
| 3 | TimetableSection fate | Compatibility projection / bridge; TG authoritative |
| 4 | SubjectAllocation required? | **Yes** on TeachingGroup |
| 5 | Clean slate (pre-prod) | **Yes** — no disposable-data backfill architecture |
| 6 | Membership after publish | Locked when linked timetable Published/Locked |

---

## Files inspected (representative)

- `TimetableEntry.cs`, `Timetable.cs`, `SubjectAllocation.cs`, `Room.cs`
- `TimetableSection.cs`, `SectionGroup.cs`, `SectionGroupMember.cs`, `StudentSubject.cs`, `BaseEntity.cs`
- `TimetableStatus.cs`, `ScheduleVersionStatus.cs`, `PermissionKeys.cs`
- `AttendanceSessionResolver.cs`, `TimetableEntryConfiguration.cs`
- `TimetableSoftValidationService.cs`, `RoomConflictRules.cs`
- `docs/AI_SCHED_TG_1_*`, `docs/AI30_PHASE2B_ATTENDANCE_RESOLUTION.md`, `docs/AI30.5_SUBJECT_ALLOCATION.md`

---

## Confirmation

**No production code, database, API, or UI was modified.**

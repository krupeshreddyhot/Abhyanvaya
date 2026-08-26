# AI-SCHED-TG.2 Prompt 3 — Teaching Group Clean Schema Design

**Workstream:** AI-SCHED-TG.2  
**Prompt:** 3 — Schema design (design only)  
**Date:** 2026-08-17  

**Pre-production:** disposable timetable test data — **no backfill/migration scaffolding for legacy TT rows**. Clean production model.

**No production implementation in this prompt.**

---

## Logical ER model

```text
SubjectAllocation 1───* TeachingGroup
TeachingGroup 1───* TeachingGroupSection *───1 Section
TeachingGroup 1───* TeachingGroupMembership *───1 Student
TeachingGroup 1───* TimetableEntry
Timetable 1───* TimetableEntry
(optional) TeachingGroup N───0..1 SectionGroup  (reference only for CombinedSections)
```

`TimetableSection`: retained as **read/compatibility projection** optionally synced from `TeachingGroupSection` for existing APIs; **not** the write-side source of truth in the clean model.

---

## Table: `SchedulingTeachingGroup`

| Column | Type | Notes |
|---|---|---|
| Id | int PK identity | BaseEntity |
| TenantId | int NOT NULL | indexed |
| AcademicYearId | int NOT NULL | FK Restrict |
| CourseId | int NOT NULL | FK Restrict |
| GroupId | int NOT NULL | curriculum Group |
| SemesterId | int NOT NULL | FK Restrict |
| SubjectId | int NOT NULL | FK Restrict |
| SubjectAllocationId | int NOT NULL | FK Restrict |
| SectionGroupId | int NULL | optional link when CombinedSections |
| Type | smallint/byte NOT NULL | TeachingGroupType |
| MembershipSource | smallint/byte NOT NULL | MembershipSource |
| Status | smallint/byte NOT NULL | TeachingGroupStatus |
| Code | varchar(50) NULL | |
| Name | varchar(200) NOT NULL | |
| DisplayOrder | int NOT NULL DEFAULT 0 | |
| PlannedCapacity | int NOT NULL DEFAULT 0 | |
| MaxCapacity | int NULL | soft operational cap |
| EffectiveFrom | date NOT NULL | |
| EffectiveTo | date NULL | |
| Notes | varchar(1000) NULL | |
| CreatedDate | timestamptz NOT NULL | |
| CreatedBy | int NULL | |
| UpdatedDate | timestamptz NULL | |
| UpdatedBy | int NULL | |
| IsDeleted | boolean NOT NULL DEFAULT false | |

**Unique:** `(TenantId, AcademicYearId, Code)` WHERE Code IS NOT NULL AND IsDeleted = false  
**Unique:** `(TenantId, SubjectAllocationId, Name)` WHERE IsDeleted = false (prevent duplicate names per allocation)  
**Indexes:**  
- `(TenantId, AcademicYearId, CourseId, GroupId, SemesterId, SubjectId)`  
- `(TenantId, SubjectAllocationId, Status)`  
- `(TenantId, Status, IsDeleted)`

---

## Table: `SchedulingTeachingGroupSection`

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| TenantId | int NOT NULL | |
| TeachingGroupId | int NOT NULL | FK Cascade |
| SectionId | int NOT NULL | FK Restrict |
| IsPrimary | boolean NOT NULL DEFAULT false | SectionDerived primary |
| CreatedDate / CreatedBy / Updated* / IsDeleted | audit | BaseEntity |

**Unique:** `(TenantId, TeachingGroupId, SectionId)` WHERE IsDeleted = false  
**Index:** `(TenantId, SectionId)`

Required when Type ∈ {SectionDerived, CombinedSections} (and often Laboratory/StudentSubset as parent section).

---

## Table: `SchedulingTeachingGroupMembership`

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| TenantId | int NOT NULL | |
| TeachingGroupId | int NOT NULL | FK Cascade |
| StudentId | int NOT NULL | FK Restrict |
| Inclusion | smallint NOT NULL | Include=1, Exclude=2 (for Hybrid) |
| EffectiveFrom | date NOT NULL | |
| EffectiveTo | date NULL | |
| IsCurrent | boolean NOT NULL DEFAULT true | |
| CreatedDate / CreatedBy / Updated* / IsDeleted | audit | |

**Unique:** `(TenantId, TeachingGroupId, StudentId, Inclusion)` WHERE IsCurrent AND IsDeleted = false  
**Index:** `(TenantId, StudentId, IsCurrent)`  
**Index:** `(TenantId, TeachingGroupId, IsCurrent)`

Used when MembershipSource ∈ {ExplicitStudents, Hybrid} and for Elective/Laboratory/CapacitySplit/StudentSubset/Custom as needed.

**Do not** duplicate all section students into membership for pure SectionDerived — resolve dynamically from `StudentSection`.

---

## TimetableEntry integration

Add to `SchedulingTimetableEntry`:

| Column | Type | Notes |
|---|---|---|
| TeachingGroupId | int NOT NULL | FK Restrict to SchedulingTeachingGroup |

**Clean production model:** required. Pre-prod disposable data → recreate timetables under new model; no compatibility nullability required for go-live.

Indexes: `(TenantId, TeachingGroupId)`, keep existing day/slot/staff/room indexes.

SubjectAllocationId remains on entry (denormalization / validation: must match TeachingGroup.SubjectAllocationId).

---

## TimetableSection fate

| Role | Decision |
|---|---|
| Authoritative cohort | **TeachingGroup** (+ sections/membership) |
| TimetableSection | **Compatibility/helper**: keep table for existing `GET/PUT /api/timetable/{id}/sections` and AttendanceSessionResolver until cutover; sync from TeachingGroupSection on write |
| Long term | Prefer read API that returns sections from TeachingGroup; TimetableSection may become obsolete |

---

## Delete behavior

| Action | Rule |
|---|---|
| Soft-delete TeachingGroup | Allowed if no TimetableEntry references **or** only Draft timetable entries (implementation policy); else Archive |
| Hard-delete | Prefer soft-delete only |
| Delete membership | Soft-delete / end-date; never touches StudentSection |
| Delete SubjectAllocation | Restrict if TeachingGroups exist |
| Cascade | TeachingGroup → Sections links & Memberships |

---

## Immutability after publication

When any referencing Timetable is `Published` or `Locked`:

- TeachingGroup.Status → `Locked`
- Membership/section-link mutations **forbidden** (require Unlock + new ScheduleVersion workflow)

Historical entries retain TeachingGroupId forever (Restrict FK).

---

## Concurrency

Follow existing patterns: rely on UpdatedDate optimistic checks in application services where used; optional `xmin`/rowversion only if platform standard expands — **not required** for TG v1 if peers lack it.

---

## EF Core mapping recommendations

- Configurations under `Infrastructure/Persistence/Configurations/Scheduling/`
- Tables prefixed `Scheduling*` like peers
- Enums as byte/smallint conversions
- Global tenant query filter via existing soft-delete + tenant conventions
- Navigation: TeachingGroup.Members, TeachingGroup.Sections, TeachingGroup.SubjectAllocation
- TimetableEntry.TeachingGroup navigation

PostgreSQL: `timestamp with time zone` for audit DateTime; `date` for Effective*; `boolean` for flags.

---

## Rejected alternatives

| Alternative | Why |
|---|---|
| Store all students always | Duplicates StudentSection; drift risk |
| Only TimetableSection | Cannot model non-section cohorts |
| TeachingGroupId nullable forever | Weakens authority; unnecessary pre-prod |
| Embed roster JSON on entry | Non-queryable; no integrity |
| FK TeachingGroup → Room | Room is placement, not cohort |

---

## Confirmation

**No production schema/migrations/code implemented.**

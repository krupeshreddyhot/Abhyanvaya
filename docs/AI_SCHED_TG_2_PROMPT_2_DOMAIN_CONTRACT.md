# AI-SCHED-TG.2 Prompt 2 — Teaching Group Domain Contract

**Workstream:** AI-SCHED-TG.2  
**Prompt:** 2 — Domain contract (design only)  
**Date:** 2026-08-17  

**No production code, schema, API, or UI was changed.**

---

## Domain definition

A **Teaching Group** is an operational scheduling construct that defines **which students are taught together** for a given subject assignment (SubjectAllocation) in an academic scope.

It is **not**:

- an academic Section
- a curriculum Group (specialization)
- a SubjectAllocation
- a TimetableEntry
- a Room

Same student may belong to different Teaching Groups for different subjects. This is expected and valid.

---

## Entity responsibilities

| Entity | Responsibility |
|---|---|
| `TeachingGroup` | Identity, type, scope, SubjectAllocation link, planned capacity, lifecycle, display |
| `TeachingGroupSection` | Section associations for SectionDerived / CombinedSections |
| `TeachingGroupMembership` | Explicit student membership rows (subsets, electives, labs, splits) |
| `SubjectAllocation` | Who teaches what in which Course/Group/Semester (unchanged ownership) |
| `TimetableEntry` | Places a TeachingGroup in day/slot/room (future FK) |
| `Section` / `StudentSection` | Academic membership — never overwritten by TG |

---

## 1–15. Contract fields

| # | Concern | Contract |
|---|---|---|
| 1 | Identity | `Id` (int, BaseEntity), optional `Code` (tenant-scoped unique with AcademicYear) |
| 2 | Tenant | `TenantId` required; all children same tenant |
| 3 | Academic scope | `AcademicYearId`, `CourseId`, `GroupId` (curriculum), `SemesterId` — must match SubjectAllocation |
| 4 | Subject | `SubjectId` denormalized from allocation (consistency/query) |
| 5 | SubjectAllocation | **Required** `SubjectAllocationId` |
| 6 | Group type | `TeachingGroupType` enum (below) |
| 7 | Membership model | Hybrid — see Prompt 4 |
| 8 | Membership source | `MembershipSource` enum (below) |
| 9 | Status/lifecycle | `TeachingGroupStatus`: Draft → Active → Locked → Archived |
| 10 | Capacity | `PlannedCapacity` (int, operational); optional `MaxCapacity` soft cap; **not** Room.Capacity |
| 11 | Display | `Name` required; `Code` optional; UI never parses codes for membership |
| 12 | Ordering | `DisplayOrder` int |
| 13 | Effective dates | `EffectiveFrom` DateOnly; `EffectiveTo` DateOnly? |
| 14 | Audit | BaseEntity Created/Updated fields |
| 15 | Soft-delete | `IsDeleted`; soft-delete preferred; hard-delete blocked if referenced by TimetableEntry |

---

## Enums

### `TeachingGroupType`

| Value | Meaning |
|---|---|
| `SectionDerived` | One academic Section’s current students |
| `CombinedSections` | Union of multiple Sections (may reference SectionGroup) |
| `Elective` | Subject/elective cohort (typically StudentSubject-based or explicit) |
| `Laboratory` | Lab batch (usually subset of a Section or explicit roster) |
| `CapacitySplit` | Cohort created to fit teaching/room capacity |
| `StudentSubset` | Explicit subset of a Section or pool |
| `Custom` | Explicit roster with no stronger type |

### `MembershipSource`

| Value | Meaning |
|---|---|
| `Section` | Resolve via `StudentSection` for linked section(s) |
| `CombinedSections` | Union of multiple sections’ current students |
| `StudentSubject` | Students enrolled in Subject (filtered to academic scope) |
| `ExplicitStudents` | Materialized `TeachingGroupMembership` rows |
| `Hybrid` | Section/SubjectSubject base **plus** explicit include/exclude |

### `TeachingGroupStatus`

| Value | Aligns with |
|---|---|
| `Draft` | Editable definition |
| `Active` | Usable in draft/review timetables |
| `Locked` | Membership frozen (published/locked timetable references) |
| `Archived` | Soft end-of-life |

---

## Invariants

1. TeachingGroup belongs to exactly one tenant.
2. TeachingGroup academic scope must equal its SubjectAllocation scope.
3. TeachingGroup has exactly one SubjectAllocation (and thus one Subject).
4. Student membership cannot cross tenants or academic scopes.
5. A student may belong to multiple TeachingGroups for **different** subjects.
6. Duplicate active membership of the same student in the same TeachingGroup is forbidden.
7. Membership changes **never** mutate `StudentSection`.
8. TimetableEntry must reference TeachingGroupId (production model) — must not reconstruct cohort from room/name/text.
9. Room capacity never creates/modifies Sections.
10. `PlannedCapacity` ≥ 0; when > 0 used for ROOM_CAPACITY checks against assigned Room.
11. Zero students: **allowed** in Draft; placement onto Published timetable should warn/block per validation policy (Ready/Active with zero students → soft warning; publish may hard-block if AttendanceMandatory on allocation).

---

## Examples

| Scenario | Type | Source | PlannedCapacity |
|---|---|---|---|
| SCA-01 / Financial Accounting, 55 | SectionDerived | Section | 55 |
| French, 10, no Section | Elective | StudentSubject or Explicit | 10 |
| CA, 30, room 40 | SectionDerived or CapacitySplit | Section | 30 |
| CA, 70 → 40+30 | CapacitySplit ×2 | Explicit or Hybrid | 40 / 30 |
| A+B combined | CombinedSections | CombinedSections | sum |
| Lab batches 30+30 from Section 60 | Laboratory ×2 | Explicit/Hybrid | 30 / 30 |

---

## Rejected alternatives

| Alternative | Why rejected |
|---|---|
| TeachingGroup = Section | Violates academic vs operational boundary |
| TeachingGroup = SubjectAllocation | No membership / multi-cohort |
| TeachingGroup = SectionGroup only | Cannot model electives/labs/splits |
| Auto-create Sections from room capacity | Forbidden |
| Infer cohort from room/subject name | Forbidden |

---

## Unresolved decisions (closed in Prompt 8)

Deferred wording only; defaults proposed:

- Exact publish hard-block on zero members → default **warn in Draft/Active; hard-block on Publish if AttendanceMandatory**.
- Whether Elective defaults to StudentSubject auto-refresh → default **yes until Locked**.

---

## Confirmation

**No production code, database, API, or UI was modified.**

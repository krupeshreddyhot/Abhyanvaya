# AI-SCHED-CATALOG/TIMETABLE — Prompt 1  
# Architecture Discovery & Change Impact Analysis

**Workstream:** AI-SCHED-CATALOG/TIMETABLE  
**Prompt:** 1 — Architecture Discovery & Change Impact Analysis  
**Date:** 2026-08-21  
**Type:** **READ-ONLY DISCOVERY** — no production behavior changed  
**Final status: PASS — discovery complete; no production behavior changed**

---

## 1. Purpose

Establish the **current** Abhyanvaya academic catalog and timetable implementation before any architectural modification. This document is the baseline for subsequent prompts. **No code, schema, migration, UI, test, or configuration changes were made in this prompt.**

---

## 2. Authoritative ADL / frozen references

Formal ADL volumes 00–12 are not present as a single library tree. The following repository documents act as the authoritative Architecture Documentation Library for this discovery:

| Area | Key documents |
| --- | --- |
| Academic hierarchy / Program | `docs/AI29_1A_ACADEMIC_HIERARCHY.md`, `docs/AI29_1A_PROGRAM_MANAGEMENT.md`, `docs/AI29_1A_DATABASE_DESIGN.md`, `docs/AI29_1D_PROMPT_4B_PROGRAM_HIERARCHY_CONSISTENCY.md`, `docs/AI29_1D_24_COURSE_PROGRAM_ASSIGNMENT.md` |
| Context hierarchy (historical) | `docs/AI22.5_HIERARCHY_ARCHITECTURE.md` |
| Teaching Group (frozen) | `docs/AI_SCHED_TG_2_FINAL_ARCHITECTURE_DECISION.md`, TG.3–TG.6 series, `docs/AI_SCHED_TG_4A_*` (SoT / projection) |
| Capacity / Publish (frozen) | `docs/AI_SCHED_CAP_PROMPT_1_…` through `docs/AI_SCHED_CAP_PROMPT_11_…` |
| Scheduling catalog | `docs/AI30.7_SCHEDULING_CATALOG.md` |

**Frozen TG / CAP rules that must not be weakened:**

- `TeachingGroupSection` = section membership SoT; `TimetableSection` = projection; `TimetableSectionProjector` = sole writer (no `SaveChanges`)
- Explicit `TimetableEntry.TeachingGroupId`; no SA→TG inference / auto-create
- Assign/clear TG via dedicated APIs; single SaveChanges with projection
- Server-authoritative membership, capacity, conflict, publish readiness / publish gate
- No Attendance schema mutation / no StudentSection mutation from Scheduling
- Tenant isolation + existing authorization policies

---

## 3. Finalized target architecture (from prompt)

```text
College
  |
  +-- Department
       |
       +-- Program (OPTIONAL institution-level feature)
       |     |
       |     +-- Course
       |
       +-- Course (when Programs are disabled)
             |
             +-- Group
                   |
                   +-- Semester
                         |
                         +-- Teaching Group
                               |
                               +-- Section / operational cohort
```

| Rule | Target |
| --- | --- |
| Programs optional | When disabled: Department → Course → Group → Semester → Teaching Group |
| Programs enabled | Department → Program → Course → Group → Semester → Teaching Group |
| Program ownership | Program **must belong to Department** |
| Student hierarchy | Course → Group → Semester |
| Operational semester | Group-specific for student use |
| Scheduling cohort | TeachingGroup (not a duplicate independent `SectionId` on TimetableEntry) |

---

## 4. Current architecture (as implemented)

### 4.1 Academic / catalog tree (AI29.1A)

```text
College
  |
  +-- Program?          (EnablePrograms; Program.CollegeId — NOT DepartmentId)
  |     |
  |     +-- Course.ProgramId?
  |
  +-- Course            (when Programs off, ProgramId forced null)
        |
        +-- Group (CourseId)
              |
              +-- Semester (CourseId, GroupId?)   ← GroupId nullable = course-wide
                    |
                    +-- Section (Course/Group/Semester + AcademicYear…)
```

**Department** today is a **parallel Catalog SSOT** under College (used heavily by Scheduling / Staff), **not** the parent of Program or Course in the academic FK tree.

```text
College
  └── Department          (Catalog SSOT)
        └── (referenced by SubjectAllocation.DepartmentId, TimetableEntry.DepartmentId, StaffDepartment…)
```

### 4.2 Scheduling / operational cohort (AI-SCHED-TG — frozen)

```text
SubjectAllocation (DepartmentId + CourseId + GroupId + SemesterId + Subject + Staff + AcademicYear)
  └── TeachingGroup* (many per SA; Course/Group/Semester denormalized)
        └── TeachingGroupSection → Academic Section (SoT)

TimetableEntry
  ├── SubjectAllocationId
  ├── TeachingGroupId?          (explicit; optional; legacy null allowed)
  ├── DepartmentId / CourseId / GroupId / SemesterId / SubjectId (denormalized)
  └── NO SectionId on entry
        └── TimetableSection* (projection from TeachingGroupSection)
```

---

## 5. Discovery findings A–L

### A. Current entities and relationships

| Entity | Path | Key relationships |
| --- | --- | --- |
| **Department** | `Abhyanvaya.Domain/Entities/Department.cs` | `CollegeId` → College. No Program/Course children. |
| **Program** | `Abhyanvaya.Domain/Entities/Academic/Program.cs` | `CollegeId` only. **No `DepartmentId`.** |
| **Course** | `Abhyanvaya.Domain/Entities/Course.cs` | `ProgramId?` (null when Programs disabled / unassigned). **No `DepartmentId`.** |
| **Group** | `Abhyanvaya.Domain/Entities/Group.cs` | Required `CourseId`. |
| **Semester** | `Abhyanvaya.Domain/Entities/Semester.cs` | Required `CourseId`; optional `GroupId?` (null = course-wide). |
| **Section** | `Abhyanvaya.Domain/Entities/Academic/Section.cs` | `CollegeId`, `AcademicYearId`, `CourseId`, `GroupId`, `SemesterId`, optional parent/section-group. |
| **TeachingGroup** | `Abhyanvaya.Domain/Entities/Scheduling/TeachingGroup.cs` | `AcademicYearId`, `CourseId`, `GroupId`, `SemesterId`, `SubjectId`, **`SubjectAllocationId`**; sections via `TeachingGroupSection`. |
| **Student** | `Abhyanvaya.Domain/Entities/Student.cs` | Required `CourseId`, `GroupId`, `SemesterId`. No Department/Program/Section FK. |
| **SubjectAllocation** | `Abhyanvaya.Domain/Entities/Scheduling/SubjectAllocation.cs` | `AcademicYearId`, `SubjectId`, `StaffId`, `CourseId`, `GroupId`, `SemesterId`, **`DepartmentId`**. |
| **TimetableEntry** | `Abhyanvaya.Domain/Entities/Scheduling/TimetableEntry.cs` | `TimetableId`, `TimeSlotId`, `SubjectAllocationId`, **`TeachingGroupId?`**, Staff/Room, denorm Dept/Course/Group/Semester/Subject. **No `SectionId`.** |

### B. Current database foreign keys and uniqueness (high-signal)

| Surface | Constraints / behavior |
| --- | --- |
| **Program** | Table `Programs`; index `(TenantId, ProgramCode)`; FK to College via `CollegeId` (convention / seed config in `ApplicationDbContext`). |
| **Course** | Index `(TenantId, Code)`; optional `ProgramId`. |
| **Group** | Index `(TenantId, CourseId, Code)`. |
| **Semester** | `DisplayOrder` default; **no unique composite** on `(CourseId, GroupId, Number)` found in EF config. `GroupId` nullable. |
| **Section** | Unique-style index `(TenantId, AcademicYearId, CourseId, GroupId, SemesterId, SectionCode)`. |
| **SubjectAllocation** | Unique `(TenantId, AcademicYearId, SubjectId, CourseId, GroupId, SemesterId, DepartmentId)`; FKs Restrict. |
| **TeachingGroup** | Index `(TenantId, SubjectAllocationId)` **not unique**; FKs Restrict. |
| **TeachingGroupSection** | Unique filtered `(TenantId, TeachingGroupId, SectionId)` where not deleted; TG Cascade, Section Restrict. |
| **TimetableEntry** | Timetable Cascade; TG/SA/Staff/Room/Dept/Course/Group/Semester/Subject Restrict; index on `TeachingGroupId`. |
| **Student** | Unique `(TenantId, StudentNumber)`; FKs to Course/Group/Semester. |
| **Department** | Staff hub: unique `(CollegeId, Code)`. |

### C. Program feature flag / configuration

| Item | Implementation |
| --- | --- |
| Flag | `TenantAcademicConfiguration.EnablePrograms` |
| Entity | `Abhyanvaya.Domain/Entities/Academic/TenantAcademicConfiguration.cs` |
| Table | `TenantAcademicConfigurations` |
| DTO | `TenantAcademicConfigurationDto` / `UpdateTenantAcademicConfigurationRequest` |
| Services | `AcademicCatalogService`, `AcademicTreeService`, `AcademicHierarchyService`, `CourseMasterWriteService`, `IAcademicStructureService` |
| API | `ProgramsController`, `CourseController.ProgramsEnabledAsync` |
| UI | `ProgramsPage.tsx`, `CoursesPage.tsx`, `AcademicUiContext.tsx`, cascade helpers |

When `EnablePrograms = false`, Course.ProgramId is forced null on create/assign (AI29.1A contract).

### D. Current Semester → Group relationship

- `Semester.GroupId` is **`int?`**.
- Null = **course-wide** semester (shared across groups of the course).
- Non-null = semester scoped to a specialization Group.
- UI (`SemestersPage.tsx`) allows Course + optional Group (`NONE_GROUP` → null).
- Scheduling helpers (`resolveSemestersForCourseGroup` in `schedulingFormUtils.ts`) include semesters where `groupId == null || groupId === selectedGroup`.

**Gap vs target:** Target requires operational student semester to be **group-specific**. Current model still allows course-wide semesters and student assignment to any semester under the course list (see E/G).

### E. Current Student → Semester relationship

- Student has required `SemesterId` (+ CourseId, GroupId).
- UI (`StudentsPage.tsx`): Course change resets `groupId` to `0` but **does not clear `semesterId`**; Group change does not cascade-clear semester.
- Semester dropdown is loaded as a flat/course-scoped list — not strictly filtered to group-specific rows only.
- Server validation requires Course/Group/Semester present; full cascade consistency depends on follow-on prompts.

### F. Current TimetableEntry → TeachingGroup / Section

| Link | Status |
| --- | --- |
| `TimetableEntry.TeachingGroupId` | Optional explicit FK (TG.4). Authoritative when set. |
| `TimetableEntry.SectionId` | **Does not exist** (correct vs target rule 9). |
| Section linkage | Via `TimetableSection` projection from `TeachingGroupSection` (`TimetableSectionProjector`). |
| Create/Update/Upsert DTOs | Omit `TeachingGroupId`; assign/clear are dedicated APIs (TG/CAP guards). |

### G. Subject Allocation UI and API hierarchy

| Layer | Detail |
| --- | --- |
| API | `SubjectAllocationsController` in `SchedulingResourceControllers.cs` — `api/scheduling/subject-allocations`; filters include academicYear, staff, department. |
| UI | `SubjectAllocationPage.tsx` — **Year → Department → Course → Group → Semester** (+ Staff). |
| Program | **No Program selector** on Subject Allocation page. |

### H. Timetable Entry UI and API hierarchy

| Layer | Detail |
| --- | --- |
| Entry dialog | `TimetableEntryDialog.tsx` — Department → Course → Group → Semester → SubjectAllocation; TG via compatible-TG + assign/clear. |
| Timetable APIs | `TimetableControllers.cs` — list/filter by department; entry CRUD; TG assign/clear; publish-readiness / publish. |
| Hub | `TimetableHubPage.tsx` — department on create form. |

### I. Teaching Groups navigation route and page registration

| Registration | Location |
| --- | --- |
| Route | `AppRoutes.tsx` — `path="setup/scheduling/teaching-groups"` → `TeachingGroupsPage` |
| Permissions | `SchedulingTeachingGroupView` / `SchedulingTeachingGroupManage` |
| Scheduling hub card | `schedulingCatalogConfig.tsx` — key `teaching-groups`, title **"Teaching Groups"**, to `/setup/scheduling/teaching-groups` |
| API | `TeachingGroupsController` — `api/scheduling/teaching-groups` |
| Setup hub | Teaching Groups **not** listed as a top-level Setup card; reachable via **Scheduling** hub |

**Likely “navigation fix” scope (for later prompts):** ensure discoverability/permissions/deep-links match product intent (Setup vs Scheduling hub), without moving TG out of frozen scheduling ownership.

### J. Current Catalog navigation labels

| Surface | Labels |
| --- | --- |
| `SetupHub.tsx` | Departments, Staff, **Programs**, **Courses**, Groups, Semesters, Sections, Subjects, … + top card **"Scheduling"** → `/setup/scheduling` |
| `schedulingCatalogConfig.tsx` | Groups include **"Faculty & Allocation"** (Subject Allocation, Teaching Groups) and **"Timetable Design"** (Designer, Faculty/Student/Room Timetable, …) |
| Rename target | Prompt item 9: **Scheduling → Timetable** UI rename — currently Setup hub still says **Scheduling** |

### K. Existing tests and architecture guards

| Cluster | Examples |
| --- | --- |
| Program / EnablePrograms | `Abhyanvaya.Application.UnitTests/Academic/AI29_1A_*`, `AI29_1D_24_*`, course-wide semester resolution tests |
| Academic architecture | `AcademicArchitectureGuard`, `Ai291DArchitectureGuard` |
| TG / TimetableEntry | `TeachingGroupApplicationArchitectureGuardTests`, `AiSchedTg4A*`, `AiSchedTg5*`, `AiSchedTg6FinalArchitectureGuardTests`, Prompt 21 projection tests |
| TG UI nav | `AiSchedTg5Prompt3TeachingGroupUiGuard.test.ts`, `AiSchedTg6Prompt3MembershipUxGuard.test.ts` |
| CAP | `AiSchedCapPrompt1`–`11` architecture/transactional/acceptance suites |
| SubjectAllocation uniqueness / scheduling resources | Phase2 / scheduling resource tests |

### L. Files likely needing modification (by change theme)

> Inventories are **impact candidates** for later prompts — **not** implemented here.

#### L1. Teaching Group navigation fix
- `abhyanvaya-ui/src/routes/AppRoutes.tsx`
- `abhyanvaya-ui/src/pages/setup/scheduling/schedulingCatalogConfig.tsx`
- `abhyanvaya-ui/src/pages/setup/SetupHub.tsx` (if Setup-level discoverability required)
- `abhyanvaya-ui/src/pages/setup/scheduling/TeachingGroupsPage.tsx`
- Guards: `AiSchedTg5Prompt3TeachingGroupUiGuard.test.ts`, `AiSchedTg6Prompt3MembershipUxGuard.test.ts`

#### L2. Program → Department association
- Domain: `Program.cs`, possibly `Department.cs`, `Course.cs` (resolution rules)
- EF: `ApplicationDbContext.cs` Program configuration; **new migration**
- DTOs: `ProgramDtos.cs`
- Services: Academic structure / catalog / tree / hierarchy / course write services
- API: `ProgramsController.cs`, related Course APIs
- UI: `ProgramsPage.tsx`, `CoursesPage.tsx`, `AcademicUiContext.tsx`, cascade helpers
- Tests: AI29.1A / 1D Program hierarchy suites + new guards

#### L3. Group-specific Semester
- Domain: `Semester.cs` (nullability / invariants)
- EF + **migration** (data backfill for course-wide rows if enforced)
- `SemestersPage.tsx`, semester APIs/services
- Consumers: Student, SubjectAllocation, TimetableEntry, TeachingGroup, Section resolution helpers
- Tests: `AI29_1D_CourseWideSemesterNodeResolutionTests` and related (expect contract updates)

#### L4. Student Semester remapping
- Data migration / remediation scripts or application remapping service
- `Student` persistence + any enrollment paths
- Tests for remap integrity + tenant isolation

#### L5. Student cascading Course → Group → Semester
- `StudentsPage.tsx` (clear semester on course/group change; filter semesters by group)
- Student create/update API validation (server-side cascade consistency)
- Possibly shared cascade util (align with `academicCascade.ts` / scheduling form utils)

#### L6. Subject Allocation hierarchy
- `SubjectAllocationPage.tsx`, `schedulingFormUtils.ts` / cascade helpers
- `SubjectAllocationsController` + DTOs if Program enters the filter chain
- Services validating Dept/Program/Course/Group/Semester consistency
- CAP/TG regression must remain green (no TG inference)

#### L7. Timetable Entry hierarchy
- `TimetableEntryDialog.tsx`, designer filters, hub create forms
- Timetable entry DTOs/services (`TimetableService`, controllers)
- Compatible-TG query scope alignment

#### L8. TimetableEntry TeachingGroup usage
- **Preserve** existing TG.4/TG.4A/CAP contracts
- Touch only if hierarchy remapping requires re-validation of assign/clear/projector paths:
  - `TeachingGroupApplicationService.cs`
  - `TimetableSectionProjector.cs`
  - `CompatibleTeachingGroupQueryService.cs`
  - CAP Prompt 10/11 / TG Prompt 21 tests (guards must not weaken)

#### L9. Scheduling → Timetable UI rename
- `SetupHub.tsx` (“Scheduling” label/route presentation)
- Possibly `SchedulingHub.tsx`, docs under `abhyanvaya-ui/public/docs/scheduling/**`
- Permission display names if user-facing (careful: permission **keys** may stay `Scheduling*` for compatibility)
- Route path rename is higher risk (bookmarks); prefer label-first unless product mandates path change

#### L10. Architecture / regression tests
- New discovery/implementation guards for catalog/timetable workstream
- Update AI29.1A Program-under-College assumptions → Program-under-Department
- Preserve/extend CAP 1–11 + TG.4A–TG.6 suites (do not delete/weaken)

---

## 6. Current vs target relationship matrix

| Relationship | Current | Target | Gap |
| --- | --- | --- | --- |
| Program parent | College | **Department** | **Major** — schema + API + UI |
| Course under Department (Programs off) | Course has no DepartmentId; Dept parallel via SA/TT | Department → Course | **Major** — needs explicit Course↔Department resolution model |
| Course under Program (Programs on) | Course.ProgramId? under College Program | Department → Program → Course | **Major** — Program must move under Department first |
| Semester group scope | `GroupId?` (course-wide allowed) | Operational semester group-specific | **Medium/Major** — enforce + remap |
| Student C→G→S cascade | Partial UI (course clears group only) | Full cascade | **Medium** — UI + server validation |
| TimetableEntry schedules TG | `TeachingGroupId?` + TimetableSection projection | Same; no SectionId dimension | **Aligned** — preserve |
| TeachingGroup under Semester | Via SA + denorm Course/Group/Semester | Same operational meaning | **Aligned** — do not weaken |
| Programs optional flag | `EnablePrograms` | Optional Program feature | **Aligned** (parent changes) |
| Catalog label Scheduling | Setup hub “Scheduling” | Rename toward Timetable | **UI-only** (path TBD) |

---

## 7. Database / migration impact (preview only)

| Change theme | Likely migration needs |
| --- | --- |
| Program → Department | Add `Programs.DepartmentId` (required or backfilled); possibly deprecate reliance on College-only ownership; FK + indexes |
| Course under Department | Possible `Courses.DepartmentId` **or** derive Department strictly via Program when enabled / alternate mapping when disabled — **product decision required before schema** |
| Group-specific Semester | Tighten `Semesters.GroupId` nullability / uniqueness; data remapping for course-wide rows |
| Student semester remap | Data fix scripts; no Attendance schema |
| TG / TimetableEntry | Prefer **no** schema change; preserve existing FKs |

**This prompt introduces zero migrations.**

---

## 8. Test impact (preview)

- AI29.1A Program management tests must be revised when Program leaves College-only ownership.
- Course-wide semester tests become conflict points if GroupId becomes mandatory for operational use.
- Student cascade needs new UI + API tests.
- Subject Allocation / Timetable Entry hierarchy tests need Program/Department path coverage when added.
- **CAP + TG suites must remain green** and must not be weakened (sole projector writer, no TG inference, publish gate, readiness read-only, tenant isolation).

---

## 9. Risks

1. **Department vs College ownership of Program** — contradicts AI29.1A “Program under College”; largest design delta.
2. **Course has no DepartmentId today** — target “Department → Course when Programs disabled” is not a simple FK flip; risk of inventing dual Department sources (Course vs SubjectAllocation).
3. **Course-wide Semester** is embedded in academic tree resolution and scheduling semester filters — enforcement may break existing tenants.
4. **Student remapping** can orphan attendance/section memberships if not carefully sequenced (read StudentSection; do not casually mutate from Scheduling).
5. **UI rename Scheduling → Timetable** vs stable `Scheduling*` permission keys and `/setup/scheduling/*` routes — bookmark/RBAC churn.
6. **Weakening TG/CAP** while touching hierarchy filters — regression risk on publish readiness, capacity, projection atomicity.
7. **Tenant isolation / authorization** must remain on all new Program-Department queries.

---

## 10. Recommended implementation order

1. **Teaching Group navigation fix** (low risk, UI discoverability)  
2. **Scheduling → Timetable UI rename** (label-first; defer route key renames)  
3. **Program → Department association** (schema + API + UI; unlock correct Course resolution)  
4. **Course Department/Program resolution rules** (Programs on/off) — clarify Course↔Department model before SA/TT UI  
5. **Group-specific Semester** (schema/invariants + Semesters UI)  
6. **Student Semester remapping** (data)  
7. **Student cascading Course → Group → Semester** (UI + server validation)  
8. **Subject Allocation hierarchy** alignment  
9. **Timetable Entry hierarchy** alignment  
10. **TimetableEntry TeachingGroup usage** verification only (preserve frozen TG/CAP; fix only if hierarchy breaks assign/projection)  
11. **Architecture / regression tests** continuous with each step (never weaken CAP/TG guards)

---

## 11. Explicit non-actions (this prompt)

- No production code changes  
- No database schema / migrations  
- No UI / test / configuration changes  
- No weakening of AI-SCHED-CAP or Teaching Group architecture  

---

## 12. Final status

**PASS — discovery complete; no production behavior changed**

**STOP** after Prompt 1.

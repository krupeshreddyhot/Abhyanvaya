# AI-SCHED-CATALOG/TIMETABLE — P1-4  
# Group-Specific Semester Architecture Discovery & Resolution Contract

**Date:** 2026-08-22  
**Type:** Discovery + Architecture Contract ONLY  
**Production code changed:** NONE  
**Schema / migrations:** NONE  
**Status:** Awaiting Chief Architect approval before P1-4 Prompt 2

---

## 1. Executive Summary

Today, `Semester.GroupId` is **optional**. `NULL` is treated as a **course-wide wildcard** (“applies to all Groups under the Course”). That model is intentional in UI, Academic Tree, and client cascade filters — and it blocks deterministic Student → Group → Semester resolution when a Course has multiple Groups (e.g. B.Com → Finance + Computer Applications).

**Recommended target (subject to Architect approval):**

```
Department → [Program optional] → Course → Group → Semester → Student
```

- Operational Semesters belong to **exactly one Group**.
- Do **not** retain `GroupId = NULL` as “all groups.”
- Shared academic numbering across Groups = **one Semester row per Group** (clone/split), not a wildcard.
- Authoritative ownership: **`Semester.GroupId`** (required operationally).  
  `Semester.CourseId` remains as validated denormalization: must equal `Group.CourseId`.
- Student remapping and schema NOT NULL are **deferred** to later prompts.

Local audit (dev DB): **5/6** Semesters are course-wide (`GroupId NULL`); **296/300** Students point at a course-wide Semester — migration will be **ambiguous** (2 Groups on Course) and must **fail closed** without a mapping worksheet.

---

## 2. Current Semester Model

| Field | Type | Notes |
| --- | --- | --- |
| `Id` | int | PK |
| `Number` | int | Academic number (1,2,3…) |
| `Name` | string | Display name |
| `CourseId` | int required | FK → Course |
| `GroupId` | int? optional | FK → Group; null = course-wide |
| `DisplayOrder` | int | Sort |

Entity: `Abhyanvaya.Domain/Entities/Semester.cs`  
API: `SemesterController` (`GET/POST/PUT api/semester`)  
DTOs: `CreateSemesterRequest` / `UpdateSemesterRequest` with `int? GroupId`

No dedicated Semester Application service — controller writes directly via `IApplicationDbContext`.

---

## 3. Current Course / Group / Semester Relationships

```
Course 1 ─── * Group          (Group.CourseId required)
Course 1 ─── * Semester       (Semester.CourseId required)
Group  1 ─── * Semester?      (Semester.GroupId optional)
```

EF: `ApplicationDbContext` configures `Semester` DisplayOrder only — **no unique index** on Semester.

Create uniqueness (application only):

```text
TenantId + CourseId + GroupId + Number
```

(`GroupId` compared as nullable; two null-group rows with same Course+Number can collide — see audit.)

---

## 4. Current Student Relationships

| Field | Required | Notes |
| --- | --- | --- |
| `Student.CourseId` | yes | Stored |
| `Student.GroupId` | yes | Stored |
| `Student.SemesterId` | yes (non-null int) | Stored; no 0 in local data |

Import (`StudentService`): parses Course/Group/Semester IDs from Excel; **does not** validate Semester.Group alignment.

`StudentController` create/update assigns CourseId/GroupId/SemesterId without Group↔Semester consistency check.

Students UI: Course change clears Group; Group change **does not** clear Semester; Semester dropdown lists **all** master semesters (not Group-filtered).

---

## 5. NULL GroupId Semantics

| Question | Answer |
| --- | --- |
| Does NULL mean “all groups”? | **Yes** — product intent |
| Backend enforced? | **Yes** — Academic Tree includes null-group Semesters under every Group of the Course |
| UI only? | **No** — UI + tree + cascade filters |
| Wildcard queries? | `AcademicTreeService`: `s.GroupId == null \|\| s.GroupId == g.Id` |
| Client filters? | `filterSemestersForScope`: includes `groupId == null` |
| SA UI label? | `SubjectAllocationPage`: `"(all groups)"` when `groupId == null` |
| SemestersPage? | Defaults new Semester to None Group; helper: “applies to the whole course” |
| Student assignment rely on it? | **Yes** — 296 students use course-wide Semester III while Groups differ (1 or 2) |
| Scheduling rely on it? | Indirectly: SA/TT store concrete `SemesterId`; null-group Semester IDs are valid FKs |

**P1-4 does not remove this behavior** — only documents that it is **not** the target operational model.

---

## 6. Existing Data Audit (local `abhyanvaya_db`, read-only)

### Semesters

| Metric | Count |
| --- | --- |
| Active Semesters | 6 |
| `GroupId IS NULL` | **5** |
| `GroupId` set | **1** (Id=9, Number=4, Name=Semester IV, GroupId=2 CA) |

Null-group Semesters (all CourseId=1 B.Com, which has **2** Groups):

| Id | Number | Name |
| --- | --- | --- |
| 1 | 1 | Semester I |
| 2 | 2 | Semester II |
| 3 | 3 | Semester III |
| 4 | 4 | Semester VI |
| 5 | 4 | Semester V |

**Data quality:** two null-group rows share `Number=4` (V and VI) — uniqueness already broken for course-wide key.

Groups on Course 1: Finance (Id=1), COMPUTER APPLICATIONS (Id=2).

### Students

| Metric | Count |
| --- | --- |
| Total active | 300 |
| With CourseId / GroupId / SemesterId | 300 / 300 / 300 |
| SemesterId = 0 | 0 |
| Pointing at null-group Semester | **296** (all SemesterId=3) |
| Semester.GroupId matches Student.GroupId | **4** (SemesterId=9, GroupId=2) |
| Semester.GroupId set but ≠ Student.GroupId | 0 |
| Student.CourseId ≠ Semester.CourseId | 0 |

Students on Semester 3 span **both** GroupIds 1 and 2 → course-wide semester shared across Groups.

### Scheduling refs (local)

| Consumer | SemesterId | Notes |
| --- | --- | --- |
| SubjectAllocation | 3 (1 row) | Course-wide Semester |
| TimetableEntry | 3 (1 row) | Course-wide Semester |

---

## 7. Existing Uniqueness Constraints

**CURRENT UNIQUE KEY (application create check):**  
`TenantId + CourseId + GroupId + Number`  
(No DB unique index on Semester.)

**Gaps:** Update path does not re-check uniqueness; duplicate Number=4 null-group rows already exist.

**TARGET UNIQUE KEY:**  
`TenantId + GroupId + Number`  
(with `GroupId` required for operational rows)

**RATIONALE:** After Group ownership, Course is derived via Group; uniqueness must be per Group, not Course-wide. Optional secondary uniqueness on `TenantId + GroupId + Name` may be considered later; Number is the primary academic discriminator in current API.

`CourseId + Number` alone is **invalid** as target unique key (collides across Groups and already collides for null-group duplicates).

---

## 8. Scheduling Impact

| Area | Current | Group-specific impact |
| --- | --- | --- |
| SubjectAllocation | Stores CourseId + GroupId + SemesterId | Must ensure Semester belongs to same Group; already has GroupId denorm |
| TimetableEntry | Denorm Course/Group/Semester from SA | Follows SA; no SectionId |
| TeachingGroup | Has CourseId, GroupId, SemesterId | Compatibility already Group-scoped; **no TG redesign** |
| ConflictEngine / CAP / Publish | Use entry/TG scope | No architecture change if Semester FKs stay valid |
| Academic Tree / cascade | Explicit null-group wildcard | **Must change** in a later implementation prompt |

Frozen TG/CAP/Attendance/Publish Gate: unchanged by P1-4 discovery.

---

## 9. Attendance Impact

Attendance sessions and student matching use CourseId + GroupId + SemesterId (and Subject). Cohort filters treat Semester as an ID, not as “course-wide.”

Future work: after Semester split, session FKs and historical Attendance rows referencing old course-wide SemesterIds need a remapping plan (not in P1-4).

---

## 10. Examination / Marks Impact

**No existing Examination/Marks implementation identified** in Domain/API for Semester-scoped mark entry.

---

## 11. Target Academic Hierarchy

```
College
  └── Department
        └── Program (optional; EnablePrograms)
              └── Course
                    └── Group          ← academic Group (Finance / CA)
                          └── Semester ← operational, Group-owned
                                └── Student
```

**Distinction (frozen):**

| Concept | Role |
| --- | --- |
| Academic **Group** | Catalog branch under Course (Finance, Computer Applications) |
| **Teaching Group / Section** | Scheduling operational cohort; TimetableEntry.TeachingGroupId; projector owns TimetableSection |

Do **not** put Section into the Semester hierarchy in P1-4.

---

## 12. Semester Ownership Contract

1. Every **operational** Semester belongs to exactly one Group (`GroupId` required).
2. Authoritative parent: **Group**.
3. `Semester.CourseId` must equal `Group.CourseId` (validated denorm / query aid — not a second SoT).
4. `GroupId = NULL` is **not** a valid target operational configuration.
5. Same academic term for multiple Groups ⇒ **multiple Semester rows** (one per Group), never a wildcard.
6. Do not introduce Student-specific semester catalogs.

---

## 13. Student Semester Resolution Contract

```
Student.CourseId → Student.GroupId → Semester where Semester.GroupId == Student.GroupId
                 (and Semester.CourseId == Student.CourseId / Group.CourseId)
```

- Reject Semester belonging to another Group.
- On Group change: invalidate/revalidate SemesterId (UI + server) — **implement later**.
- Import must validate alignment — **implement later**.
- **No automatic Student remapping in P1-4.**

---

## 14. Migration Strategy (future prompts)

| Case | Strategy |
| --- | --- |
| A. `GroupId` already set + Course matches Group.Course | Keep |
| B. `GroupId` NULL + **exactly one** Group for Course | Deterministic: set `GroupId` to that Group |
| C. `GroupId` NULL + **multiple** Groups | **Fail closed** — require split worksheet: clone Semester per Group; remap Students/SA/TT/Subjects/Sections by Group |
| D. Invalid Course/Group | Fail closed |
| E. Duplicate Number within target Group | Fail closed / manual renumber |
| F. Duplicate codes | N/A (Semester has no Code today) |
| G. Students on Semesters that split | Remap by Student.GroupId → new Group-specific Semester Id |
| H. Students that become invalid | Fail closed |

**Local Course 1:** 2 Groups ⇒ all 5 null-group Semesters are **Case C** — not auto-mappable.

---

## 15. Ambiguous Data Handling

Do **not**:

- Assign null-group Semester to “first” Group.
- Infer mapping from Name alone when multiple Groups exist.
- Silently share one row across Groups after making GroupId required.

Provide a **manual mapping worksheet** columns (recommended for Prompt 2 prep):

`OldSemesterId | CourseId | Number | Name | TargetGroupId | NewSemesterId (after split) | StudentCount | SaCount | Notes`

---

## 16. API Contract Proposal (not implemented)

| Operation | Target |
| --- | --- |
| Create | Require `GroupId`; validate Group∈Course; unique `(Tenant, GroupId, Number)` |
| Update | Require `GroupId`; cannot move to Group of another Course without Course sync; CourseId = Group.CourseId |
| List / Get | Expose GroupId; filter by Course and/or Group |
| Delete/Archive | Unchanged policy + FK checks |
| Dropdown | Filter by Course **and** Group (no null-group wildcard) |

---

## 17. UI Contract Proposal (not implemented)

**SemestersPage:** Group required; no “None / all groups”; Groups filtered by Course.

**StudentsPage:** Course → Group → Semester cascade; Semester options = Group-specific only; Group change clears invalid Semester.

**Scheduling cascades:** Remove null-group inclusion from `filterSemestersForScope` / Academic Tree in implementation prompt.

---

## 18. Backward Compatibility

- Keep nullable DB column until backfill + split complete.
- Dual-read window may temporarily still *read* null-group for legacy, but new writes should require Group (implementation decision for Prompt 2).
- Course-wide node IDs (`CourseWideSemesterNodeId`) become obsolete after migration.

---

## 19. Risks

| Risk | Mitigation |
| --- | --- |
| 296 students on shared Semester III | Explicit remapping by Group after split |
| Duplicate Number=4 null-group rows | Clean before unique index |
| SA/TT/Subject/Section FKs | Remap with Student/scope Group |
| Academic Tree disambiguation removal | Coordinated with cascade tests |
| Weakening TG/CAP | Out of scope; guards |

---

## 20. Architecture Guards

Contract tests verify **intent** without changing production behavior (nullable GroupId remains).

---

## 21. Test Results

| Suite | Result |
| --- | --- |
| `AiSchedCatalogTimetableP14GroupSpecificSemesterContractGuardTests` | **12 passed** |

---

## 22. Recommended Next Prompt

**P1-4 Prompt 2 — Group-Specific Semester Implementation (schema/API/UI write path)**  
Subject to Architect approval of this contract. Must include: uniqueness, fail-closed migration for Case C, Student/cascade contracts — **or** a dedicated Prompt 2a for Semester write enforcement + Prompt 3 for Student remapping if preferred.

---

## Frozen preservations

- Program optional / EnablePrograms  
- Course.DepartmentId Catalog SSOT  
- SA/TT Department denorm (P1-3)  
- Teaching Group / TimetableSection projector / CAP / Attendance / no SectionId on TimetableEntry

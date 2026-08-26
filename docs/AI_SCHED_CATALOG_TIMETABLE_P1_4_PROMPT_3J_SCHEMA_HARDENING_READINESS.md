# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3J  
# Final Legacy Semester Integrity Audit, Wildcard Consumer Closure & Schema Hardening Readiness Gate

**Date:** 2026-08-23  
**Architect package:** `P1-4/3J3`  
**Implementation PromptCode:** `P1-4-3J3`  
*(Subject Catalog remediation retains PromptCode `P1-4-3J`; prior package 3J1 used `P1-4-3M`.)*  
**Type:** AUDIT + DEPENDENCY CLOSURE + READINESS GATE ONLY  
**API:** `GET /api/semester/schema-hardening-readiness`  
**Auth:** existing Semester administration (`CanManageSemesters`)  
**Runner:** `--schema-hardening-readiness`  

**Explicit:** Prompt 3J performs **no mutation**, **no DDL**, **no Group assignment**, **no TG/TimetableSection writes**, **no CAP/ConflictEngine/Publish changes**.

---

## 1. Audit scope

| Area | Covered |
| --- | --- |
| Semester integrity | NULL GroupId, invalid/deleted Group, Course≠Group.CourseId, duplicates `(TenantId,GroupId,Number)`, orphans |
| Downstream consumers | Student, AttendanceSession, StudentSection, Section, Subject, SA, TT, TimetableSection, TeachingGroup, TeachingGroupSection |
| Teaching Group boundary | Classify-only residual report |
| Wildcard consumers | AcademicTree, filterSemestersForScope, cascades, Subjects/Students/Elective/Scheduling UI, admin historical display |
| Write paths | `SemesterGroupOwnershipRules` + Create/Update DTO + controller assignment |
| Student integrity | `Student.Semester.GroupId == Student.GroupId` and Course match |
| Scheduling | SA / TT / TimetableSection Group ownership |
| Tenant isolation | Semester↔Group/Course, Student/Section/Attendance/SA/TT/TG ↔ Semester |
| Constraint simulation | NOT NULL + UNIQUE (no ALTER) |

---

## 2. Final data counts (expected live baseline)

| Metric | Expected |
| --- | ---: |
| NullGroupSemesterCount | **5** (Ids 1–5) |
| DuplicateGroupSemesterCount (Group-owned keys) | **0** |
| CrossTenantViolationCount | **0** |
| Active operational wildcard consumers | **0** (CLOSED) |
| WritePathsGroupOwned | **true** |
| IsReady / READY_FOR_SCHEMA_HARDENING | **false / NOT READY** |

NULL-group disposition (unchanged by this audit):

| Sem | Disposition | Notes |
| ---: | --- | --- |
| 1 | MANUAL_MAPPING_REQUIRED / historical Subject | Blocks NOT NULL |
| 2–3 | RETAIN_HISTORICAL | Blocks NOT NULL while rows remain |
| 4–5 | DUPLICATE_REVIEW | Manual review; no merge |

---

## 3. Downstream consumer results

Operational refs (Student / Attendance / Section / SA / TT / TG) on NULL-group Semesters: **expected 0** after prior remediations.  
Historical **Subject** ref on Sem 1 may remain and is reported as `LEGACY` consumer finding — still blocks `NullGroup=0` / NOT NULL.

Consumer status vocabulary: `VALID` | `LEGACY` | `MISMATCH` | `ORPHANED` | `CROSS_TENANT` | `MANUAL_REVIEW`.

---

## 4. Wildcard closure results

| Consumer | Closure |
| --- | --- |
| AcademicTreeService | **CLOSED** |
| filterSemestersForScope / academicCascade | **CLOSED** |
| schedulingFormUtils | **CLOSED** |
| SubjectsPage / StudentsPage / ElectiveGroupsPage | **CLOSED** |
| SemestersPage / Master IsLegacyCourseWide | Historical display only (**CLOSED** for operational resolution) |

`WildcardConsumerClosureStatus = CLOSED` when zero `ACTIVE_PRODUCTION` / blocking sites remain.  
Schema hardening still **NOT READY** while NULL-group rows exist.

---

## 5. Teaching Groups

Classify-only. No TG / TGS / Membership / TimetableSection mutation.  
Any residual TG→legacy Semester reference forces `NOT_READY_TG_REFERENCES` and `IsReady=false`.

---

## 6. Write-path verification

- Create/Update require `GroupId` via `SemesterGroupOwnershipRules`.  
- `CourseId` aligned from Group.  
- Create DTO uses non-nullable `GroupId`.  
- No active write path reintroduces operational `GroupId = null`.

---

## 7. Tenant isolation

Hard blocker on any Semester↔Group/Course or consumer↔Semester cross-tenant relationship (`NOT_READY_TENANT_ISOLATION`).

---

## 8. Duplicate analysis

`UNIQUE(TenantId, GroupId, Number)` simulated only.  
Duplicates among Group-specific non-deleted rows block `UniqueReady`.  
Recommended eventual index: filtered `WHERE IsDeleted = 0`. Soft-deleted NULL GroupId must be DBA-scanned before ALTER NOT NULL.

---

## 9. NOT NULL readiness

Requires `NullGroupSemesterCount = 0` **and** no active wildcard operational consumers.  
Current state: **NOT READY** (legacy NULL rows remain).

---

## 10. UNIQUE readiness

Requires no duplicate Group+Number **and** NOT NULL readiness.  
Current state: **NOT READY**.

---

## 11. Blockers / Warnings

Typical `ReadinessCodes` while baseline persists:

- `NOT_READY_NULL_SEMESTERS`
- `NOT_READY_MANUAL_REVIEW` (Sem1 / Sem4–5 dispositions)
- optionally `NOT_READY_DOWNSTREAM_REFERENCES` if Subject/other refs remain

Warnings: soft-deleted scan note; historical archive design still Architect-owned.

---

## 12. Final readiness decision

| Flag | Value |
| --- | --- |
| `IsReady` | **false** |
| `Decision` | **NoGo** |
| Primary `DecisionCode` | `NOT_READY_NULL_SEMESTERS` (or first applicable NOT_READY_*) |
| `READY_FOR_SCHEMA_HARDENING` | **only when every §17 gate is true** |

**Do not claim READY unless the live audit proves every hard gate.**

---

## 13. Proposed next prompt (if READY) — **do not implement**

**Prompt 3K — Semester Database Schema Hardening Execution**

1. Pre-migration re-run of this readiness gate (`IsReady=true` required).  
2. Soft-deleted NULL GroupId DBA scan.  
3. Explicit historical archive exclusion / table design for any retained historical Semesters.  
4. Transactional migration: `GroupId` NOT NULL + filtered UNIQUE.  
5. Post-migration re-audit.  
6. Rollback plan.

If **NOT READY**, remediate BlockingFindings first (historical archive disposition for Sem 1–5, Sem1 Subject, Sem4/5 duplicate review) under Architect-approved prompts — **not** auto-started here.

---

## Architecture guards

- No writes / no SaveChanges / no migrations applied  
- No TG / TimetableSection writes  
- No CAP/TG architecture changes  
- No Course/Department/Program hierarchy changes  
- Single blocker ⇒ `IsReady=false`  
- Tenant isolation enforced  
- Existing Semester write-path hardening intact  

**STOP after Prompt 3J (package 3J3).**

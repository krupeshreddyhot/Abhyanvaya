# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3G.1  
# Controlled Section Semester Remediation — Post-Execution Audit & Readiness

**Date:** 2026-08-23  
**Type:** AUDIT / DISCOVERY / READINESS ONLY — zero mutations  
**PromptCode:** `P1-4-3G.1`  
**Architect package:** `P1-4/3G.1`  
**API:** `GET /api/semester/section-semester-remediation-audit`  
**Auth:** `CanManageSemesters`  
**Runner:** `--section-semester-remediation-audit`

---

## 1. Objective

Determine whether the tenant is **READY** for controlled Section Semester remediation (or confirm post-execution completeness) for:

| Legacy | Finance target | CA target |
| --- | --- | --- |
| Semester **3** | Semester **10** | Semester **11** |

Targets are **validated from the database** (existence, non-NULL `GroupId`, Course↔Group alignment, tenant, Number=3).

**This prompt does not mutate `Section.SemesterId`, Teaching Groups, TeachingGroupSection, SA, TT, TimetableSection, Attendance, StudentSection, Student, or Semester.**

---

## 2. Discovered Section model

`Section` (AI29) ownership path:

`College → AcademicYear → Course → Group → Semester → Section`

Authoritative fields used:

- `Section.GroupId` (required)
- `Section.CourseId`
- `Section.SemesterId`
- `TeachingGroup` / `TeachingGroupSection` (scheduling links)
- `StudentSection` → `Student.GroupId` (evidence only)
- `SubjectAllocation` via `TeachingGroup.SubjectAllocationId` (evidence only)

There is **no** `Section.SubjectId` / `Section.DepartmentId` / `Section.TeachingGroupId` on the entity; those are not invented for this audit.

---

## 3. Resolution precedence

1. Explicit `Section.GroupId` when Group exists, same tenant, and `Group.CourseId == Section.CourseId`  
2. Unanimous `TeachingGroup.GroupId` across linked TeachingGroups  
3. Unanimous `Student.GroupId` via current StudentSection membership  
4. Unanimous `SubjectAllocation.GroupId` via linked TeachingGroups  

**Never** infer from Section name, student counts, or majority voting.

---

## 4. Classification rules

| Code | Meaning |
| --- | --- |
| `SAFE_FOR_FINANCE` | Legacy Sem 3; deterministic Group = Finance target Group → Sem **10** |
| `SAFE_FOR_CA` | Legacy Sem 3; deterministic Group = CA target Group → Sem **11** |
| `ALREADY_CORRECT` | Already on Sem 10/11 with matching Course/Group |
| `MANUAL_MAPPING_REQUIRED` | Ambiguous / non-Finance-non-CA Group |
| `BLOCKED` | Unsafe TGS conflict, target validation failure, or other hard block |
| `INVALID_REFERENCE` | Missing/cross-tenant Course/Group/Semester |

---

## 5. Target Semester validation

For Semesters **10** and **11**:

- Exists, not deleted, same tenant  
- `GroupId` NOT NULL  
- Number = 3  
- `Semester.CourseId == Group.CourseId`  
- Group and Course tenant-safe  
- Finance GroupId ≠ CA GroupId  

Failure → affected remediation **BLOCKED** / overall **NOT_READY**.

---

## 6. TeachingGroupSection compatibility

| Status | Meaning |
| --- | --- |
| `COMPATIBLE` | TG already on Section target Semester |
| `INTERIM_LEGACY_TG_ALLOWED` | TG still on Sem 3; Architect sequence allows Section-first then TG remap (no TGS detach) |
| `INCOMPATIBLE` | TG on conflicting Group-specific Semester or Group mismatch → Section **BLOCKED** |
| `CROSS_TENANT` | Fail closed |
| `MISSING_TEACHING_GROUP` | Orphan link |

TimetableSection is counted for impact only — **never written**.

---

## 7. Downstream impact (read-only counts)

Per Section: TeachingGroupSection, StudentSection, SubjectAllocation (via TG), TimetableEntry (via TimetableSection), TimetableSection, AttendanceSessionSection.

No mutation. Later prompts own any required remediations.

---

## 8. Tenant isolation

Every Section → Course / Group / Semester / TeachingGroup relationship is tenant-checked. Cross-tenant → INVALID/BLOCKED. Architecture guards assert fail-closed audit behavior.

---

## 9. Readiness decision

**READY** only if:

- Finance + CA targets valid  
- Every remaining Sem-3 Section is `SAFE_FOR_FINANCE` or `SAFE_FOR_CA`  
- Zero MANUAL / BLOCKED / INVALID on the audited set  

Zero remaining Sem-3 Sections with valid targets ⇒ **READY** (post-execution complete for Section remap).

Otherwise **NOT_READY** with explicit `BlockingReasons`.

---

## 10. Future execution contract (not implemented here)

Future execution MUST guarantee: transaction boundary, optimistic concurrency, deterministic mapping, fail-closed, zero partial updates, idempotent re-run, full rollback, post-execution integrity audit. Must not mutate TG/TGS/membership/SA/TT/TimetableSection/Attendance/StudentSection/Student/Semester ownership.

---

## 11. Live findings (ambient tenant, 2026-08-23)

| Metric | Value |
| --- | --- |
| Readiness | **READY** |
| TotalLegacySections (Sem 3) | **0** |
| AlreadyCorrectCount | **8** |
| SafeFinance / SafeCA | 0 / 0 |
| Manual / Blocked / Invalid | 0 / 0 / 0 |
| TeachingGroupSectionDependencyCount | 1 (Section 5 → TG; COMPATIBLE / interim as applicable) |
| Finance target Sem 10 | **valid** (GroupId=1, CourseId=1) |
| CA target Sem 11 | **valid** (GroupId=2, CourseId=1) |

Sem III Sections already on targets include Finance Sem 10 (`9,10,11,12`, …) and CA Sem 11 (`5,13,14,15`, …) — all `ALREADY_CORRECT`.

**Interpretation:** Controlled Section Semester remediation for legacy Sem 3 is **complete** for this tenant. No remaining Sem-3 Sections require remap.

---

## 12. Remaining blockers / recommended next prompt

**Section remap:** none remaining.

**Recommended next prompt:** Do not re-execute Section Semester mutation. Proceed with Architect-approved work on remaining schema-hardening NO_GO items (NULL-group Semesters 1–5 / Subject historical FK), or other authorized prompts. Teaching Group remediation is separate and already verified previously where applicable.

---

## 13. Explicit statement

**No mutation, no schema change, no TG/Section/Attendance/SA/TT/Publish change occurred in this prompt.**

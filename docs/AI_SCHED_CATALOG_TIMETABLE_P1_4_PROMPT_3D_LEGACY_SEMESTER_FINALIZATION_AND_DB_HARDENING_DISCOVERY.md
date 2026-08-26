# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3D  
# Legacy Semester Finalization & Database Hardening — Discovery

**Date:** 2026-08-22  
**Type:** Read-only discovery (NO mutation / NO schema change / NO TG mutation)  
**Final status: PASS**  
**Endpoint:** `GET /api/semester/legacy-finalization-audit`  
**Mutations performed:** **0**

---

## 1. Scope

Inventory remaining `Semester.GroupId IS NULL` rows; classify dispositions; document TG residuals; discover NOT NULL / UNIQUE preconditions; catalog NULL-wildcard dependencies; verify Student integrity and post-3C downstream cleanliness.

---

## 2. Live inventory (tenant 1)

| SemId | # | Name | Disposition | Students | Att | SA | TT | TG | Subject | Section | Prompt3A |
| ---: | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 1 | 1 | Semester I | MANUAL_MAPPING_REQUIRED | 0 | 0 | 0 | 0 | 0 | 1 | 0 | RETAIN_LEGACY_PENDING_DECISION |
| 2 | 2 | Semester II | HISTORICAL_RETAIN | 0 | 0 | 0 | 0 | 0 | 0 | 0 | RETAIN_LEGACY_PENDING_DECISION |
| 3 | 3 | Semester III | BLOCKED_BY_TEACHING_GROUP_REFERENCE | 0 | 0 | 0 | 0 | **2** | 17 | 8 | RETAIN_LEGACY_PENDING_DECISION |
| 4 | 4 | Semester VI | DUPLICATE_REVIEW | 0 | 0 | 0 | 0 | 0 | 0 | 0 | DUPLICATE_REVIEW |
| 5 | 4 | Semester V | DUPLICATE_REVIEW | 0 | 0 | 0 | 0 | 0 | 0 | 0 | DUPLICATE_REVIEW |

**Legacy NULL-group count:** 5  
**Duplicate TenantId+GroupId+Number (group-specific):** 0

---

## 3. Teaching Group residuals (identify-only — **no mutation**)

| TG Id | Code | GroupId | Legacy Sem | Candidate Sem | Recommendation | TG Sections | TT using TG |
| ---: | --- | ---: | ---: | ---: | --- | ---: | ---: |
| 1 | TG-PROOF-01 | 2 (CA) | 3 | **11** | SAFE_FOR_SEPARATE_TG_REMEDIATION | 1 | 0 |
| 2 | TG-PROOF-02 | 2 (CA) | 3 | **11** | SAFE_FOR_SEPARATE_TG_REMEDIATION | 0 | 0 |

Deterministic CA Semester III (Id=11) exists. Remap requires a **separate approved TG prompt**. Prompt 3D performed **zero** TG writes.

---

## 4. Downstream (legacy Sem III) after Prompt 3C

| Entity | Count |
| --- | ---: |
| Attendance | **0** |
| SubjectAllocation | **0** |
| TimetableEntry | **0** |
| TeachingGroup | **2** (deferred) |
| Subject | 17 (newly highlighted consumer — not mutated in 3C) |
| Section | 8 (newly highlighted consumer — not mutated in 3C) |

---

## 5. Student integrity

| Metric | Value |
| --- | ---: |
| Students checked | 300 |
| Violations | **0** |

Invariant `Student.Semester.GroupId == Student.GroupId` and Course alignment holds.

---

## 6. NULL-group wildcard dependencies

14 catalogued paths (AcademicTree, filterSemestersForScope, SemestersPage, Master IsLegacyCourseWide, SA/Subjects/Attendance UI, ElectiveGroups, schedulingFormUtils, Students filter, write-paths).  
Actions: REPLACE_WITH_GROUP_SCOPE / HISTORICAL_READ_ONLY / SAFE_TO_DEPRECATE / REQUIRES_REVIEW.  
**Not changed in this prompt.**

---

## 7. Hardening readiness

| Gate | Ready? |
| --- | --- |
| `Semester.GroupId NOT NULL` | **NO** |
| `UNIQUE(TenantId, GroupId, Number)` | **NO** |

### Blocking reasons (live)

1. 5 NULL-group Semester rows remain.  
2. 2 TeachingGroup refs on legacy Semesters.  
3. 18 Subject refs on legacy NULL-group Semesters (Sem1=1 + Sem3=17).  
4. 8 Section refs on legacy Sem 3.  
5. NULL-group wildcard dependencies still present.  
6. Operational rollback backup/journal not yet executed (strategy documented only).

---

## 8. STOP

Do not implement Prompt 3E, mutate legacy rows, mutate TGs, or apply DB constraints.

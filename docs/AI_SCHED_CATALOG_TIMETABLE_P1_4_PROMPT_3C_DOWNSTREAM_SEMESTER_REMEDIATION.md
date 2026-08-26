# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3C  
# Controlled Downstream Legacy Semester Reference Remediation

**Date:** 2026-08-22  
**Type:** Controlled data remediation (AttendanceSession / SubjectAllocation / TimetableEntry)  
**Teaching Group:** DEFERRED / IDENTIFY-ONLY  
**Final status: PASS**

---

## 1. Problem statement

After Prompt 3B (Student remap) and 3B-A (integrity audit), legacy Semester III (Id=3, GroupId=NULL) still had downstream references:

| Entity | Count (3B-A) |
| --- | ---: |
| AttendanceSession | 67 |
| SubjectAllocation | 1 |
| TimetableEntry | 1 |
| TeachingGroup | 2 |

Students were already remapped. Semester targets: Finance=10, CA=11.

---

## 2. Approved scope

**Mutate:** AttendanceSession, SubjectAllocation, TimetableEntry — `SemesterId` only.  
**Identify-only:** TeachingGroup (frozen TG architecture).  
**Out of scope:** Student, Semester, Course, Group, TimetableSection, TeachingGroupSection, CAP/Publish.

---

## 3. Target resolution

```
legacy Sem Number=3, GroupId=NULL, CourseId=C
→ target: TenantId + CourseId=C + GroupId=record.GroupId + Number=3 (exactly one)
```

Fail closed on missing/duplicate targets, course/tenant mismatch, or GroupId ≤ 0. No fuzzy matching.

---

## 4. Workflow / API

```
AUDIT → PREVIEW → EXECUTE (transaction) → POST-INTEGRITY AUDIT
```

| Endpoint | Mode |
| --- | --- |
| `GET /api/semester/downstream-remediation-audit` | read-only |
| `GET /api/semester/downstream-remediation-preview` | read-only |
| `POST /api/semester/downstream-remediation/execute` | mutate + post-audit |

Auth: `CanManageSemesters`. Local runner: `--remediate-preview` / `--remediate-execute`.

---

## 5. Live preview

| Entity | Ready | Deferred | Manual |
| --- | ---: | ---: | ---: |
| AttendanceSession | 67 | 0 | 0 |
| SubjectAllocation | 1 | 0 | 0 |
| TimetableEntry | 1 | 0 | 0 |
| TeachingGroup | 0 | 2 | 0 |
| **Total** | **69** | **2** | **0** |

---

## 6. Live execution

| Metric | Value |
| --- | --- |
| ExecutionStatus | **Completed** |
| RolledBack | false |
| Remediated | **69** |
| Manual review | 0 |
| TeachingGroup writes | **0** |
| Idempotent second run | AlreadyComplete, Remediated=0 |

### Post-integrity audit

| | |
| --- | --- |
| IsHealthy | **true** |
| Critical | **0** |
| Errors | **0** |
| Warnings | 7 (5 LEGACY_COURSE_WIDE_SEMESTER + 2 TEACHING_GROUP_REFERENCE_IMPACT) |
| Attendance legacy refs | **0** |
| SA legacy refs | **0** |
| TT legacy refs | **0** |
| TG legacy refs | **2** (deferred) |

---

## 7. Transaction & idempotency

- Single `ExecuteInTransactionAsync` + one `SaveChanges` for approved mutations.
- Second execution: 0 additional mutations.
- TeachingGroup never written.

---

## 8. Residuals / risks

- TeachingGroup ×2 remain on legacy Sem 3 until a separately approved TG prompt.
- Legacy Semesters 1–5 remain classified (not mutated).
- No schema migration.

---

## 9. Tests / builds

- Prompt 3C unit/guards: 10 passed
- Regression (P1-3/P1-4/TG/CAP/related): see final report
- API / UI builds: see final report

---

## 10. Recommended next

Chief Architect review. Optional later: TG remediation (separate approval), DB unique constraint / NOT NULL GroupId, remaining legacy Semesters.

**STOP** — do not begin Prompt 3D automatically.

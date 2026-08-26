# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3A  
# Legacy Semester Migration Decision Validation & Split Plan

**Date:** 2026-08-22  
**Type:** Read-only decision plan (NO mutation)  
**Final status: PASS**  
**Awaiting Chief Architect approval before Prompt 3B execution**

---

## 1. Revalidated data

Re-queried live `abhyanvaya_db`. **Matches Prompt 2B baseline** (no DIFF on core Semester/Student counts).

| SemId | Course | # | Name | GroupId | Students | Notes |
| ---: | --- | ---: | --- | --- | --- | --- |
| 1 | B.Com | 1 | Semester I | NULL | 0 | |
| 2 | B.Com | 2 | Semester II | NULL | 0 | |
| 3 | B.Com | 3 | Semester III | NULL | **296** (Finance **60** / CA **236**) | |
| 4 | B.Com | 4 | Semester VI | NULL | 0 | duplicate #4 |
| 5 | B.Com | 4 | Semester V | NULL | 0 | duplicate #4 |
| 9 | B.Com | 4 | Semester IV | **2 CA** | 4 (CA) | untouched |

Groups: Finance (Id=1), COMPUTER APPLICATIONS (Id=2).

Semester III downstream (revalidated):

| Consumer | Total | By Group |
| --- | ---: | --- |
| AttendanceSession | 67 | Finance 12 / CA 55 |
| Subject | 17 | Finance 9 / CA 8 |
| Sections | 8 | Finance 4 / CA 4 |
| SubjectAllocation | 1 | CA (GroupId=2) |
| TimetableEntry | 1 | CA (GroupId=2), TeachingGroupId=null |
| TeachingGroup | 2 | both CA (Ids 1–2 proof TGs) |

Additive vs 2B worksheet: Sections=8 (not previously listed on Sem III row). Core 2B counts unchanged.

---

## 2. Migration decisions

| SemId | Decision | TargetGroupIds | RequiresManualApproval | MustNotModify |
| ---: | --- | --- | --- | --- |
| 1 | RETAIN_LEGACY_PENDING_DECISION | 1,2 | Yes | Yes |
| 2 | RETAIN_LEGACY_PENDING_DECISION | 1,2 | Yes | Yes |
| 3 | **SPLIT** | 1,2 | **Yes** | No (plan only) |
| 4 | DUPLICATE_REVIEW | 1,2 | Yes | Yes |
| 5 | DUPLICATE_REVIEW | 1,2 | Yes | Yes |
| 9 | ALREADY_GROUP_SPECIFIC | 2 | No | **Yes** |

Allowed decision set (closed): `SPLIT | MAP_TO_SINGLE_GROUP | RETAIN_LEGACY_PENDING_DECISION | DUPLICATE_REVIEW | ALREADY_GROUP_SPECIFIC | INVALID_DATA`.

---

## 3. Semester III split plan (proposed — not executed)

```
Legacy: B.Com / Semester III / GroupId=NULL / Id=3

Target (after Architect approval + Prompt 3B):
  B.Com → Finance (1) → Semester III (NEW)
  B.Com → COMPUTER APPLICATIONS (2) → Semester III (NEW)
```

**Authoritative student criterion:** `Student.GroupId` → target `Semester.GroupId`.  
Never majority / alpha / roll / UI order / first Group.

---

## 4. Student mapping evidence

| GroupId | Group | Count |
| ---: | --- | ---: |
| 1 | FINANCE | 60 |
| 2 | COMPUTER APPLICATIONS | 236 |
| | **Total** | **296** |

No Student.SemesterId updates in Prompt 3A.

---

## 5–9. Downstream classification (Semester III)

| Entity | Count | Determinism | Notes |
| --- | ---: | --- | --- |
| Student | 296 | DETERMINISTIC_BY_STUDENT_GROUP_ID | Future remap by Student.GroupId |
| AttendanceSession | 67 | DETERMINISTIC_BY_ENTITY_GROUP_ID | Session.GroupId present (12/55) |
| Subject | 17 | DETERMINISTIC_BY_ENTITY_GROUP_ID | Subject.GroupId (9/8) |
| Section | 8 | DETERMINISTIC_BY_ENTITY_GROUP_ID | Section.GroupId (4/4) |
| SubjectAllocation | 1 | DETERMINISTIC_BY_ENTITY_GROUP_ID | SA.GroupId=2 CA |
| TimetableEntry | 1 | DETERMINISTIC_BY_ENTITY_GROUP_ID | Entry.GroupId=2; TG id null |
| TeachingGroup | 2 | **IDENTIFY_ONLY_DO_NOT_MUTATE** | Frozen TG architecture |

---

## 10. Duplicate Semester review

| SemId | Number | Name | Group | Students | Action |
| ---: | ---: | --- | --- | ---: | --- |
| 4 | 4 | Semester VI | NULL | 0 | DUPLICATE_REVIEW — no merge/delete/rename |
| 5 | 4 | Semester V | NULL | 0 | DUPLICATE_REVIEW — no merge/delete/rename |
| 9 | 4 | Semester IV | CA | 4 | ALREADY_GROUP_SPECIFIC — **untouched** |

---

## 11. Records MUST NOT modify (Prompt 3A + until approved)

- All Semesters in this prompt (no writes)
- Semester **9** permanently excluded from legacy conversion
- Semesters **4, 5** until admin resolves duplicates
- Semesters **1, 2** until explicit SPLIT vs RETAIN decision
- TeachingGroup / TimetableSection / CAP / Publish / Attendance rows

---

## 12. Transactional execution plan (Prompt 3B+ only)

1. Architect approval of this document.  
2. For Sem III: create two Group-specific Semesters (same Number/Name).  
3. Remap Students by `Student.GroupId`.  
4. Remap Attendance/Subject/Section/SA/TT by their stored `GroupId`.  
5. Identify TG rows for separate TG-safe review (no auto TG mutation).  
6. Soft-retire or retain legacy Sem III row per Architect policy (not decided here).  
7. Separate admin resolution for Number=4 duplicates before any unique index.

---

## 13. Rollback considerations

- Keep legacy Sem III Id=3 until all FKs remapped and verified.  
- New Semesters must be deletable if Prompt 3B aborts before Student remap.  
- Never delete Sem 9.  
- Prefer transactional batches per entity type with counts matching this plan.

---

## 14. Migration blockers

- SPLIT Sem III awaiting approval  
- DUPLICATE_REVIEW Sem 4 & 5  
- RETAIN pending Sem 1 & 2  
- `MatchesPrompt2BBaseline` must remain true before execution (re-run plan)

---

## 15. Required administrative approvals

1. Approve Sem III SPLIT into Finance + CA.  
2. Confirm Student.GroupId as sole Student remap key.  
3. Approve later entity remaps (Attendance/Subject/Section/SA/TT) by entity GroupId.  
4. Explicit TG review (identify-only).  
5. Resolve Sem 4/5 duplicate Number=4.  
6. Decide Sem 1/2 RETAIN vs SPLIT.

---

## 16. Tests / 17. Guards / Regression

| Suite | Result |
| --- | --- |
| P1-4 + P1-3 + TG/CAP filtered unit | **76 passed** |
| API build | **PASS** |
| UI build | **PASS** |

---

## 18. Recommended Prompt 3B

**Controlled execution:** create Group-specific Semesters for approved SPLIT rows only; remap Students by GroupId; fail closed on any count mismatch vs this plan; TG identify-only; no NOT NULL yet.

---

## Implementation

- `LegacySemesterMigrationDecisionPlanner`
- `ILegacySemesterMigrationDecisionPlanService` / `LegacySemesterMigrationDecisionPlanService`
- `GET /api/semester/legacy-migration-decision-plan` (CanManageSemesters, read-only)

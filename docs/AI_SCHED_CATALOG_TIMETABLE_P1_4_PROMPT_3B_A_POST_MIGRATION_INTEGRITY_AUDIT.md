# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3B-A  
# Post-Migration Integrity Audit & Hardening

**Date:** 2026-08-22  
**Type:** Read-only integrity audit + Student write-path / UI hardening (no data migration)  
**Final status: PASS**  
**Live audit:** Critical=0, Errors=0, Warnings=76 (deferred downstream + legacy classification)  
**IsHealthy:** true  

---

## A. Migration baseline (from Prompt 3B)

| Item | Value |
| --- | --- |
| Legacy Semester III | Id=**3**, GroupId=NULL (retained) |
| Finance Semester III | Id=**10**, GroupId=1 |
| CA Semester III | Id=**11**, GroupId=2 |
| Students remapped | Finance **60** → 10; CA **236** → 11; Total **296** |
| Students remaining on legacy 3 | **0** |
| Downstream left on legacy 3 | Att 67, Subject 17, Section 8, SA 1, TT 1, TG 2 (identify-only; not repaired) |

---

## B. Integrity results (live)

| Check | Result | Violations |
| --- | --- | ---: |
| Semester → Group | PASS | 0 |
| Semester → Course | PASS | 0 |
| Group → Course | PASS | 0 |
| Student → Group | PASS | 0 |
| Student → Semester | PASS | 0 |
| Student → Course | PASS | 0 |
| Attendance → Semester | WARN | 67 (legacy Sem 3 refs) |
| SA → Course/Department | WARN | 1 (legacy Sem 3 ref; Department OK) |
| Timetable → Course/Department | WARN | 1 (legacy Sem 3 ref; Department OK) |
| Teaching Group boundary | WARN | 2 (legacy Sem 3 refs; no TG architecture change) |
| Tenant isolation | PASS | 0 |
| Duplicate Semester numbers | PASS | 0 |
| Legacy Semester classification | WARN | 5 NULL-group Semesters classified |
| Semester III split verification | PASS | 0 |

### Semester III split proof

| Field | Value |
| --- | --- |
| Finance Sem III | Id=10, students=60 |
| CA Sem III | Id=11, students=236 |
| Legacy Sem III students | 0 |
| MigratedStudentsFullyRemapped | **true** |

### Legacy Semesters remaining

| SemId | Classification | Students | Downstream |
| ---: | --- | ---: | ---: |
| 1 | RETAIN_LEGACY_PENDING_DECISION | 0 | 1 |
| 2 | RETAIN_LEGACY_PENDING_DECISION | 0 | 0 |
| 3 | RETAIN_LEGACY_PENDING_DECISION | 0 | 96 |
| 4 | DUPLICATE_REVIEW | 0 | 0 |
| 5 | DUPLICATE_REVIEW | 0 | 0 |

---

## C. Hardening delivered

### Audit (read-only)
- `ISemesterPostMigrationIntegrityAuditService` / `SemesterPostMigrationIntegrityAuditService`
- `GET /api/semester/post-migration-integrity-audit` (`CanManageSemesters`)
- Zero writes (`AsNoTracking` only; no `SaveChanges`)
- Local runner: `scripts/P1_4_Prompt3B_Runner --integrity`

### Student write-path
- `StudentSemesterOwnershipRules` — Course → Group → Semester fail-closed
- Enforced on Student Create / Update (`StudentController`)
- Enforced on Excel import (`StudentService`)
- Rejects legacy NULL-group Semester assignment
- Does **not** infer Semester when Group changes

### UI cascade (`StudentsPage`)
- Loads `/master/semesters/full`
- Form Semesters filtered to selected Course + Group (group-specific only)
- Course change → Group reset + Semester reset
- Group change → Semester reset
- Server remains authoritative

### Frozen boundaries preserved
- No TG create/delete/infer; no TimetableSection writes; no CAP / ConflictEngine / Publish changes
- No automatic repair of Attendance / Subject / SA / TT / TG refs

---

## D. Severity model

| Severity | Examples |
| --- | --- |
| Critical | Cross-tenant; Student Semester wrong Group; Semester Course≠Group.Course; broken TG Group vs Semester |
| Error | Student Semester/Course mismatch; SA/TT Department mismatch; duplicate Group+Number; incomplete Sem III remap |
| Warning | Remaining legacy NULL-group Semesters; downstream still on legacy Sem III (deferred to Prompt 3C) |

`IsHealthy` = Critical==0 && Errors==0 (warnings allowed).

---

## E. Tests

- `StudentSemesterOwnershipRulesTests`
- `SemesterPostMigrationIntegrityAuditServiceTests` (healthy dataset, mismatches, duplicates, legacy, zero writes)
- `AiSchedCatalogTimetableP14Prompt3BAPostMigrationIntegrityGuardTests`

Unit filter result: **16 passed**.

---

## F. Known limitations / deferred

- Downstream entities still referencing legacy Sem III are **warnings**, not auto-repaired → **Prompt 3C**
- Remaining legacy Semesters 1, 2, 4, 5 classified only
- DB unique constraint `(TenantId, GroupId, Number)` and NOT NULL GroupId **not** applied here
- Browser E2E: **NOT EXECUTED — ENVIRONMENT/DATA UNAVAILABLE**

---

## G. Recommended next prompt

Chief Architect review of this audit, then optionally:

**P1-4 Prompt 3C** — downstream remapping of Attendance / Subject / Section / SA / TimetableEntry still on legacy Sem III (TG identify-only unless separately approved).

**STOP** — do not begin next migration automatically.

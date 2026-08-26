# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3H  
# Post-Section-Remediation Integrity Audit & Semester Hardening Readiness

**Date:** 2026-08-23  
**Type:** AUDIT AND READINESS ONLY — zero mutations, zero schema changes  
**Architect package:** `P1-4/3H`  
**PromptCode:** `P1-4-3H`  
**API:** `GET /api/semester/post-section-remediation-integrity-audit`  
(alias: `GET /api/semester/post-section-integrity-schema-readiness`)  
**Runner:** `--prompt3h-audit`

---

## 1. Objective

Validate the post–Prompt 3G hierarchy and all downstream Semester references, then decide whether `Semester.GroupId` schema hardening is safe.

**Do not migrate data. Do not add NOT NULL / UNIQUE. Do not modify Teaching Groups or TimetableSection.**

---

## 2. Prompt 3G verification methodology

Independent of PASS labels:

1. Read `LegacySemesterDispositionJournals` for `PromptCode=P1-4-3G` / `SECTION_SEMESTER_REMEDIATION`.
2. Parse journaled Section IDs from evidence.
3. Re-query live `Sections` for CA Group (expected Course=1, Group=2):
   - remediated on target Sem **11**
   - still on legacy Sem **3**
4. Count Finance residual Sections still on Sem 3 (other Group).
5. Contract satisfied only when journal exists, known blocker Section **5** is on Sem 11, and CA legacy residual = 0.

### Expected 3G contract (from implementation)

| Item | Value |
| --- | --- |
| Targeted legacy Semester | **3** |
| Target Semester | **11** (CA Group 2) |
| Known blocker Section | **5** |
| Mutations | `Section.SemesterId` only |
| TG / TGS / TimetableSection | **not mutated** |
| Transactional / idempotent / concurrency | yes (unit-tested) |

---

## 3. Audit methodology

Composes:

| Source | Role |
| --- | --- |
| Prompt 3B-A `SemesterPostMigrationIntegrityAuditService` | Embedded integrity |
| Prompt 3D `LegacySemesterFinalizationAuditService` | NULL-group dispositions + wildcards |
| Live EF queries | Section / Student / Att / SA / TT / TG / TGS / TimetableSection |
| Prompt 3G journals + Sections | Independent 3G verification |

Zero `SaveChanges`. Fail-closed readiness.

---

## 4. Integrity dimensions

### Section
`Section.Semester.GroupId == Section.GroupId` and Course alignment via Group.

### Student
`Student.Semester.GroupId == Student.GroupId` and Course alignment; write-path hardening remains authoritative.

### Teaching Group (classify-only)
Each TG classified: **SAFE** / **BLOCKED** / **MANUAL_REVIEW_REQUIRED**. No TG mutation.

### Downstream
Attendance, Subject, SubjectAllocation, TimetableEntry, TeachingGroupSection, TimetableSection (projector-owned counts only).

### Legacy NULL-group Semesters
Classified as exactly one of:

- `RETAIN_HISTORICAL`
- `MANUAL_MAPPING_REQUIRED`
- `DUPLICATE_REVIEW`
- `BLOCKED_BY_DOWNSTREAM_REFERENCE`
- `SAFE_FOR_GROUP_MAPPING`
- `OBSOLETE_CANDIDATE`
- (+ TG-specific: `BLOCKED_BY_TEACHING_GROUP_REFERENCE`)

### Duplicates
`TenantId + GroupId + Number` among Group-specific rows — report only.

### Programs / Department SSOT
`EnablePrograms` does not become mandatory; `Course.DepartmentId` remains mandatory; SA/TT Department denorm checked against Course.

### Tenant isolation
Cross-tenant Semester/Group associations counted; must be zero.

---

## 5. Hardening readiness contract

| Flag | Meaning |
| --- | --- |
| `NotNullReady` | Safe to consider `GroupId NOT NULL` |
| `UniqueReady` | Safe to consider `UNIQUE(TenantId, GroupId, Number)` |
| `DownstreamReady` | Att/SA/TT/Subject operational integrity |
| `TenantIsolationReady` | Zero cross-tenant leaks |
| `StudentIntegrityReady` | Students Group-owned |
| `SectionIntegrityReady` | Sections Group-owned + 3G contract |
| `TeachingGroupBoundaryReady` | No TG legacy/incompatible residuals |
| **`SemesterHardeningReady`** | `READY` \| `NOT_READY` \| `BLOCKED` |

`READY` requires all gates true (including wildcards removable). Otherwise `NOT_READY`; critical integrity failures → `BLOCKED`.

---

## 6. Live baseline (last verified ambient tenant)

From prior 3H / 3G.1 / 3I2 re-audits (re-run `--prompt3h-audit` after deploy):

| Metric | Value |
| --- | --- |
| Prompt 3G contract | Verified (Sections 5,13,14,15 → Sem 11) |
| NULL-group Semesters | **5** (Ids 1–5) |
| Section legacy on Sem 3 | **0** |
| Student incompatible | **0** |
| TG legacy | **0** |
| Subject historical on Sem 1 | **1** |
| Duplicate Group+Number | **0** |
| `SemesterHardeningReady` | **NOT_READY** |

---

## 7. Residual blockers (harden only after Architect GO)

1. NULL-group Semesters 1–5 still in operational table  
2. Sem 1 `MANUAL_MAPPING_REQUIRED` / Subject historical  
3. Sem 4–5 `DUPLICATE_REVIEW`  
4. Historical retention / archive model (Prompt 3J-A) may clear operational selection but does **not** alone authorize NOT NULL  
5. Wildcard catalog sites may still appear in 3D inventory until retired  

---

## 8. Recommended next step

Chief Architect decides next prompt based on `SemesterHardeningReady`.  
**Do not auto-start NOT NULL / UNIQUE DDL.**

---

## 9. Strict non-goals (this prompt)

- No NOT NULL / UNIQUE  
- No Semester.GroupId assignment  
- No TG / TimetableSection mutation  
- No automatic remaps  
- No deletion/merge of Semesters  

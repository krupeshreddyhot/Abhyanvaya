# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3I  
# Legacy Semester Final Disposition & Schema Hardening Readiness

**Date:** 2026-08-23  
**Type:** FINAL AUDIT + DISPOSITION + READINESS GATE — prefer zero mutation  
**Architect package:** `P1-4/3I2`  
**Implementation PromptCode:** `P1-4-3N`  
*(Finance Section remediation already owns journal PromptCode `P1-4-3I`.)*  
**API:** `GET /api/semester/legacy-final-disposition-schema-hardening-readiness`  
**Auth:** `CanManageSemesters`  
**Runner:** `--legacy-final-disposition-readiness`

---

## 1. Objective

Determine whether Semester schema is genuinely ready for:

- `Semester.GroupId NOT NULL`
- `UNIQUE(TenantId, GroupId, Number)` (prefer filtered `WHERE IsDeleted=0`)

**Do not execute DDL in this prompt.**

---

## 2. Composition (no second migration engine)

| Source | Role |
| --- | --- |
| Prompt 3M (`SemesterSchemaHardeningReadinessService`) | Ownership, duplicates, students, scheduling, wildcards, write-paths |
| Prompt 3D (`LegacySemesterFinalizationAuditService`) | Disposition inventory + reference counts |
| Prompt 3H2 (`TeachingGroupRemediationReadinessService`) | TG boundary post-Section state |

---

## 3. Current database state (live ambient tenant)

| Metric | Value |
| --- | --- |
| TotalSemesters | 8 |
| NullGroupSemesters | **5** (Ids 1–5) |
| GroupSpecificSemesters | 3 (incl. Sem 10 Finance, Sem 11 CA) |
| DuplicateKeyGroups (Tenant+Group+Number) | 0 |
| Student / Att / SA / TT / Section / TG Sem-3 ops refs | 0 |
| Subject legacy refs | **1** (→ Sem 1) |
| Student integrity violations | 0 |
| Cross-tenant violations | 0 |
| Active production wildcards | 0 |
| Write paths Group-owned | true |

---

## 4. Legacy dispositions (every NULL-group row)

| Sem | Number | Disposition | Mutation | Notes |
| ---: | ---: | --- | --- | --- |
| 1 | 1 | MANUAL_MAPPING_REQUIRED | false | Multi-Group Course; Subject refs=1 |
| 2 | 2 | RETAIN_HISTORICAL | false | No ops refs |
| 3 | 3 | RETAIN_HISTORICAL | false | No ops refs |
| 4 | 4 | DUPLICATE_REVIEW | false | Duplicate Number on Course |
| 5 | 4 | DUPLICATE_REVIEW | false | Duplicate Number on Course |

**Important:** `RETAIN_HISTORICAL` does **not** remove the row from the operational `Semester` table → still blocks `GroupId NOT NULL`.

---

## 5. TG boundary

Approved TGs 1–2 are ALREADY_COMPLETE on Sem 11; zero legacy TG Sem-3 refs.  
`TeachingGroupBoundaryReady = true` (TG residuals do not block; NULL-group Semesters do).

---

## 6. Wildcard / write-path

- Operational wildcards: retired (DEAD_UNREACHABLE / display-only)  
- `WildcardDependencyReady = true`  
- `WritePathReady = true` (Group required; Course from Group)

---

## 7. Readiness flags (live)

| Flag | Value |
| --- | --- |
| SchemaHardeningReady / IsReady | **FALSE** |
| NullGroupReady | **FALSE** |
| UniqueKeyReady | **FALSE** (blocked while NULL rows remain) |
| StudentIntegrityReady | TRUE |
| DownstreamReferenceReady | **FALSE** (Subject→Sem 1) |
| TeachingGroupBoundaryReady | TRUE |
| TenantIsolationReady | TRUE |
| WildcardDependencyReady | TRUE |
| WritePathReady | TRUE |
| MigrationSafetyReady | **FALSE** |

---

## 8. Blockers

1. Five Semesters still `GroupId=NULL` (1–5) — NOT NULL cannot apply.  
2. Subject historical FK on Sem 1.  
3. DUPLICATE_REVIEW on Semesters 4 & 5.  
4. MANUAL_MAPPING_REQUIRED on Sem 1.  
5. No Architect-approved archive/exclusion model that removes NULL-group rows from the operational table.

---

## 9. Migration contract

`AuthorizedForExecution = false`. Blocked 15-step discovery contract returned (do not ALTER). Full authorized contract is produced only when `IsReady=TRUE`.

---

## 10. Next step

**Smallest required remediation:** Architect-approved historical archive / exclusion model for Semesters **1–5** + Subject Sem-1 FK policy (no Group guessing) → re-run this gate → only then authorize Prompt **3J** schema hardening execution.

Do **not** start 3J automatically.

---

## 11. Explicit statement

**No mutation, no DDL, no TG/Section/Attendance/SA/TT/Publish change occurred in this prompt.**

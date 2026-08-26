# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3J  
# Final Semester Schema Hardening Readiness Audit & GO/NO-GO Contract

**Date:** 2026-08-23  
**Type:** AUDIT + CONTRACT ONLY — zero mutations, zero DDL  
**Architect package:** `P1-4/3J1`  
**Implementation PromptCode:** `P1-4-3M`  
*(Subject Catalog remediation already owns journal PromptCode `P1-4-3J`.)*  
**API:** `GET /api/semester/schema-hardening-readiness`  
**Auth:** `CanManageSemesters`  
**Runner:** `--schema-hardening-readiness`  
**Live Decision:** **NO_GO** (`IsReady=false`)

---

## 1. Objective

Produce a machine-verifiable **GO / NO_GO** decision for:

- A. `Semester.GroupId` NOT NULL  
- B. `UNIQUE(TenantId, GroupId, Number)`  
- C. Removal of legacy NULL-group operational semantics  
- D. Removal of remaining AcademicTree/UI course-wide Semester wildcard dependencies  

**No schema change and no data mutation occur in this prompt.**

---

## 2. Frozen architecture decisions

1. Group owns Semester (`Group 1 ── * Semester`).  
2. `Semester.GroupId` is authoritative ownership.  
3. `Semester.CourseId` is validated denormalization of `Group.CourseId`.  
4. NULL `GroupId` is legacy-only — not a future operational state.  
5. Course-wide / all-Groups wildcard must not be reintroduced.  
6. New create/update requires `GroupId`.  
7. Programs optional (`TenantAcademicConfiguration.EnablePrograms`).  
8. If `Course.ProgramId` present → `Course.DepartmentId == Program.DepartmentId`.  
9. `Course.DepartmentId` is catalog ownership SSOT.  
10. SA/TT `DepartmentId` are scheduling denorms from Course — not catalog ownership.  
11. Teaching Group remains the scheduling grouping mechanism.  
12. TG.4A–TG.6 TimetableSection / TeachingGroup boundaries frozen.  
13. ConflictEngine / PlacementSize / RoomCapacity / CAP / Publish Gate frozen.  
14. No second conflict/capacity engine.  
15. No UI recreation of server scheduling rules.  
16. Tenant isolation fail-closed.  
17. Migrations must be deterministic / classified / transactional / idempotent / auditable / concurrency-safe / fail-closed / rollback-safe.  
18–19. Never infer Group when ambiguous; never silently assign legacy Semesters.  
20. Do not mutate TG / Section / Attendance / StudentSection / SA / TT merely to pass schema checks.

---

## 3. Current Semester model

- `Semester.GroupId` is nullable (`int?`).  
- Group-specific operational Semesters exist (e.g. Finance Sem III = 10, CA Sem III = 11).  
- Legacy NULL-group Semesters **1–5** remain as historical / dispositioned rows.  
- Write paths enforce `SemesterGroupOwnershipRules` (Group required; CourseId from Group).  
- Operational wildcards retired under Prompt 3L (`P1-4-3L`).

---

## 4. Target hardened model

| Aspect | Target |
| --- | --- |
| Ownership | `GroupId` NOT NULL |
| Uniqueness | `UNIQUE(TenantId, GroupId, Number)` (prefer filtered `WHERE IsDeleted=0`) |
| CourseId | Always `== Group.CourseId` |
| Wildcards | No operational NULL-group resolution |
| Historical rows | Explicit archive / exclusion design before ALTER |

---

## 5. Audit methodology

- Service: `SemesterSchemaHardeningReadinessService` (`PromptCode=P1-4-3M`).  
- Read-only EF `AsNoTracking` under tenant + `IsDeleted` filters.  
- Reuses Prompt 3D finalization inventory + `LegacySemesterDispositionJournal` for NULL-group disposition.  
- Downstream reference scan: Student, AttendanceSession, Section, Subject, SA, TT, TeachingGroup, TeachingGroupSection, StudentSection, TimetableSection.  
- Source scan for wildcards (AcademicTree, UI filters, Semesters page, controller).  
- Write-path verification against `SemesterGroupOwnershipRules` + `SemesterController` + `CreateSemesterRequest`.  
- Constraint **simulation only** (no ALTER / CREATE INDEX).  
- Architecture guards: TG6 + CAP Prompt11 tests remain present (not weakened).

**Lifecycle scope:** Non-deleted Semesters visible under EF filters. Soft-deleted excluded from counts; DBA must scan soft-deleted NULL `GroupId` before ALTER NOT NULL. Recommended eventual UNIQUE is filtered `WHERE IsDeleted=0`.

---

## 6. Data findings (live ambient tenant)

| Metric | Value |
| --- | --- |
| TenantCount | 1 |
| SemesterCount | 8 |
| NullGroupSemesterCount | **5** (Ids `1–5`) |
| InvalidOwnershipCount | 0 |
| DuplicateKeyCount | 0 (Group-specific keys) |
| CrossTenantViolationCount | 0 |
| WritePathsGroupOwned | true |
| NoActiveNullGroupWritePath | true |
| ArchitectureGuardsIntact | true |

### NULL-group dispositions

| Sem | DispositionCode | Downstream refs |
| ---: | --- | ---: |
| 1 | OTHER_EXPLICIT_APPROVED_STATE (wildcard-retirement journal) | 1 (Subject) |
| 2 | RETAIN_HISTORICAL | 0 |
| 3 | RETAIN_HISTORICAL | 0 |
| 4 | OTHER_EXPLICIT_APPROVED_STATE (DUPLICATE_REVIEW) | 0 |
| 5 | OTHER_EXPLICIT_APPROVED_STATE (DUPLICATE_REVIEW) | 0 |

All NULL-group rows have an explicit disposition (none UNEXPLAINED). They still block NOT NULL because GO requires `NullGroupSemesterCount == 0`.

---

## 7. Downstream findings

| Entity | On NULL-group Semesters |
| --- | --- |
| Student / Attendance / Section / SA / TT / TG / TGS / StudentSection / TimetableSection | **0** operational refs |
| Subject | **1** historical reference → Semester **1** |

`DownstreamLegacyReferenceCount = 1` → blocks GO.

---

## 8. Teaching Group / Section findings

| Classification | SAFE_FOR_HARDENING |
| --- | --- |
| TeachingGroupBlockingCount | 0 |
| SectionBlockingCount | 0 |
| TimetableSectionLegacyRefs | 0 |

TG/Section boundaries do **not** block hardening by themselves. NULL-group Semesters and Subject FK still do.

---

## 9. Wildcard dependency findings

| Path | Kind | BlocksHardening |
| --- | --- | --- |
| AcademicTreeService | DEAD_UNREACHABLE | false |
| filterSemestersForScope | DEAD_UNREACHABLE | false |
| schedulingFormUtils | DEAD_UNREACHABLE | false |
| academicCascade | DEAD_UNREACHABLE | false |
| ElectiveGroupsPage | DEAD_UNREACHABLE | false |
| StudentsPage | DEAD_UNREACHABLE | false |
| SemestersPage | HISTORICAL_DISPLAY_ONLY | false |
| SemesterController (`IsLegacyCourseWide`) | LEGACY_READ_COMPATIBILITY | false |

`WildcardProductionDependencyCount = 0`.

---

## 10. Database constraint simulation

- `ALTER Semester.GroupId SET NOT NULL` → **FAIL** on SemIds `[1,2,3,4,5]`.  
- `UNIQUE(TenantId, GroupId, Number)` → Group-specific non-deleted keys would succeed, but overall UniqueReady=**false** while NULL rows remain under a plain UNIQUE without filtered/archive design.

`NotNullReady=false`, `UniqueReady=false`.

---

## 11. GO/NO-GO criteria

GO requires ALL of:

1. NullGroupSemesterCount == 0  
2. InvalidOwnershipCount == 0  
3. DuplicateKeyCount == 0  
4. DownstreamLegacyReferenceCount == 0  
5. TeachingGroupBlockingCount == 0  
6. SectionBlockingCount == 0  
7. StudentIntegrityViolationCount == 0  
8. SchedulingIntegrityViolationCount == 0  
9. WildcardProductionDependencyCount == 0  
10. CrossTenantViolationCount == 0  
11. NotNullReady == true  
12. UniqueReady == true  
13. Every Semester write path is Group-owned  
14. No active path can recreate NULL GroupId  
15. No architecture guard was weakened  

---

## 12. Current decision

| Field | Value |
| --- | --- |
| Decision | **NO_GO** |
| IsReady | **false** |
| StudentIntegrityViolationCount | 0 |
| SchedulingIntegrityViolationCount | 0 |
| EvidenceSummary | NO_GO: 6 blocking finding(s). NullGroup=5; Dup=0; StudentViol=0; SchedViol=0; ActiveWildcards=0. |

---

## 13. Blocking findings

| Code | EntityId | Reason (summary) | Required remediation |
| --- | ---: | --- | --- |
| SEMESTER_NULL_GROUP | 1 | GroupId NULL; disposition OTHER_EXPLICIT_APPROVED_STATE | Architect historical archive / map / exclude before NOT NULL |
| SEMESTER_NULL_GROUP | 2 | GroupId NULL; RETAIN_HISTORICAL | Same |
| SEMESTER_NULL_GROUP | 3 | GroupId NULL; RETAIN_HISTORICAL | Same |
| SEMESTER_NULL_GROUP | 4 | GroupId NULL; DUPLICATE_REVIEW | Same + duplicate disposition |
| SEMESTER_NULL_GROUP | 5 | GroupId NULL; DUPLICATE_REVIEW | Same + duplicate disposition |
| DOWNSTREAM_LEGACY_REFERENCE | Sem 1 / Subject | Subject still references NULL-group Semester 1 | Subject Catalog / formal historical FK exclusion |

**Warning:** Soft-deleted Semesters excluded by EF filter — DBA must scan before ALTER.

---

## 14. Required remediation prompts (separate Architect authorization)

1. Historical-archive / disposition finalization for Semesters **1–5** (no inferred Group mapping).  
2. Duplicate review resolution for Semesters **4/5**.  
3. Subject Sem 1 historical policy (remap, archive, or exclude from operational FK model).  
4. Soft-deleted NULL `GroupId` DBA pre-check.  
5. **Only after GO:** dedicated DDL prompt for NOT NULL + filtered UNIQUE with rollback plan.

---

## 15. Rollback / safety considerations

This prompt performs **no mutations**. Rollback N/A.  
Future DDL must ship with backup + reversible migration + concurrency-safe rollout.  
Do not weaken TG/CAP/Publish to obtain GO.

---

## 16. Test evidence

| Suite | Result |
| --- | --- |
| `SemesterSchemaHardening*` + Prompt3J architecture guards | **8 Passed** |
| TG6 / CAP Prompt11 / SemesterGroupOwnership / Wildcard / Prompt3H filters | **51 Passed** |
| API build | **0 Errors** |
| UI build | N/A (UI not modified in this prompt) |

Guards prove: no `SaveChanges`, no Semester/TG/Section/Attendance/SA/TT mutation, no DDL markers, tenant isolation, NULL/duplicate/downstream/wildcard/write-path/GO criteria, idempotent audit, TG/CAP guards intact.

---

## 17. Explicit statement

**No mutation, no schema change, no TG/Section/Attendance/SA/TT/Publish change occurred in this prompt.**  
Production behavior unchanged beyond exposing a new **read-only** readiness endpoint.

---

## STOP

Do not auto-start a DDL or remediation prompt. Await Chief Architect authorization based on this NO_GO contract.

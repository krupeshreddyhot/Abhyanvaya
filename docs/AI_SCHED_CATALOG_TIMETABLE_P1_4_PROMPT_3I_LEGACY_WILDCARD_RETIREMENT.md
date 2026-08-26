# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3I  
# Legacy Semester Operational Disposition & Wildcard Retirement Discovery, Controlled Disposition, and Readiness Contract

**Date:** 2026-08-23  
**Architect package folder:** `P1-4/3I3`  
**Implementation PromptCode:** `P1-4-3I3` (readiness contract)  
**Related disposition/execute PromptCode:** `P1-4-3L` (package `3I1`)  
**Does not collide with:** Finance Section remediation PromptCode `P1-4-3I` (package `3I`)

**Type:** Discovery inventory + disposition matrix + historical retention verification + **read-only readiness API**  
**STOP:** No `GroupId` NOT NULL, no UNIQUE(TenantId, GroupId, Number), no Sem 1 auto-map, no duplicate merge, no TG/TimetableSection mutation.

---

## 1. Discovery findings

### 1.1 Prior prompts reused (no second journal)

| Prompt | Role |
| --- | --- |
| 3D | Legacy finalization audit + NULL-group inventory + wildcard catalog |
| 3E / 3JA | Historical retain / `IsHistoricalArchive` disposition |
| 3H | Post-section integrity baseline (`SemesterHardeningReady = NOT_READY`) |
| 3L (3I1) | Operational wildcard retirement + disposition journaling (`LegacySemesterDispositionJournals`) |

### 1.2 Operational paths inspected

| Area | Path | Wildcard status (post 3I3) |
| --- | --- | --- |
| Academic tree | `AcademicTreeService` | **Retired** — `s.GroupId == g.Id` only |
| UI cascade | `filterSemestersForScope` / `academicCascade` | **Retired** — excludes NULL-group |
| Scheduling forms | `schedulingFormUtils` | **Retired** |
| Students | `StudentsPage` + `StudentSemesterOwnershipRules` | **Retired** / write-path rejects NULL-group |
| Subjects | `SubjectsPage` | **Retired** (3I3: uses `filterSemestersForScope` only) |
| Attendance | `AttendanceMarking` | **Retired** |
| Subject allocation | `SubjectAllocationPage` | **Retired** (historical label only) |
| Elective groups | `ElectiveGroupsPage` | **Retired** (`groupId != null`) |
| Semester admin list | `SemestersPage` / Master / SemesterController | **Historical read-only** (list/display only) |
| Semester create | `SemesterGroupOwnershipRules` (Prompt 2A) | **Blocked** — Group required |
| Teaching Groups | TG remediation services | **Unchanged** — no writes in 3I3 |
| TimetableSection | Projector-owned | **Unchanged** — no writes |
| CAP / ConflictEngine / Publish | Scheduling | **Unchanged** |

### 1.3 Remaining NULL-group interpretation

`Semester.GroupId == NULL` is **no longer** treated as “all Groups under Course” in operational selectors.  
NULL-group rows may still appear on **admin historical lists** and disposition/readiness audits only.

---

## 2. Legacy Semester disposition matrix

Baseline Course 1; Groups Finance=1, CA=2; Group-owned Sem III = 10/11.

| SemesterId | Number | Disposition | Notes |
| ---: | ---: | --- | --- |
| 1 | 1 | `MANUAL_MAPPING_REQUIRED` | Historical Subject ref; **no invented Group** |
| 2 | 2 | `RETAIN_HISTORICAL` | Zero operational refs |
| 3 | 3 | `RETAIN_HISTORICAL` | Remapped to Sem 11 path; do not reverse |
| 4 | 4 | `DUPLICATE_REVIEW` | No merge/delete/reassign |
| 5 | 5 | `DUPLICATE_REVIEW` | No merge/delete/reassign |
| 9/10/11… | Group-owned | **Do not modify** | Authoritative operational Semesters |

Classification is exact; no new categories introduced.

---

## 3. Wildcard dependency inventory

Catalog source: `LegacySemesterFinalizationAuditService.BuildWildcardCatalog` (updated notes in 3I3).  
Classification at readiness time: `LegacySemesterWildcardRetirementService.MapWildcardSites`.

| Classification | Meaning |
| --- | --- |
| `SAFE_TO_REMOVE` | Operational wildcard code path retired |
| `LEGACY_READ_ONLY_COMPATIBILITY` | Admin/list historical display only |
| `ACTIVE_RUNTIME_DEPENDENCY` / `REQUIRES_FOLLOWUP` | Blocks `WildcardRetirementReady` |

Expected after 3I3: **0** active/follow-up operational sites; historical list sites remain read-only compatible.

---

## 4. Historical retention model

- Original Semester identity retained (no physical delete).
- No arbitrary `GroupId` assignment to satisfy future NOT NULL.
- Operational selectors exclude NULL-group and `IsHistoricalArchive` rows.
- Single journal: `LegacySemesterDispositionJournals` (reuse; no second journal).
- Disposition execute (`POST .../legacy-wildcard-retirement/execute`, PromptCode `P1-4-3L`):
  - tenant-safe, transactional, concurrency-aware
  - idempotent — second run → zero additional writes
- Readiness (`GET .../legacy-wildcard-retirement-readiness`, PromptCode `P1-4-3I3`):
  - **read-only**; `SaveChangesInvoked = false`

---

## 5. APIs changed/added

| Method | Route | Behavior |
| --- | --- | --- |
| **GET** | `/api/semester/legacy-wildcard-retirement-readiness` | **NEW (3I3)** — readiness contract |
| GET | `/api/semester/legacy-wildcard-retirement-preview` | Existing (3L) |
| POST | `/api/semester/legacy-wildcard-retirement/execute` | Existing (3L) |

Authorization: existing Semester administration model.

### Readiness contract fields

- `legacyNullGroupCount`, `activeLegacyWildcardCount`, `historicalOnlyCount`
- `manualMappingRequiredCount`, `duplicateReviewCount`
- `downstreamReferenceCount`, `wildcardQueryDependencyCount`
- `tenantIsolationPassed`, `operationalSemesterResolutionPassed`, `historicalRetentionPassed`
- `wildcardRetirementReady`, `blockers[]`, `warnings[]`
- Plus: `semester1ManualMappingPreview`, `duplicateReviewPreviews`, `dispositionMatrix`

`wildcardRetirementReady = true` only when all seven Architect conditions hold (write-path blocked, no operational NULL resolution, no operational consume of NULL-group Semesters, remaining NULL rows classified, no unsafe downstream refs, tenant isolation pass, no critical integrity blockers).  
**Does not** authorize `GroupId` NOT NULL / UNIQUE.

Runner: `--legacy-wildcard-retirement-readiness`

---

## 6. Transaction / idempotency behavior

| Operation | Transaction | Idempotency |
| --- | --- | --- |
| Readiness GET | N/A (no writes) | N/A |
| Preview GET | N/A | N/A |
| Execute POST | `ExecuteInTransactionAsync`; abort rolls back | Prior `OPERATIONAL_WILDCARD_RETIRED` journal → AlreadyComplete, zero new rows |

---

## 7. Tenant isolation controls

- All queries scoped by `ICurrentUserService.TenantId`.
- Readiness embeds Prompt 3H `TenantIsolation` result; failure → blocker + `WildcardRetirementReady = false`.
- Fail-closed: no cross-tenant disposition or readiness elevation.

---

## 8. Test evidence

Focused:

- `LegacySemesterWildcardRetirementServiceTests` (preview, readiness read-only, architecture guards, SubjectsPage no null-group OR)
- Existing P1-4 Group-specific Semester contract guards
- Semester Group ownership / AcademicTree guards

Regression (run with this package):

- P1-4 Academic / Semester / Teaching Group suites
- P1-3 Department alignment guards
- CAP / scheduling regression guards (no CAP source edits)

---

## 9. Remaining blockers (schema / Architect)

These **do not** authorize NOT NULL / UNIQUE:

1. NULL-group Semesters 1–5 still physically present (historical/duplicate/manual).
2. Semester 1 remains `MANUAL_MAPPING_REQUIRED` until Subject historical disposition approved.
3. Semesters 4 and 5 remain `DUPLICATE_REVIEW` (no auto-merge).
4. Historical retention design for permanent archive vs operational table still Architect-owned.
5. `CanMakeGroupIdNotNull` / `CanAddGroupSemesterUniqueConstraint` remain **false**.

---

## 10. Explicit readiness result

| Flag | Expected after 3I3 (code) | Schema hardening |
| --- | --- | --- |
| `NewNullGroupWritePathBlocked` | **true** | — |
| `OperationalSemesterResolutionPassed` | **true** when active wildcards=0 and downstream ops=0 | — |
| `WildcardRetirementReady` | **true** only if seven conditions met | Does **not** imply schema GO |
| `CanMakeGroupIdNotNull` | **false** | BLOCKED until Architect |
| `SemesterHardeningReady` (3H) | **NOT_READY** | Separate 3J/schema prompts |

**Recommended next prompt (after Architect review of this readiness evidence):**  
Prompt **3J** final integrity audit — only then consider NOT NULL + UNIQUE hardening.

**Do not auto-start schema hardening from this package.**

---

## Architecture guards proven

- No automatic legacy Group assignment / no arbitrary Group selection  
- No deletion of historical Semesters  
- No TeachingGroup / TimetableSection writes  
- No CAP / ConflictEngine / Publish changes  
- Readiness endpoint: no SaveChanges  
- Disposition execute: transactional + idempotent  
- Tenant isolation enforced  
- Course.DepartmentId SSOT unchanged; EnablePrograms optional  
- Student Group → Semester ownership authoritative  

**STOP after Prompt 3I (package 3I3).**

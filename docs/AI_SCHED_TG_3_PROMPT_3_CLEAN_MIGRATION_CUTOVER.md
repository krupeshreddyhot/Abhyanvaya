# AI-SCHED-TG.3 Prompt 3 — Clean Pre-Production Migration & Disposable Timetable Cutover

**Workstream:** AI-SCHED-TG.3  
**Prompt:** 3 — Clean pre-production migration & disposable timetable cutover  
**Date:** 2026-08-18  
**Predecessor:** AI-SCHED-TG.3 Prompt 2 (PASS — frozen)

**STATUS: PASS**

---

## 1. Environment confirmation

| Check | Result |
|---|---|
| Connection source | `Abhyanvaya.API/appsettings.json` → `DefaultConnection` |
| Host | `localhost` (`::1`) |
| Port | `5432` |
| Database | `abhyanvaya_db` |
| Server role | Local developer PostgreSQL 18 |
| Other local DBs present | `abhyanvaya_integration_test`, `attorneys_db` (untouched) |
| Production remote connection | **None** — no cloud/prod host involved |
| Application live at college | **No** (architecture authority + prior TG.2A Prompt 3) |

**Disposable pre-production/test determination:** **CONFIRMED.**  
STOP condition not triggered.

---

## 2. Database identity/environment

```
current_database = abhyanvaya_db
current_user     = postgres
server_addr      = ::1/128
server_port      = 5432
```

Design-time factory echoed the same localhost connection during `dotnet ef database update`.

---

## 3. Pre-cutover timetable inventory

| Table | Count |
|---|---:|
| SchedulingTimetable | 5 |
| SchedulingTimetableEntry | 17 |
| TimetableSections | 0 |
| SchedulingScheduleVersion | 6 |
| SchedulingTimetableChangeHistory | 33 |
| SchedulingTimetableApprovalRequest | 2 |
| SchedulingTimetableApprovalStep | 4 |
| SchedulingTimetableApprovalHistory | 3 |
| SchedulingTimetableDecisionHistory | 3 |
| SchedulingConflictDetectionRun | 1 |
| SchedulingConflictFinding | 50 |
| SchedulingTimetableCloneJob / WarningDismissal / ApprovalComment | 0 |

Timetable headers included soft-deleted and draft/test codes (`IICA1`, `TTT`, blank codes).

TeachingGroup tables **did not exist** before migration.

---

## 4. Disposable-data determination

Per AI-SCHED-TG.2 / TG.2A Prompt 3 **approved clean cutover** policy:

- Timetable designer / governance / conflict artifacts in this DB are **test/disposable**.
- Academic foundation is **not** disposable.

No production backfill strategy was designed or executed.

---

## 5. Records removed

Deleted (transactional, children-first):

| Artifact | Removed |
|---|---:|
| SchedulingConflictFinding | 50 |
| SchedulingConflictDetectionRun | 1 |
| SchedulingTimetableApprovalHistory | 3 |
| SchedulingTimetableDecisionHistory | 3 |
| SchedulingTimetableApprovalStep | 4 |
| SchedulingTimetableApprovalRequest | 2 |
| SchedulingTimetableChangeHistory | 33 |
| SchedulingTimetableEntry | 17 |
| SchedulingTimetable | 5 |
| SchedulingScheduleVersion | 6 |
| TimetableSections / CloneJob / WarningDismissal / ApprovalComment | 0 (already empty) |

Operations used targeted `DELETE` only — **no** `DROP DATABASE`, `DROP SCHEMA`, or `TRUNCATE *`.

---

## 6. Records intentionally preserved

| Artifact | Count after cutover |
|---|---:|
| Student | 300 |
| Sections | 15 |
| StudentSections | 100 |
| Course | 1 |
| Group | 2 |
| Subject | 18 |
| SchedulingSubjectAllocation | 1 |
| Attendance | 3748 |
| User | 6 |

Roles/permissions/tenant-college configuration left intact.

---

## 7–8. Migration applied / Migration ID

| Item | Value |
|---|---|
| Command | `dotnet ef database update 20260817153000_AI_SCHED_TG_3_TeachingGroup --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API` |
| Migration ID | `20260817153000_AI_SCHED_TG_3_TeachingGroup` |
| Result | **Applied successfully** (`Done.`) |
| Pre-list status | Only pending migration |
| Post-list status | Applied (no longer pending) |

Migration content matches Prompt 2 design (no material deviation; migration source not altered to force apply).

---

## 9. Created tables

- `SchedulingTeachingGroup`
- `SchedulingTeachingGroupSection`
- `SchedulingTeachingGroupMembership`

---

## 10. Foreign keys / indexes (verified in PostgreSQL)

### Foreign keys (delete behavior)

| FK | Behavior |
|---|---|
| TG → SchedulingSubjectAllocation | RESTRICT |
| TG → SchedulingAcademicYear / Course / Group / Semester / Subject | RESTRICT |
| TGSection → TeachingGroup | CASCADE |
| TGSection → Sections | RESTRICT |
| TGMembership → TeachingGroup | CASCADE |
| TGMembership → Student | RESTRICT |

### Notable indexes

- `(TenantId, SubjectAllocationId)` — **non-unique** (many TGs per allocation)
- Filtered unique `(TenantId, TeachingGroupId, SectionId) WHERE IsDeleted = FALSE`
- Filtered unique `(TenantId, TeachingGroupId, StudentId) WHERE IsCurrent AND NOT IsDeleted`
- ExclusionGroupKey composite index — **non-unique**

### Capacity columns

- `ExpectedStudentCount` nullable  
- `MaxTeachingCapacity` nullable  
- No `ResolvedStudentCount`, `PlannedCapacity`, `SectionGroupId`

### TimetableEntry

- **No** `TeachingGroupId` column (deferred to later prompt)

---

## 11. Teaching Group relationship verification

Live DB proof after seed:

| Check | Result |
|---|---|
| SubjectAllocation `1` owns **2** TeachingGroups | Pass |
| TG-PROOF-01 → TeachingGroupSection → Section `5` | Pass |
| TG-PROOF-01 membership Student `1` (Include, IsCurrent) | Pass |
| Duplicate TG↔Section insert rejected by unique index | Pass |
| No SectionGroupId column | Pass |
| No second TG→Section FK path | Pass |

Canonical path confirmed:

```
SubjectAllocation → TeachingGroup → TeachingGroupSection → Section
TeachingGroup → TeachingGroupMembership → Student
```

---

## 12. Test data created

Minimal deterministic seed only:

| Code | Purpose |
|---|---|
| `TG-PROOF-01` | SectionDerived + Section link + membership; Expected=0, Max=40 |
| `TG-PROOF-02` | Second TG on same SubjectAllocation (many-per-allocation proof) |

**Not created:** wholesale prior timetable dataset; TimetableEntry with TeachingGroupId (field not yet in schema).

---

## 13. Tests executed

| Suite filter | Failed | Passed | Skipped | Total |
|---|---:|---:|---:|---:|
| TeachingGroupDomain + EfModelIntegrity + ArchitectureGuard + SchedulingFoundation + SubjectAllocation + TimetableEntryMapping + Phase2APermissionKeys | 0 | 89 | 0 | 89 |
| AttendanceSessionResolver + AllocationEngine + AllocationPermissionProvisioning + JwtPermissionIsolation | 0 | 26 | 0 | 26 |

Domain invariants covered by unit tests remain: Max=0 invalid, Expected=0 valid, Expected>Max rejected, ResolvedStudentCount derived, SectionGroupId absent, tenant filters active.

---

## 14. Architecture Guard result

Architecture Guard included in regression run — **PASS** expectations preserved:

- no SectionGroupId reintroduction  
- no IgnoreQueryFilters in TG configs  
- no SubjectAllocation uniqueness assumption  
- no Room.Capacity copied into TeachingGroup  
- TimetableEntry.TeachingGroupId not prematurely added  

---

## 15–16. Build results

| Build | Result |
|---|---|
| API | PASS (0 errors) |
| UI | PASS (no UI source changes) |

---

## 17. Regression result

- Primary scheduling/TG/Architecture Guard pack: **89 passed / 0 failed / 0 skipped**
- Attendance / allocation / auth sample: **26 passed / 0 failed / 0 skipped**
- Combined executed: **115 passed / 0 failed / 0 skipped**

No new regressions introduced by this prompt.

---

## 18. Deferred functionality

| Item | Status |
|---|---|
| `TimetableEntry.TeachingGroupId` | Deferred (Prompt 5 / later) |
| TimetableSection projection from TeachingGroupSection | Deferred |
| Legacy PUT `/sections` bridge | Deferred |
| Membership resolver service | Deferred |
| TeachingGroup APIs / UI | Deferred |
| Attendance TG integration | Deferred |
| Automatic TG generation / allocation engine changes | Deferred |

---

## 19. Unexpected findings

1. Local PostgreSQL client is v18 (`C:\Program Files\PostgreSQL\18\bin\psql.exe`); database remained `abhyanvaya_db` on localhost.  
2. `TimetableSections` was already empty (0) before cutover — no bridge rows to discard.  
3. ModelSnapshot drift (documented in Prompt 2) did not block apply: the hand-authored migration was the sole pending migration and applied cleanly.

No second compensatory migration was created.

---

## 20. Final status

**STATUS = PASS**

Database/model state produced by this prompt is eligible to freeze pending Chief Architect approval of the next step.

Do **not** proceed to Teaching Group timetable integration or UI implementation without an explicit next-prompt instruction.

---

## Concise gate summary

| Gate | Result |
|---|---|
| Migration | PASS |
| Disposable timetable cleanup | PASS |
| Teaching Group schema | PASS |
| Relationship integrity | PASS |
| Domain invariants | PASS |
| Architecture Guard | PASS |
| Security/Tenant isolation | PASS (filters + Restrict FKs; no IgnoreQueryFilters) |
| API build | PASS |
| UI build | PASS |
| Regression | 115 passed / 0 failed / 0 skipped |
| Deferred | TimetableEntry.TeachingGroupId + bridge/API/UI/attendance |
| **Final** | **PASS** |

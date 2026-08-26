# AI-SCHED-CATALOG/TIMETABLE — P1-4 Test Data Reset  
# Safety & Governance Gate

**Date:** 2026-08-24 (baseline) · **Re-audit:** 2026-08-25  
**Package:** `P1-4/TESTDATARESET/Final`  
**Prompt type:** Discovery + safety validation **ONLY**  
**Database mutation:** **NONE**  
**Schema / migration changes:** **NONE**  
**Application behavior changes:** **NONE** (docs + guards only)

**Scripts under review (not executed):**

- `scripts/AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_PREVIEW.sql`
- `scripts/AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET.sql`
- `scripts/AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_VERIFY.sql`

**Related (separate path — not this package):** `PreProductionTransactionalResetService` (Prompt 3HC1) is an application-layer wipe + Student Semester reconciliation path. This safety gate evaluates the **SQL TESTDATARESET** scripts only. Do not conflate the two.

---

## FINAL EXECUTION-READINESS DECISION

# RESET BLOCKED — DO NOT EXECUTE

| Blocker ID | Severity | Reason |
| --- | --- | --- |
| B1 | **CRITICAL** | **Test-data boundary is not deterministic.** Scope is only `"TenantId" = :tenant_id` on transactional tables. There is **no** schema marker (`IsTestData`, named test college code, seed batch id, etc.). Within a tenant, permanent-looking operational rows cannot be distinguished from disposable test rows by the script. Per governance: *RESET NOT SAFE — TEST DATA BOUNDARY NOT DETERMINISTIC*. |
| B2 | **CRITICAL** | **Teaching Group wipe is in the reset SQL** (`SchedulingTeachingGroup*`, and `TimetableSections`). This governance prompt forbids silent TG / TeachingGroupSection / TimetableSection mutation without **Chief Architect approval** and an explicit dependency-safe recreation plan. |
| B3 | **HIGH** | **Expected live row counts not captured in this gate.** PREVIEW.sql was not run against `abhyanvaya_db` in this prompt (read-only preview is allowed later; counts must be attached to an execution approval packet). Without baseline counts, “stop on unexpected row counts” cannot be certified. |
| B4 | **MEDIUM** | **REVIEW-excluded residuals** (conflict workspace UX, telemetry aggregate, section policies/preferences) remain after wipe — not blockers for safety, but post-reset residual ≠ zero for every scheduling-adjacent table. |

**2026-08-25 re-audit confirmation:** Independent discovery of provider (PostgreSQL/Npgsql → `abhyanvaya_db`), EF Fluent table names, FK Restrict chains (TG ← TimetableEntry, SA ← TG), `OperationalSemesterRules` (GroupId required + non-historical), and absence of any `IsTestData` discriminator **reaffirms** blockers B1–B3. Classification unchanged: **RESET BLOCKED**.

**What is NOT blocked (positive findings):**

- Tenant-scoped predicates on destructive statements (no unscoped `DELETE FROM Table;`)
- FK checks remain enabled (no `FOREIGN_KEY_CHECKS` / trigger disable)
- Student / Semester / Group / Course / Subject / Department / Programs / College are **not** deleted or updated
- No recreation of `Semester.GroupId = NULL` operational wildcards in the reset scripts
- Transactional BEGIN/COMMIT with protected-count and residual checks
- SQL statement inventory: **58** tenant-scoped `DELETE` statements in reset script (all classified below); **0** unscoped deletes; **0** UPDATE/TRUNCATE/DROP/ALTER

---

## 1. Current database topology

| Item | Value |
| --- | --- |
| Provider | **PostgreSQL** (Npgsql) |
| Configured database | `abhyanvaya_db` (local `DefaultConnection`) |
| Schema | `public` (EF default quoted identifiers) |
| Tenant model | `TenantId` on most academic/scheduling entities; `College."TenantId"` is the college/tenant root |
| ORM | EF Core 8 + hand-authored migrations for TG / AI29 sections |

**Important naming:** Semester table is `"Semester"` (singular), **not** `"Semesters"`.

### Relevant tables (entity → table)

| Domain | Tables |
| --- | --- |
| Catalog | `College`, `Department`, `Programs`, `Course`, `Group`, `Semester`, `Subject`, `TenantSubject` |
| People | `Student`, `StaffMembers`, `User`, auth role/permission tables |
| Attendance | `Attendance`, `AttendanceDetail`, `AttendanceSession`, `AttendanceRecognition*`, `AttendanceSessionImage`, `AttendanceRetryHistory`, `AttendanceBulkOperationHistory`, `AttendanceSessionSections`, `ClassSchedule` |
| Sections | `Sections`, `StudentSections`, `FacultySectionAssignments`, `TimetableSections`, `SectionGroups*`, lifecycle/merge/split/lineage/version/capacity tables |
| Scheduling | `SchedulingSubjectAllocation`, `SchedulingTimetable*`, `SchedulingScheduleVersion`, conflict/optimization tables |
| Teaching Groups | `SchedulingTeachingGroup`, `SchedulingTeachingGroupMembership`, `SchedulingTeachingGroupSection` |
| Config | `TenantAcademicConfigurations` (`EnablePrograms`), `ProgramPolicies`, `TenantSectionCapacityPolicies`, `SchedulingConflictRuleThresholdSetting` |

---

## 2. Test-data boundary

| Candidate discriminator | Present? | Assessment |
| --- | --- | --- |
| Row flag `IsTestData` / similar | **No** | Cannot use |
| Dedicated test TenantId vs production TenantId | **Policy-only** | Script *assumes* caller picks disposable tenant; not proven by schema |
| Named College code / environment | **Not encoded in SQL** | Not used as predicate |
| Architect verbal declaration “all TT/TG/Attendance for tenant are test” | **Out-of-band** | Insufficient alone under this gate |

**Conclusion:** Boundary = **ambiguous at row level** → **RESET NOT SAFE** until Chief Architect either:

1. Affirms in writing that for a **named** `TenantId`/`CollegeId` the entire allowlist is disposable pre-production test data, **and** no other tenants share the DB with production-like data; **or**
2. Approves a deterministic marker and a revised script that filters on it.

---

## 3. Protected-data boundary (must NOT be removed)

| Category | Tables / data |
| --- | --- |
| Tenant / org | `College`, `University` |
| Catalog | `Department`, `Programs`, `Course`, `Group`, `Semester`, `Subject`, `TenantSubject`, `ElectiveGroup` |
| People | `Student` (no DELETE/UPDATE of CourseId/GroupId/SemesterId), Staff masters |
| Auth | `User`, `Permission`, `ApplicationRole*`, `UserApplicationRole` |
| Config | `TenantAcademicConfigurations` (**EnablePrograms**), `ProgramPolicies`, `TenantSectionCapacityPolicies`, conflict **threshold** settings |
| Facility / calendar | AcademicYear/Term, Campus/Building/Floor/Room, TimeSlot*, ArchiveReason |
| Preferences / identity | Faculty prefs/workload, WorkspacePreference, AttendanceRecoveryPreference, face embeddings / enrollment |
| Audit / journals | Global `AuditEntry`, `LegacySemesterDispositionJournals` |
| REVIEW (excluded from DELETE) | Conflict workspace pin/bookmark/note, conflict rule config history, optimization telemetry aggregate, `SectionPolicies`, `SectionAllocationPreferences`, room rules/availability |

---

## 4. FK / dependency graph (summary)

```
Masters (PRESERVE): Student, Semester, Group, Course, Subject, Dept, Program, College
        ▲ Restrict / logical FKs
Attendance* → AttendanceSession → ClassSchedule
ConflictFinding → ConflictDetectionRun → Timetable
Timetable approval children → Timetable / ScheduleVersion
TimetableSections → TimetableEntry / Section
TimetableEntry → Timetable; Restrict → TeachingGroup, SubjectAllocation, masters
TeachingGroupMembership / TeachingGroupSection → TeachingGroup
TeachingGroup → SubjectAllocation (Restrict)
StudentSections / Section* → Sections
AllocationEngine* → section allocation sandbox
```

Full matrix: `docs/AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_MATRIX.md`

---

## 5. Proposed deletion order (from reviewed script)

1. Attendance children → Attendance → Session → ClassSchedule  
2. ConflictFinding → ConflictDetectionRun  
3. Optimization sandbox children → Scenario → runs/metrics  
4. Timetable approval/history/clone → **TimetableSections** → TimetableEntry → Timetable → ScheduleVersion  
5. **TeachingGroupMembership → TeachingGroupSection → TeachingGroup** → SubjectAllocation  
6. AllocationEngine* → SectionAllocationSnapshots → StudentSections → Section* children → Sections  

**FK checks stay ON.** No disable-FK shortcuts.

---

## 6. Proposed recreation / seed order (post-reset, future prompt only)

Manual / admin UI preferred (no seed script in this package that creates Semesters):

1. Confirm Group-owned operational Semesters already exist (`GroupId IS NOT NULL`, `IsHistoricalArchive = false`) — **do not** create NULL-group operational Semesters  
2. Recreate **Sections** bound to Group-owned Semester  
3. Recreate **SubjectAllocations** (DepartmentId denormalized from Course)  
4. Recreate **Teaching Groups** + membership/sections (**Architect-approved TG workflow**)  
5. Recreate **Timetable / Entries**; allow **TimetableSection projector** to materialize — do not hand-write TimetableSection  
6. Resume Attendance against new operational structure  

**No automatic Student.SemesterId remap** in the reset scripts (Students preserved as-is).

---

## 7. Tenant-isolation analysis

| Check | Result |
| --- | --- |
| Destructive statements require `:tenant_id` | **PASS** (psql `\if :{?tenant_id}`) |
| Unscoped `DELETE FROM table;` | **None found** |
| Recognition review history (no TenantId column) | Deleted via join to `AttendanceRecognition."TenantId"` — **SAFE WITH CONDITION** |
| Cross-tenant leak if wrong tenant_id passed | **Caller risk** — mitigated only by College existence check, not by “test” proof |

---

## 8. Semester-specific safety analysis

| Concern | Script behavior | Gate |
| --- | --- | --- |
| DELETE `"Semester"` | Not present | PASS |
| UPDATE Semester.GroupId / CourseId | Not present | PASS |
| Recreate NULL-group operational Semesters | Not present | PASS |
| Student Semester remap | Not present | PASS |
| Legacy NULL-group Semesters remain as masters | Intentional preserve | PASS (archival is separate disposition prompts) |
| Future seed creating NULL-group operational rows | Out of scope of these scripts; **must be forbidden** in any later seed prompt | Conditional |

---

## 9. Teaching Group protection analysis

| Item | In reset SQL? | Governance |
| --- | --- | --- |
| `SchedulingTeachingGroupMembership` | YES DELETE | **REQUIRES CHIEF ARCHITECT APPROVAL** |
| `SchedulingTeachingGroupSection` | YES DELETE | **REQUIRES CHIEF ARCHITECT APPROVAL** |
| `SchedulingTeachingGroup` | YES DELETE | **REQUIRES CHIEF ARCHITECT APPROVAL** |
| `TimetableSections` | YES DELETE | **REQUIRES CHIEF ARCHITECT APPROVAL** (projector-owned; wipe ok only if TT entries also wiped and no bypass recreate) |
| CAP / ConflictEngine / Publish Gate code | Untouched | PASS |
| TG schema / capacity rules | Untouched | PASS |

Dependency-safe TG wipe sequence (only if Architect approves inclusion):

1. Delete TimetableEntry (clears Restrict TG refs) — already earlier in script  
2. Delete TimetableSections  
3. Delete TeachingGroupMembership  
4. Delete TeachingGroupSection  
5. Delete TeachingGroup  
6. Do **not** recreate TG in SQL; recreate via TG.4A–TG.6 application services later  

Until Architect explicitly approves TG inclusion **or** a revised script that **excludes** TG tables, execution remains **BLOCKED**.

---

## 10. Transaction strategy (future execution — not run now)

1. `BEGIN`  
2. Validate `:tenant_id` + College exists  
3. Snapshot protected counts  
4. Optional: assert PREVIEW counts within Architect-approved bands  
5. DELETE allowlist in FK order  
6. Re-check protected counts unchanged  
7. Assert core transactional residuals = 0  
8. `COMMIT` or exception → `ROLLBACK`  

Fail closed on any unexpected protected-count change or residual.

---

## 11. Idempotency analysis

| Aspect | Assessment |
| --- | --- |
| Second run after successful wipe | Allowlist DELETEs affect 0 rows; protected checks still pass; residual check still 0 → **idempotent for allowlist** |
| First run failure mid-transaction | Full rollback (PostgreSQL) → **safe** |
| REVIEW tables left non-zero | Second run still “succeeds” while residuals remain outside allowlist — document as **expected** |
| Journal / audit not written by SQL | No duplicate journal rows from this script |

---

## 12. Pre-execution checks (mandatory before any future execute prompt)

| # | Check | PASS/FAIL rule |
| --- | --- | --- |
| P1 | Architect written approval of TenantId + TG inclusion (or TG-excluded script) | FAIL → stop |
| P2 | Deterministic boundary certified (pre-prod tenant-wide OR marker) | FAIL → stop |
| P3 | Run PREVIEW.sql; attach counts to approval packet | FAIL if preview errors |
| P4 | Protected master counts recorded | FAIL if Student/Semester/… = 0 unexpectedly for a populated college |
| P5 | No production tenant sharing DB without explicit exclusion | FAIL → stop |
| P6 | Confirm no unscoped DELETE in script under review | FAIL → stop |
| P7 | Confirm script uses `"Semester"` not `"Semesters"` | FAIL → stop |
| P8 | Confirm Students will not be UPDATEd | FAIL → stop |

---

## 13. Post-execution validation contract (future)

| # | Validation |
| --- | --- |
| V1 | Allowlisted transactional counts = 0 for tenant |
| V2 | Student/Semester/Group/Course/Subject/Dept/Program/College counts unchanged vs pre-snapshot |
| V3 | No new operational Semesters with `GroupId IS NULL` created by reset (none should be created at all) |
| V4 | Course.DepartmentId SSOT still consistent; Program alignment if ProgramId set |
| V5 | Remaining Students still reference existing Semester/Group/Course rows |
| V6 | No TG rows if TG wipe approved; else TG integrity unchanged |
| V7 | TimetableSection count = 0 if wiped; projector remains sole writer going forward |
| V8 | CAP/TG/Publish architecture guards still pass (code unchanged) |
| V9 | EnablePrograms / TenantAcademicConfiguration unchanged |

---

## 14. SQL statement safety matrix

Legend: **SAFE** / **SAFE WITH CONDITION** / **UNSAFE** / **UNKNOWN — REQUIRES REVIEW**

### Non-destructive / control

| Statement | Classification | Notes |
| --- | --- | --- |
| Require `:tenant_id` | SAFE | Fail closed |
| College existence check | SAFE | Tenant resolve |
| TEMP protected snapshot | SAFE | No persistent mutation beyond temp |
| BEGIN/COMMIT + residual checks | SAFE | Fail closed |

### Destructive (all `DELETE … WHERE "TenantId" = :tenant_id` unless noted)

| Table | Op | Predicate | Classification | Protected-data risk | TG/Architect note |
| --- | --- | --- | --- | --- | --- |
| AttendanceRecognitionReviewHistory | DELETE | Join Recognition.TenantId | SAFE WITH CONDITION | Low | — |
| AttendanceDetail / Attendance / Recognition / SessionImage / Retry / Bulk / SessionSections / Session | DELETE | TenantId | SAFE WITH CONDITION | Low if tenant is pure test | Boundary B1 |
| ClassSchedule | DELETE | TenantId | SAFE WITH CONDITION | Low | Boundary B1 |
| SchedulingConflictFinding / DetectionRun | DELETE | TenantId | SAFE WITH CONDITION | Low | Boundary B1 |
| Optimization* (excl. TelemetryAggregate) | DELETE | TenantId | SAFE WITH CONDITION | Low | Boundary B1 |
| Timetable approval/history/clone/warning | DELETE | TenantId | SAFE WITH CONDITION | Low | Boundary B1 |
| TimetableSections | DELETE | TenantId | **UNSAFE without Architect approval** | Projector-owned | **B2** |
| SchedulingTimetableEntry / Timetable / ScheduleVersion | DELETE | TenantId | SAFE WITH CONDITION | Low | Boundary B1 |
| SchedulingTeachingGroupMembership | DELETE | TenantId | **UNSAFE without Architect approval** | TG architecture | **B2** |
| SchedulingTeachingGroupSection | DELETE | TenantId | **UNSAFE without Architect approval** | TG architecture | **B2** |
| SchedulingTeachingGroup | DELETE | TenantId | **UNSAFE without Architect approval** | TG architecture | **B2** |
| SchedulingSubjectAllocation | DELETE | TenantId | SAFE WITH CONDITION | Medium (planning) | Boundary B1 |
| AllocationEngine* / SectionAllocationSnapshots | DELETE | TenantId | SAFE WITH CONDITION | Low | Boundary B1 |
| StudentSections / FacultySectionAssignments / Section* / Sections | DELETE | TenantId | SAFE WITH CONDITION | Medium | Boundary B1; Sections are operational |

**Expected row counts:** *Not established in this gate* — must come from PREVIEW.sql in the execution approval packet (blocker B3). Placeholder until Architect-approved PREVIEW attachment:

| Bucket | Expected count |
| --- | --- |
| Allowlisted DELETE candidates (per table) | **UNKNOWN — run PREVIEW** |
| Protected masters (Student/Semester/Group/Course/Subject/Dept/Program/College) | **UNKNOWN — snapshot at execute time; must be unchanged post-COMMIT** |
| Residual allowlist after successful wipe | **0** (contract) |

**SQL statements reviewed:** 3 scripts (PREVIEW read-only, RESET 58 DELETEs + guards, VERIFY read-only). Classification summary: control statements **SAFE**; TG/TimetableSection DELETEs **UNSAFE without Architect approval**; remaining tenant-scoped DELETEs **SAFE WITH CONDITION** (condition = B1 boundary certification).

**Rollback:** Single PostgreSQL transaction; statements are not individually reversible after COMMIT (no automatic undelete). Recovery = restore from backup / re-seed.

---

## 15. Risks

1. Wiping an entire tenant’s operational scheduling/attendance history if TenantId is wrong or shared.  
2. TG/TimetableSection deletion without approved recreation path breaks scheduling until rebuilt correctly.  
3. Students may still point at legacy NULL-group Semesters after wipe (intentionally not remapped) — operational UI must use Group-owned Semesters going forward; separate Student remap is **out of scope** of this SQL.  
4. REVIEW residuals may confuse “clean” expectations.  
5. InMemory unit tests do not prove live FK order on PostgreSQL.

---

## 16. Blocking issues (summary)

1. **B1** — Non-deterministic test-data boundary  
2. **B2** — TG / TimetableSection deletion requires Chief Architect approval (or script revision to exclude them)  
3. **B3** — Live PREVIEW counts not attached  

---

## 17. Chief Architect approval gate

To move from **BLOCKED** to **RESET READY FOR CHIEF ARCHITECT APPROVAL** (still not auto-execute), Architect must approve **all** of:

- [ ] Named `TenantId` / College identity for wipe  
- [ ] Explicit statement that all allowlisted rows for that tenant are disposable test data **or** approve a new deterministic marker + script revision  
- [ ] Explicit **include** or **exclude** decision for Teaching Groups + TimetableSections  
- [ ] If include TG: approve recreation via application TG services only (no SQL TG invent)  
- [ ] Attach PREVIEW.sql count output to the approval packet  
- [ ] Confirm no Student Semester remap and no NULL-group Semester seed in follow-on work  

**This prompt stops here. Do not execute. Do not proceed to an execution prompt automatically.**

---

## Frozen architecture confirmation

| Rule | Status |
| --- | --- |
| Course.DepartmentId catalog SSOT | Untouched |
| EnablePrograms optional Programs | Untouched |
| SA/TT Department denormalization | Untouched |
| Group-owned Semester model | Preserved (Semesters not deleted) |
| Student Course→Group→Semester hierarchy | Students preserved; no remap |
| TG.4A–TG.6 / CAP / Publish Gate | Untouched in application code |
| TimetableSection projector ownership | Honored (SQL wipe flagged for Architect approval) |

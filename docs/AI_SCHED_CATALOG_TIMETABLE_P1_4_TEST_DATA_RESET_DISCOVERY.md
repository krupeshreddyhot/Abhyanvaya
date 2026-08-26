# AI-SCHED-CATALOG/TIMETABLE — P1-4 Test Data Reset  
# Discovery (DISCOVERY + SCRIPT DESIGN ONLY)

**Date:** 2026-08-24 · **Re-validated:** 2026-08-25 (safety gate re-audit; no script mutation)  
**Package:** `P1-4/TESTDATARESET`  
**Prompt scope:** Discovery + dry-run/reset/verify SQL + architecture guards  
**Database mutation in this prompt:** **NONE**  
**Schema / migrations in this prompt:** **NONE**  
**Application behavior changes:** **NONE** (guards + docs + scripts only)

---

## 1. Purpose

The Attendance, Section, Teaching Group, Subject Allocation, Timetable, and Scheduling rows in the local environment are **test data**.

Instead of remediating legacy Semester IDs through those transactional rows, the Architect approved a **tenant-scoped wipe** of that operational test data so administrators can recreate Sections / Teaching Groups / Timetables against the authoritative **Group-owned Semester** model.

This package **does not execute** the wipe. It only discovers, classifies, and publishes safe scripts.

---

## 2. Critical safety decisions

| Rule | Enforcement |
| --- | --- |
| Students preserved | Never in DELETE set; verify count unchanged |
| Semesters preserved | Never in DELETE set; no `UPDATE "Semester"` |
| Groups / Courses / Subjects / Depts / Programs / College preserved | Never in DELETE set |
| No `Student.SemesterId` / CourseId / GroupId updates | Explicitly out of scope (unlike prior 3HC1 C# service) |
| No schema / NOT NULL / UNIQUE | Out of scope |
| No TG/CAP/ConflictEngine/Publish code changes | Out of scope |
| Tenant isolation | Every DELETE filtered by `"TenantId"` (or join for tables without TenantId) |
| FK checks remain ON | No `session_replication_role`, no disable triggers, no MySQL `FOREIGN_KEY_CHECKS` |
| REVIEW tables | **Excluded** from DELETE script |

---

## 3. Naming facts (do not guess)

| Entity | Actual PostgreSQL table |
| --- | --- |
| Semester | `"Semester"` (**not** `Semesters`) |
| Student | `"Student"` |
| Group | `"Group"` |
| Course | `"Course"` |
| TeachingGroup | `"SchedulingTeachingGroup"` |
| TeachingGroupMembership | `"SchedulingTeachingGroupMembership"` |
| TeachingGroupSection | `"SchedulingTeachingGroupSection"` |
| SubjectAllocation | `"SchedulingSubjectAllocation"` |
| TimetableEntry | `"SchedulingTimetableEntry"` |
| Timetable | `"SchedulingTimetable"` |
| ScheduleVersion | `"SchedulingScheduleVersion"` |
| TimetableSection | `"TimetableSections"` |
| Section | `"Sections"` |
| StudentSection | `"StudentSections"` |

Evidence: EF Fluent `ToTable(...)` / model snapshot / TG.3 hand migrations.

---

## 4. Dependency graph (derived)

```
PRESERVE: Student / Semester / Group / Course / Subject / Department / Program / College / Auth / Config
    │
    │  (FKs Restrict into masters — masters never deleted)
    ▼
AttendanceRecognitionReviewHistory ──► AttendanceRecognition ──► AttendanceSession
AttendanceDetail ──► Attendance ──► AttendanceSession (Restrict: delete Attendance first)
AttendanceSessionImage / RetryHistory / AttendanceSessionSections ──► AttendanceSession
AttendanceSession ──(SetNull)──► ClassSchedule
ClassSchedule ──Restrict──► Course/Group/Semester/Subject/Staff

SchedulingConflictFinding ──Cascade──► ConflictDetectionRun ──Restrict──► Timetable
Timetable approval children ──Cascade──► ApprovalRequest ──Restrict──► Timetable/ScheduleVersion
TimetableChangeHistory / WarningDismissal ──Cascade──► Timetable
TimetableSections (logical) ──► TimetableEntry / Section
SchedulingTimetableEntry ──Cascade──► Timetable
                       ──Restrict──► TeachingGroup / SubjectAllocation / masters
SchedulingTimetable ──Restrict──► ScheduleVersion / AcademicYear
SchedulingScheduleVersion

SchedulingTeachingGroupMembership ──Cascade──► TeachingGroup; Restrict──► Student
SchedulingTeachingGroupSection ──Cascade──► TeachingGroup; Restrict──► Section
SchedulingTeachingGroup ──Restrict──► SubjectAllocation / masters
SchedulingSubjectAllocation ──Restrict──► masters

StudentSections / FacultySectionAssignments / Section* children ──► Sections
AllocationEngine* sandbox ──► Section allocation workflows
Sections
```

---

## 5. Delete candidates (allowlist)

See matrix document for full rows. Summary:

**Attendance:** RecognitionReviewHistory, Detail, Attendance, Recognition, SessionImage, RetryHistory, BulkOperationHistory, AttendanceSessionSections, AttendanceSession, ClassSchedule  

**Scheduling TT:** ConflictFinding, ConflictDetectionRun, Optimization scenario graph (excl. telemetry aggregate), Timetable approval/history/clone/warning, TimetableSections, TimetableEntry, Timetable, ScheduleVersion  

**TG / SA:** TeachingGroupMembership, TeachingGroupSection, TeachingGroup, SubjectAllocation  

**Sections / allocation ops:** AllocationEngine*, SectionAllocationSnapshots, StudentSections, FacultySectionAssignments, SectionGroupMembers, lifecycle/merge/split/lineage/version/capacity history, SectionGroups, Sections  

---

## 6. Preserved (denylist)

Student, Semester, Group, Course, Subject, Department, Programs, College, University, User, Permission, ApplicationRole*, Staff*, AcademicYear/Term, Campus/Building/Floor/Room, TimeSlot*, ArchiveReason lookup, TenantAcademicConfigurations, ProgramPolicies, TenantSectionCapacityPolicies, ConflictRuleThresholdSetting, faculty preferences/workload/availability, WorkspacePreference, AttendanceRecoveryPreference, StudentFaceEmbedding, face/enrollment pipelines, AuditEntry (global), LegacySemesterDispositionJournals  

---

## 7. Ambiguous / REVIEW (excluded from DELETE)

| Table / entity | Reason |
| --- | --- |
| `SchedulingConflictWorkspacePin/Bookmark/Note` | UI workspace; ownership unclear vs config |
| `SchedulingConflictRuleConfigChangeHistory` | Config audit |
| `SchedulingOptimizationTelemetryAggregate` | Rolling telemetry / unique keys |
| `SectionPolicies` | May be tenant policy |
| `SectionAllocationPreferences` | Preference vs transactional |
| `RoomAllocationRule` / `RoomAvailability` | Facility rules |
| Soft-deleted-only residuals outside allowlist | Not scanned for hard delete |

---

## 8. Scripts

| File | Role |
| --- | --- |
| `scripts/AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_PREVIEW.sql` | Counts only |
| `scripts/AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET.sql` | Transactional DELETE (manual run later) |
| `scripts/AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_VERIFY.sql` | Post checks |

**Invocation (future, Architect-approved):**

```bash
psql ... -v tenant_id=1 -f ..._PREVIEW.sql
psql ... -v tenant_id=1 -f ..._TEST_DATA_RESET.sql
psql ... -v tenant_id=1 -f ..._VERIFY.sql
```

---

## 9. Explicit non-goals

- Do **not** run these scripts in this prompt  
- Do **not** call SaveChanges / MigrateAsync as part of this work  
- Do **not** reconcile Student.SemesterId here  
- Do **not** reset identity/sequences  
- Do **not** weaken TG/CAP/Publish architecture  

**STOP** after discovery + scripts + guards.

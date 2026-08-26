# AI-SCHED-CATALOG/TIMETABLE — P1-4 Prompt 3H  
# Pre-Production Transactional Data Reset & Student Semester Reconciliation

**Date:** 2026-08-23  
**Architect package:** `P1-4/3HC1`  
**Implementation PromptCode:** `P1-4-3HC1`  
*(Distinct from existing read-only Prompt 3H post-section integrity audit.)*

**API**
- `GET  /api/academic-data/preproduction-cleanup/preview`
- `POST /api/academic-data/preproduction-cleanup/execute`

**Auth:** `CanManageSemesters`

---

## 1. Purpose

Establish a clean pre-production baseline by removing obsolete **Attendance**, **Scheduling/Timetable**, and **Teaching Group** transactional data, then reconciling **Student.SemesterId** via authoritative **Group → Semester** ownership.

This avoids further complex legacy Semester-3 transactional remediation.

## 2. Scope

| In scope | Out of scope |
| --- | --- |
| Attendance session graph | Student / Course / Group / Semester deletes |
| Timetable designer + governance + conflict runs + optimization sandbox | Department / Program / College / Subject master |
| TeachingGroup + membership + section links | AuthZ / tenant config / feature flags |
| SubjectAllocation (scheduling transactions) | Schema NOT NULL / UNIQUE hardening |
| Student.SemesterId updates (deterministic only) | CAP / Publish Gate / TG architecture changes |

## 3. Entity classification (discovery-backed)

Classifications use EF Fluent FK graphs and prior TG.3 disposable cutover evidence — not table-name inference alone.

### DELETE (allowlist)

Attendance: `AttendanceRecognitionReviewHistory`, `AttendanceDetail`, `Attendance`, `AttendanceRecognition`, `AttendanceSessionImage`, `AttendanceRetryHistory`, `AttendanceSessionSection`, `AttendanceSession`, `ClassSchedule`, `AttendanceBulkOperationHistory`

Scheduling: conflict findings/runs/workspace pins, timetable approval/history/clone/warning, `TimetableSection`, `TimetableEntry`, `Timetable`, `ScheduleVersion`, optimization scenario/run/snapshot/telemetry graph, `TeachingGroupMembership`, `TeachingGroupSection`, `TeachingGroup`, `SubjectAllocation`

### PRESERVE (denylist)

`Student`, `Course`, `Group`, `Semester`, `Department`, `Program`, `College`/`University`, `Subject`/`TenantSubject`/`ElectiveGroup`, `Section`/`StudentSection`, Users/Roles/Permissions, `TenantAcademicConfiguration`/`ProgramPolicy`, Staff, calendar/facility/time-slot masters, conflict **threshold settings**, preferences, face enrollment, faculty preference/workload, `LegacySemesterDispositionJournal`

## 4–5. Allowlist / denylist

Encoded in `PreProductionTransactionalResetAllowlist`. Execution may mutate **only** allowlisted entities (plus Student.SemesterId updates and a disposition journal row).

## 6–7. FK / deletion order

Dependents first (Restrict-aware):

1. Attendance children → Attendance → Session children → Session → ClassSchedule → bulk history  
2. Conflict findings/workspace → runs  
3. Timetable approval children → TT projections/entries → Timetable → ScheduleVersion  
4. Optimization nested → scenarios/runs  
5. TeachingGroupMembership / TeachingGroupSection → TeachingGroup  
6. SubjectAllocation  

**FAIL CLOSED cascades into protected data:** deleting Course/Group/Semester/Student/Subject is forbidden (snapshot Cascade would destroy Students).

## 8. Student Semester resolution

Authoritative:

`Student.CourseId` + `Student.GroupId` → validate `Group.CourseId` → operational Semesters where `Semester.GroupId == Student.GroupId` and `!IsHistoricalArchive`.

Rules:

- Unique operational Semester for Group → resolve to that Id  
- Multiple → resolve only if exactly one matches the student’s current Semester.**Number**  
- Else `AMBIGUOUS` / `NO_SEMESTER` / `INVALID_GROUP` / `COURSE_GROUP_MISMATCH` → **fail closed** (no batch mutations)

**No hard-coded Semester 10/11** in business logic. Local Finance→10 / CA→11 is an expected *outcome* of Group ownership, not an embedded constant.

Eligible mutation statuses: `ALREADY_CORRECT`, `UPDATE_REQUIRED` only.

## 9–10. Preview / execute

Preview is read-only (`SaveChangesInvoked=false`).

Execute requires:

```json
{
  "confirm": true,
  "confirmationPhrase": "PREPRODUCTION_TRANSACTIONAL_RESET",
  "reason": "optional"
}
```

Flow: re-preview → re-reconcile students → single `ExecuteInTransactionAsync` → allowlist `Remove` (via `QueryIgnoringFilters` + TenantId) → Student updates → journal (`PromptCode=P1-4-3HC1`) → SaveChanges → protected-count + residual + student integrity checks → commit or rollback.

## 11–13. Transaction / rollback / idempotency

- **ALL_OR_NOTHING** one transaction  
- No TRUNCATE / DROP / raw SQL wipe  
- Second run with zero transactional rows + zero UPDATE_REQUIRED → `AlreadyComplete` / `IdempotentZeroMutation=true`  
- Unit-test hook proves abort path after simulated mid-transaction failure  

## 14. Tenant isolation

Every wipe query filters `TenantId == ambient`. Cross-tenant rows remain.

## 15. Before/after audit

Preview reports protected + allowlist counts and reconciliation rows. Execute returns protected before/after, deleted counts, students updated, post-integrity flag.

## 16. Test evidence

Unit + architecture guards: allowlist wipe, student reconcile without hard-coded Ids, idempotency, ambiguous fail-closed, cross-tenant preservation, confirmation gate, no TRUNCATE/Student remove, API docs present.

## 17. Known residual

- Soft-deleted rows outside allowlist unchanged  
- Section / StudentSection / faculty preferences retained by design  
- Legacy NULL-group Semester **master** rows retained (not deleted)  
- Schema hardening still deferred  

## 18. Deliberately NOT deleted

Student/master/config/auth/Sections/StudentSections/face enrollment/faculty prefs/rooms/calendar/conflict threshold settings/journals.

---

**STOP after Prompt 3HC1.** Do not auto-start Semester NOT NULL / UNIQUE hardening.

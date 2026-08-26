-- =============================================================================
-- AI-SCHED-CATALOG/TIMETABLE P1-4 TEST DATA RESET — SAFE DELETE SCRIPT
-- Package: TESTDATARESET
--
-- *** DO NOT RUN DURING THE DISCOVERY PROMPT ***
-- Architect-approved manual execution only, after PREVIEW review.
--
-- Usage (future):
--   psql -v tenant_id=1 -f AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_PREVIEW.sql
--   psql -v tenant_id=1 -f AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET.sql
--   psql -v tenant_id=1 -f AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_VERIFY.sql
--
-- Guarantees:
--   - Single transaction (BEGIN/COMMIT); any error => ROLLBACK
--   - Tenant isolation via :tenant_id
--   - Foreign keys stay enforced (no bypass mechanisms)
--   - Never DELETE/UPDATE Student, Semester, Group, Course, Subject, Department, Programs, College
--   - Never UPDATE Student.SemesterId / CourseId / GroupId
--   - Never change schema or identity sequences
--   - REVIEW-classified tables are NOT included
-- =============================================================================

\if :{?tenant_id}
\else
\echo 'FATAL: tenant_id is required. Example: psql -v tenant_id=1 -f ..._RESET.sql'
\quit 1
\endif

\set ON_ERROR_STOP on

BEGIN;

-- Fail closed: tenant must resolve to an existing College row.
DO $$
DECLARE
  v_tenant int := :tenant_id;
  v_college_count int;
BEGIN
  IF v_tenant IS NULL OR v_tenant <= 0 THEN
    RAISE EXCEPTION 'FAIL CLOSED: tenant_id missing or invalid (%)', v_tenant;
  END IF;

  SELECT COUNT(*) INTO v_college_count
  FROM "College"
  WHERE "TenantId" = v_tenant AND COALESCE("IsDeleted", false) = false;

  IF v_college_count < 1 THEN
    RAISE EXCEPTION 'FAIL CLOSED: no College found for TenantId=%', v_tenant;
  END IF;
END $$;

-- Snapshot protected counts (checked again before COMMIT).
CREATE TEMP TABLE _p14_reset_protected AS
SELECT
  (SELECT COUNT(*) FROM "Student" WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false) AS students,
  (SELECT COUNT(*) FROM "Semester" WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false) AS semesters,
  (SELECT COUNT(*) FROM "Group" WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false) AS groups,
  (SELECT COUNT(*) FROM "Course" WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false) AS courses,
  (SELECT COUNT(*) FROM "Subject" WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false) AS subjects,
  (SELECT COUNT(*) FROM "Department" WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false) AS departments,
  (SELECT COUNT(*) FROM "Programs" WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false) AS programs,
  (SELECT COUNT(*) FROM "College" WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false) AS colleges;

-- ---------------------------------------------------------------------------
-- 1) Attendance graph (children before session; Attendance before Session Restrict)
-- ---------------------------------------------------------------------------
DELETE FROM "AttendanceRecognitionReviewHistory" h
USING "AttendanceRecognition" r
WHERE h."RecognitionId" = r."Id" AND r."TenantId" = :tenant_id;

DELETE FROM "AttendanceDetail" WHERE "TenantId" = :tenant_id;
DELETE FROM "Attendance" WHERE "TenantId" = :tenant_id;
DELETE FROM "AttendanceRecognition" WHERE "TenantId" = :tenant_id;
DELETE FROM "AttendanceSessionImage" WHERE "TenantId" = :tenant_id;
DELETE FROM "AttendanceRetryHistory" WHERE "TenantId" = :tenant_id;
DELETE FROM "AttendanceBulkOperationHistory" WHERE "TenantId" = :tenant_id;
DELETE FROM "AttendanceSessionSections" WHERE "TenantId" = :tenant_id;
DELETE FROM "AttendanceSession" WHERE "TenantId" = :tenant_id;
DELETE FROM "ClassSchedule" WHERE "TenantId" = :tenant_id;

-- ---------------------------------------------------------------------------
-- 2) Conflict runs (findings before runs)
-- ---------------------------------------------------------------------------
DELETE FROM "SchedulingConflictFinding" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingConflictDetectionRun" WHERE "TenantId" = :tenant_id;

-- ---------------------------------------------------------------------------
-- 3) Optimization sandbox
-- ---------------------------------------------------------------------------
DELETE FROM "SchedulingOptimizationSnapshot" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationScenarioFavorite" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationScenarioNote" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationScenarioComment" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationScenarioBookmark" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationScenarioApprovalRequest" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationScenarioShare" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationScenarioHistory" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationScenario" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationEngineRun" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationSimulationRun" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingOptimizationMetricSnapshot" WHERE "TenantId" = :tenant_id;

-- ---------------------------------------------------------------------------
-- 4) Timetable governance + designer (entries before TG Restrict)
-- ---------------------------------------------------------------------------
DELETE FROM "SchedulingTimetableApprovalStep" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTimetableApprovalHistory" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTimetableApprovalComment" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTimetableDecisionHistory" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTimetableApprovalRequest" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTimetableChangeHistory" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTimetableWarningDismissal" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTimetableCloneJob" WHERE "TenantId" = :tenant_id;
DELETE FROM "TimetableSections" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTimetableEntry" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTimetable" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingScheduleVersion" WHERE "TenantId" = :tenant_id;

-- ---------------------------------------------------------------------------
-- 5) Teaching Groups then SubjectAllocation
-- ---------------------------------------------------------------------------
DELETE FROM "SchedulingTeachingGroupMembership" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTeachingGroupSection" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingTeachingGroup" WHERE "TenantId" = :tenant_id;
DELETE FROM "SchedulingSubjectAllocation" WHERE "TenantId" = :tenant_id;

-- ---------------------------------------------------------------------------
-- 6) Allocation engine sandbox + Section operational graph
-- ---------------------------------------------------------------------------
DELETE FROM "AllocationEngineSandboxItems" WHERE "TenantId" = :tenant_id;
DELETE FROM "AllocationEngineDrafts" WHERE "TenantId" = :tenant_id;
DELETE FROM "AllocationScenarioVersions" WHERE "TenantId" = :tenant_id;
DELETE FROM "AllocationAuditEntries" WHERE "TenantId" = :tenant_id;
DELETE FROM "AllocationEngineScenarios" WHERE "TenantId" = :tenant_id;
DELETE FROM "AllocationEngineSessions" WHERE "TenantId" = :tenant_id;
DELETE FROM "SectionAllocationSnapshots" WHERE "TenantId" = :tenant_id;
DELETE FROM "StudentSections" WHERE "TenantId" = :tenant_id;
DELETE FROM "FacultySectionAssignments" WHERE "TenantId" = :tenant_id;
DELETE FROM "SectionGroupMembers" WHERE "TenantId" = :tenant_id;
DELETE FROM "SectionLifecycleTransitions" WHERE "TenantId" = :tenant_id;
DELETE FROM "SectionMergeTransactions" WHERE "TenantId" = :tenant_id;
DELETE FROM "SectionSplitTransactions" WHERE "TenantId" = :tenant_id;
DELETE FROM "SectionLineages" WHERE "TenantId" = :tenant_id;
DELETE FROM "SectionVersions" WHERE "TenantId" = :tenant_id;
DELETE FROM "SectionCapacityHistories" WHERE "TenantId" = :tenant_id;
DELETE FROM "SectionGroups" WHERE "TenantId" = :tenant_id;
DELETE FROM "Sections" WHERE "TenantId" = :tenant_id;

-- ---------------------------------------------------------------------------
-- Post-delete guards inside the same transaction
-- ---------------------------------------------------------------------------
DO $$
DECLARE
  v_tenant int := :tenant_id;
  b record;
  a_students int; a_semesters int; a_groups int; a_courses int;
  a_subjects int; a_departments int; a_programs int; a_colleges int;
  residual bigint;
BEGIN
  SELECT * INTO b FROM _p14_reset_protected;

  SELECT COUNT(*) INTO a_students FROM "Student"
    WHERE "TenantId" = v_tenant AND COALESCE("IsDeleted", false) = false;
  SELECT COUNT(*) INTO a_semesters FROM "Semester"
    WHERE "TenantId" = v_tenant AND COALESCE("IsDeleted", false) = false;
  SELECT COUNT(*) INTO a_groups FROM "Group"
    WHERE "TenantId" = v_tenant AND COALESCE("IsDeleted", false) = false;
  SELECT COUNT(*) INTO a_courses FROM "Course"
    WHERE "TenantId" = v_tenant AND COALESCE("IsDeleted", false) = false;
  SELECT COUNT(*) INTO a_subjects FROM "Subject"
    WHERE "TenantId" = v_tenant AND COALESCE("IsDeleted", false) = false;
  SELECT COUNT(*) INTO a_departments FROM "Department"
    WHERE "TenantId" = v_tenant AND COALESCE("IsDeleted", false) = false;
  SELECT COUNT(*) INTO a_programs FROM "Programs"
    WHERE "TenantId" = v_tenant AND COALESCE("IsDeleted", false) = false;
  SELECT COUNT(*) INTO a_colleges FROM "College"
    WHERE "TenantId" = v_tenant AND COALESCE("IsDeleted", false) = false;

  IF a_students <> b.students OR a_semesters <> b.semesters OR a_groups <> b.groups
     OR a_courses <> b.courses OR a_subjects <> b.subjects OR a_departments <> b.departments
     OR a_programs <> b.programs OR a_colleges <> b.colleges THEN
    RAISE EXCEPTION 'FAIL CLOSED: protected master counts changed (Student/Semester/Group/Course/Subject/Dept/Program/College).';
  END IF;

  SELECT
      (SELECT COUNT(*) FROM "Attendance" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "AttendanceSession" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "Sections" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "StudentSections" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingTeachingGroup" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingTeachingGroupMembership" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingSubjectAllocation" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingTimetableEntry" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "TimetableSections" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingTimetable" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingScheduleVersion" WHERE "TenantId" = v_tenant)
  INTO residual;

  IF residual <> 0 THEN
    RAISE EXCEPTION 'FAIL CLOSED: residual transactional rows remain (residual=%)', residual;
  END IF;
END $$;

COMMIT;

\echo 'RESET COMMITTED for tenant_id=' :tenant_id
\echo 'Run VERIFY.sql next. REVIEW tables (if any) were intentionally not deleted.'

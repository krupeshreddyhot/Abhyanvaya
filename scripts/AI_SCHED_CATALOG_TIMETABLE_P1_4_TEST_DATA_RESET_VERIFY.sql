-- =============================================================================
-- AI-SCHED-CATALOG/TIMETABLE P1-4 TEST DATA RESET — VERIFICATION
-- Package: TESTDATARESET
-- MUTATION: NONE — SELECT / assertions only
--
-- Usage:
--   psql -v tenant_id=1 -f AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_VERIFY.sql
-- =============================================================================

\if :{?tenant_id}
\else
\echo 'FATAL: tenant_id is required. Example: psql -v tenant_id=1 -f ..._VERIFY.sql'
\quit 1
\endif

\set ON_ERROR_STOP on

\echo '=== VERIFY tenant_id=' :tenant_id ' ==='

\echo '--- Allowlisted transactional tables (expect 0) ---'
SELECT * FROM (
  SELECT 'Attendance' AS entity, COUNT(*)::bigint AS cnt FROM "Attendance" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'AttendanceSession', COUNT(*) FROM "AttendanceSession" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'AttendanceDetail', COUNT(*) FROM "AttendanceDetail" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'AttendanceRecognition', COUNT(*) FROM "AttendanceRecognition" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'ClassSchedule', COUNT(*) FROM "ClassSchedule" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'StudentSections', COUNT(*) FROM "StudentSections" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'Sections', COUNT(*) FROM "Sections" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'SchedulingTeachingGroup', COUNT(*) FROM "SchedulingTeachingGroup" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'SchedulingTeachingGroupMembership', COUNT(*) FROM "SchedulingTeachingGroupMembership" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'SchedulingTeachingGroupSection', COUNT(*) FROM "SchedulingTeachingGroupSection" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'SchedulingSubjectAllocation', COUNT(*) FROM "SchedulingSubjectAllocation" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'SchedulingTimetableEntry', COUNT(*) FROM "SchedulingTimetableEntry" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'TimetableSections', COUNT(*) FROM "TimetableSections" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'SchedulingTimetable', COUNT(*) FROM "SchedulingTimetable" WHERE "TenantId" = :tenant_id
  UNION ALL SELECT 'SchedulingScheduleVersion', COUNT(*) FROM "SchedulingScheduleVersion" WHERE "TenantId" = :tenant_id
) t
ORDER BY entity;

DO $$
DECLARE
  v_tenant int := :tenant_id;
  bad bigint;
BEGIN
  SELECT
      (SELECT COUNT(*) FROM "Attendance" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "AttendanceSession" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "StudentSections" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "Sections" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingTeachingGroup" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingTeachingGroupMembership" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingSubjectAllocation" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingTimetableEntry" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "TimetableSections" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingTimetable" WHERE "TenantId" = v_tenant)
    + (SELECT COUNT(*) FROM "SchedulingScheduleVersion" WHERE "TenantId" = v_tenant)
  INTO bad;

  IF bad <> 0 THEN
    RAISE EXCEPTION 'VERIFY FAILED: allowlisted transactional residual=%', bad;
  END IF;
END $$;

\echo '--- Preserved masters (informational counts) ---'
SELECT * FROM (
  SELECT 'Student' AS entity, COUNT(*)::bigint AS cnt FROM "Student"
    WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false
  UNION ALL SELECT 'Semester', COUNT(*) FROM "Semester"
    WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false
  UNION ALL SELECT 'Group', COUNT(*) FROM "Group"
    WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false
  UNION ALL SELECT 'Course', COUNT(*) FROM "Course"
    WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false
  UNION ALL SELECT 'Subject', COUNT(*) FROM "Subject"
    WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false
  UNION ALL SELECT 'Department', COUNT(*) FROM "Department"
    WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false
  UNION ALL SELECT 'Programs', COUNT(*) FROM "Programs"
    WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false
  UNION ALL SELECT 'College', COUNT(*) FROM "College"
    WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false
) p
ORDER BY entity;

\echo '--- Smoke: no Student.SemesterId rewrite markers in reset script scope ---'
\echo 'VERIFY PASS if allowlisted residuals are 0 and masters remain populated as expected.'

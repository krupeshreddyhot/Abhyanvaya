-- =============================================================================
-- AI-SCHED-CATALOG/TIMETABLE P1-4 TEST DATA RESET — PREVIEW (DRY-RUN)
-- Package: TESTDATARESET
-- MUTATION: NONE — SELECT counts only
--
-- Usage:
--   psql -v tenant_id=1 -f AI_SCHED_CATALOG_TIMETABLE_P1_4_TEST_DATA_RESET_PREVIEW.sql
--
-- Fail closed if tenant_id is not provided by the caller.
-- =============================================================================

\if :{?tenant_id}
\else
\echo 'FATAL: tenant_id is required. Example: psql -v tenant_id=1 -f ..._PREVIEW.sql'
\quit 1
\endif

\echo '=== PREVIEW tenant_id=' :tenant_id ' ==='

SELECT 'College' AS entity, COUNT(*)::bigint AS cnt
FROM "College" WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false;

\echo '--- DELETE candidates (would be wiped) ---'

SELECT * FROM (
  SELECT 1 AS ord, 'AttendanceRecognitionReviewHistory' AS entity,
         (SELECT COUNT(*) FROM "AttendanceRecognitionReviewHistory" h
          INNER JOIN "AttendanceRecognition" r ON r."Id" = h."RecognitionId"
          WHERE r."TenantId" = :tenant_id) AS cnt
  UNION ALL SELECT 2, 'AttendanceDetail',
         (SELECT COUNT(*) FROM "AttendanceDetail" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 3, 'Attendance',
         (SELECT COUNT(*) FROM "Attendance" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 4, 'AttendanceRecognition',
         (SELECT COUNT(*) FROM "AttendanceRecognition" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 5, 'AttendanceSessionImage',
         (SELECT COUNT(*) FROM "AttendanceSessionImage" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 6, 'AttendanceRetryHistory',
         (SELECT COUNT(*) FROM "AttendanceRetryHistory" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 7, 'AttendanceBulkOperationHistory',
         (SELECT COUNT(*) FROM "AttendanceBulkOperationHistory" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 8, 'AttendanceSessionSections',
         (SELECT COUNT(*) FROM "AttendanceSessionSections" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 9, 'AttendanceSession',
         (SELECT COUNT(*) FROM "AttendanceSession" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 10, 'ClassSchedule',
         (SELECT COUNT(*) FROM "ClassSchedule" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 11, 'SchedulingConflictFinding',
         (SELECT COUNT(*) FROM "SchedulingConflictFinding" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 12, 'SchedulingConflictDetectionRun',
         (SELECT COUNT(*) FROM "SchedulingConflictDetectionRun" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 13, 'SchedulingOptimizationSnapshot',
         (SELECT COUNT(*) FROM "SchedulingOptimizationSnapshot" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 14, 'SchedulingOptimizationScenarioFavorite',
         (SELECT COUNT(*) FROM "SchedulingOptimizationScenarioFavorite" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 15, 'SchedulingOptimizationScenarioNote',
         (SELECT COUNT(*) FROM "SchedulingOptimizationScenarioNote" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 16, 'SchedulingOptimizationScenarioComment',
         (SELECT COUNT(*) FROM "SchedulingOptimizationScenarioComment" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 17, 'SchedulingOptimizationScenarioBookmark',
         (SELECT COUNT(*) FROM "SchedulingOptimizationScenarioBookmark" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 18, 'SchedulingOptimizationScenarioApprovalRequest',
         (SELECT COUNT(*) FROM "SchedulingOptimizationScenarioApprovalRequest" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 19, 'SchedulingOptimizationScenarioShare',
         (SELECT COUNT(*) FROM "SchedulingOptimizationScenarioShare" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 20, 'SchedulingOptimizationScenarioHistory',
         (SELECT COUNT(*) FROM "SchedulingOptimizationScenarioHistory" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 21, 'SchedulingOptimizationScenario',
         (SELECT COUNT(*) FROM "SchedulingOptimizationScenario" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 22, 'SchedulingOptimizationEngineRun',
         (SELECT COUNT(*) FROM "SchedulingOptimizationEngineRun" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 23, 'SchedulingOptimizationSimulationRun',
         (SELECT COUNT(*) FROM "SchedulingOptimizationSimulationRun" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 24, 'SchedulingOptimizationMetricSnapshot',
         (SELECT COUNT(*) FROM "SchedulingOptimizationMetricSnapshot" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 25, 'SchedulingTimetableApprovalStep',
         (SELECT COUNT(*) FROM "SchedulingTimetableApprovalStep" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 26, 'SchedulingTimetableApprovalHistory',
         (SELECT COUNT(*) FROM "SchedulingTimetableApprovalHistory" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 27, 'SchedulingTimetableApprovalComment',
         (SELECT COUNT(*) FROM "SchedulingTimetableApprovalComment" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 28, 'SchedulingTimetableDecisionHistory',
         (SELECT COUNT(*) FROM "SchedulingTimetableDecisionHistory" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 29, 'SchedulingTimetableApprovalRequest',
         (SELECT COUNT(*) FROM "SchedulingTimetableApprovalRequest" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 30, 'SchedulingTimetableChangeHistory',
         (SELECT COUNT(*) FROM "SchedulingTimetableChangeHistory" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 31, 'SchedulingTimetableWarningDismissal',
         (SELECT COUNT(*) FROM "SchedulingTimetableWarningDismissal" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 32, 'SchedulingTimetableCloneJob',
         (SELECT COUNT(*) FROM "SchedulingTimetableCloneJob" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 33, 'TimetableSections',
         (SELECT COUNT(*) FROM "TimetableSections" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 34, 'SchedulingTimetableEntry',
         (SELECT COUNT(*) FROM "SchedulingTimetableEntry" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 35, 'SchedulingTimetable',
         (SELECT COUNT(*) FROM "SchedulingTimetable" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 36, 'SchedulingScheduleVersion',
         (SELECT COUNT(*) FROM "SchedulingScheduleVersion" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 37, 'SchedulingTeachingGroupMembership',
         (SELECT COUNT(*) FROM "SchedulingTeachingGroupMembership" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 38, 'SchedulingTeachingGroupSection',
         (SELECT COUNT(*) FROM "SchedulingTeachingGroupSection" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 39, 'SchedulingTeachingGroup',
         (SELECT COUNT(*) FROM "SchedulingTeachingGroup" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 40, 'SchedulingSubjectAllocation',
         (SELECT COUNT(*) FROM "SchedulingSubjectAllocation" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 41, 'AllocationEngineSandboxItems',
         (SELECT COUNT(*) FROM "AllocationEngineSandboxItems" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 42, 'AllocationEngineDrafts',
         (SELECT COUNT(*) FROM "AllocationEngineDrafts" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 43, 'AllocationScenarioVersions',
         (SELECT COUNT(*) FROM "AllocationScenarioVersions" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 44, 'AllocationAuditEntries',
         (SELECT COUNT(*) FROM "AllocationAuditEntries" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 45, 'AllocationEngineScenarios',
         (SELECT COUNT(*) FROM "AllocationEngineScenarios" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 46, 'AllocationEngineSessions',
         (SELECT COUNT(*) FROM "AllocationEngineSessions" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 47, 'SectionAllocationSnapshots',
         (SELECT COUNT(*) FROM "SectionAllocationSnapshots" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 48, 'StudentSections',
         (SELECT COUNT(*) FROM "StudentSections" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 49, 'FacultySectionAssignments',
         (SELECT COUNT(*) FROM "FacultySectionAssignments" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 50, 'SectionGroupMembers',
         (SELECT COUNT(*) FROM "SectionGroupMembers" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 51, 'SectionLifecycleTransitions',
         (SELECT COUNT(*) FROM "SectionLifecycleTransitions" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 52, 'SectionMergeTransactions',
         (SELECT COUNT(*) FROM "SectionMergeTransactions" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 53, 'SectionSplitTransactions',
         (SELECT COUNT(*) FROM "SectionSplitTransactions" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 54, 'SectionLineages',
         (SELECT COUNT(*) FROM "SectionLineages" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 55, 'SectionVersions',
         (SELECT COUNT(*) FROM "SectionVersions" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 56, 'SectionCapacityHistories',
         (SELECT COUNT(*) FROM "SectionCapacityHistories" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 57, 'SectionGroups',
         (SELECT COUNT(*) FROM "SectionGroups" WHERE "TenantId" = :tenant_id)
  UNION ALL SELECT 58, 'Sections',
         (SELECT COUNT(*) FROM "Sections" WHERE "TenantId" = :tenant_id)
) d
ORDER BY ord;

\echo '--- PRESERVED masters (must remain unchanged by reset) ---'

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
  UNION ALL SELECT 'User', COUNT(*) FROM "User"
    WHERE "TenantId" = :tenant_id AND COALESCE("IsDeleted", false) = false
) p
ORDER BY entity;

\echo 'PREVIEW complete — no mutations performed.'

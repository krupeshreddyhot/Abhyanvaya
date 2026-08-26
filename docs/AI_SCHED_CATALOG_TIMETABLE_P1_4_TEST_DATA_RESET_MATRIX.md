# AI-SCHED-CATALOG/TIMETABLE — P1-4 Test Data Reset  
# Classification Matrix

**Package:** `P1-4/TESTDATARESET`  
**Mutation in this prompt:** NONE  

Legend: **DELETE** = allowlisted in reset SQL · **PRESERVE** = denylist · **REVIEW** = excluded from reset SQL

---

## DELETE allowlist

| Entity | Table | Purpose | Classification | FK dependencies (summary) |
| --- | --- | --- | --- | --- |
| AttendanceRecognitionReviewHistory | `AttendanceRecognitionReviewHistory` | Recognition review audit | DELETE | → AttendanceRecognition (Cascade); no TenantId — delete via join |
| AttendanceDetail | `AttendanceDetail` | Attendance capture detail | DELETE | → Attendance (Cascade) |
| Attendance | `Attendance` | Official attendance rows | DELETE | → AttendanceSession (Restrict); Student/Subject |
| AttendanceRecognition | `AttendanceRecognition` | AI provisional faces | DELETE | → AttendanceSession (Cascade); Student Restrict |
| AttendanceSessionImage | `AttendanceSessionImage` | Session images | DELETE | → AttendanceSession (Cascade) |
| AttendanceRetryHistory | `AttendanceRetryHistory` | Retry audit | DELETE | → AttendanceSession (Cascade) |
| AttendanceBulkOperationHistory | `AttendanceBulkOperationHistory` | Bulk assist audit | DELETE | Tenant-scoped |
| AttendanceSessionSection | `AttendanceSessionSections` | Session↔Section | DELETE | Logical Session/Section |
| AttendanceSession | `AttendanceSession` | Core sessions | DELETE | Restrict to masters; SetNull ClassSchedule |
| ClassSchedule | `ClassSchedule` | Legacy schedule slots | DELETE | Restrict to masters |
| ConflictFinding | `SchedulingConflictFinding` | Conflict artifacts | DELETE | → ConflictDetectionRun (Cascade) |
| ConflictDetectionRun | `SchedulingConflictDetectionRun` | Conflict runs | DELETE | Restrict Timetable/AcademicYear |
| OptimizationSnapshot | `SchedulingOptimizationSnapshot` | Sandbox snapshots | DELETE | → Scenario (Cascade) |
| OptimizationScenarioFavorite | `SchedulingOptimizationScenarioFavorite` | Sandbox UX | DELETE | Scenario-linked |
| OptimizationScenarioNote | `SchedulingOptimizationScenarioNote` | Sandbox UX | DELETE | Scenario-linked |
| OptimizationScenarioComment | `SchedulingOptimizationScenarioComment` | Sandbox UX | DELETE | Scenario-linked |
| OptimizationScenarioBookmark | `SchedulingOptimizationScenarioBookmark` | Sandbox UX | DELETE | Scenario-linked |
| OptimizationScenarioApprovalRequest | `SchedulingOptimizationScenarioApprovalRequest` | Sandbox UX | DELETE | Scenario-linked |
| OptimizationScenarioShare | `SchedulingOptimizationScenarioShare` | Sandbox UX | DELETE | Scenario-linked |
| OptimizationScenarioHistory | `SchedulingOptimizationScenarioHistory` | Sandbox history | DELETE | Scenario-linked |
| OptimizationScenario | `SchedulingOptimizationScenario` | Sandbox root | DELETE | Parent of snapshots |
| OptimizationEngineRun | `SchedulingOptimizationEngineRun` | Engine runs | DELETE | Tenant-scoped |
| OptimizationSimulationRun | `SchedulingOptimizationSimulationRun` | Simulations | DELETE | Tenant-scoped |
| OptimizationMetricSnapshot | `SchedulingOptimizationMetricSnapshot` | Metric snapshots | DELETE | Tenant-scoped |
| TimetableApprovalStep | `SchedulingTimetableApprovalStep` | Approval steps | DELETE | → Request (Cascade) |
| TimetableApprovalHistory | `SchedulingTimetableApprovalHistory` | Approval history | DELETE | → Request (Cascade) |
| TimetableApprovalComment | `SchedulingTimetableApprovalComment` | Approval comments | DELETE | → Request (Cascade) |
| TimetableDecisionHistory | `SchedulingTimetableDecisionHistory` | Decisions | DELETE | → Request (Cascade) |
| TimetableApprovalRequest | `SchedulingTimetableApprovalRequest` | Approval header | DELETE | Restrict TT/Version |
| TimetableChangeHistory | `SchedulingTimetableChangeHistory` | Change log | DELETE | → Timetable (Cascade) |
| TimetableWarningDismissal | `SchedulingTimetableWarningDismissal` | Warning UX | DELETE | → Timetable (Cascade) |
| TimetableCloneJob | `SchedulingTimetableCloneJob` | Clone jobs | DELETE | Restrict Timetables |
| TimetableSection | `TimetableSections` | TT↔Section projection | DELETE | Logical TT/Entry/Section |
| TimetableEntry | `SchedulingTimetableEntry` | Designer cells | DELETE | Cascade Timetable; Restrict TG/SA |
| Timetable | `SchedulingTimetable` | Timetable header | DELETE | Restrict Version/AY |
| ScheduleVersion | `SchedulingScheduleVersion` | Version artifact | DELETE | Restrict AY |
| TeachingGroupMembership | `SchedulingTeachingGroupMembership` | TG membership | DELETE | Cascade TG; Restrict Student |
| TeachingGroupSection | `SchedulingTeachingGroupSection` | TG↔Section | DELETE | Cascade TG; Restrict Section |
| TeachingGroup | `SchedulingTeachingGroup` | Teaching cohorts | DELETE | Restrict SA/masters |
| SubjectAllocation | `SchedulingSubjectAllocation` | SA planning rows | DELETE | Restrict masters |
| AllocationEngineSandboxItem | `AllocationEngineSandboxItems` | Alloc sandbox | DELETE | Logical session |
| AllocationEngineDraft | `AllocationEngineDrafts` | Alloc drafts | DELETE | Logical |
| AllocationScenarioVersion | `AllocationScenarioVersions` | Alloc versions | DELETE | Logical |
| AllocationAuditEntry | `AllocationAuditEntries` | Alloc audit | DELETE | Alloc-scoped |
| AllocationEngineScenario | `AllocationEngineScenarios` | Alloc scenarios | DELETE | Logical |
| AllocationEngineSession | `AllocationEngineSessions` | Alloc sessions | DELETE | Tenant-scoped |
| SectionAllocationSnapshot | `SectionAllocationSnapshots` | Snapshots | DELETE | Section ops |
| StudentSection | `StudentSections` | Student↔Section | DELETE | Logical Student/Section |
| FacultySectionAssignment | `FacultySectionAssignments` | Faculty↔Section | DELETE | Logical |
| SectionGroupMember | `SectionGroupMembers` | Section group members | DELETE | SectionGroups |
| SectionLifecycleTransition | `SectionLifecycleTransitions` | Lifecycle audit | DELETE | Sections |
| SectionMergeTransaction | `SectionMergeTransactions` | Merges | DELETE | Sections |
| SectionSplitTransaction | `SectionSplitTransactions` | Splits | DELETE | Sections |
| SectionLineage | `SectionLineages` | Lineage | DELETE | Sections |
| SectionVersion | `SectionVersions` | Versions | DELETE | Sections |
| SectionCapacityHistory | `SectionCapacityHistories` | Capacity history | DELETE | Sections |
| SectionGroup | `SectionGroups` | Section groups | DELETE | Tenant-scoped |
| Section | `Sections` | Academic sections (test) | DELETE | Logical Course/Group/Semester |

---

## PRESERVE denylist

| Entity | Table | Purpose | Classification | Notes |
| --- | --- | --- | --- | --- |
| Student | `Student` | Roster | PRESERVE | Never DELETE/UPDATE |
| Semester | `Semester` | Catalog | PRESERVE | Singular name |
| Group | `Group` | Catalog | PRESERVE | |
| Course | `Course` | Catalog | PRESERVE | |
| Subject | `Subject` | Catalog | PRESERVE | |
| Department | `Department` | Catalog | PRESERVE | |
| Program | `Programs` | Catalog | PRESERVE | |
| College | `College` | Tenant | PRESERVE | |
| University | `University` | Org | PRESERVE | |
| User | `User` | Auth | PRESERVE | |
| Permission | `Permission` | Auth | PRESERVE | Global |
| ApplicationRole | `ApplicationRole` | Auth | PRESERVE | |
| ApplicationRolePermission | `ApplicationRolePermission` | Auth | PRESERVE | |
| UserApplicationRole | `UserApplicationRole` | Auth | PRESERVE | |
| TenantAcademicConfiguration | `TenantAcademicConfigurations` | Config | PRESERVE | |
| ProgramPolicy | `ProgramPolicies` | Config | PRESERVE | |
| TenantSectionCapacityPolicy | `TenantSectionCapacityPolicies` | Config | PRESERVE | |
| ConflictRuleThresholdSetting | `SchedulingConflictRuleThresholdSetting` | Config | PRESERVE | |
| AcademicYear | `SchedulingAcademicYear` | Calendar | PRESERVE | |
| AcademicTerm | `SchedulingAcademicTerm` | Calendar | PRESERVE | |
| Campus/Building/Floor/Room | `SchedulingCampus` etc. | Facility | PRESERVE | |
| TimeSlot* | `SchedulingTimeSlot*` | Slots | PRESERVE | |
| ArchiveReasonLookup | `SchedulingArchiveReason` | Lookup | PRESERVE | |
| Staff* | `StaffMembers` etc. | Faculty master | PRESERVE | |
| AttendanceRecoveryPreference | `AttendanceRecoveryPreference` | Preference | PRESERVE | |
| WorkspacePreference | `SchedulingWorkspacePreference` | Preference | PRESERVE | |
| Faculty* preference/workload/availability | `SchedulingFaculty*` | Preference | PRESERVE | |
| StudentFaceEmbedding | `StudentFaceEmbedding` | Identity | PRESERVE | |
| Face/Enrollment/PhotoAcquisition* | various | Identity pipeline | PRESERVE | |
| AuditEntry | `AuditEntry` | Global audit | PRESERVE | |
| LegacySemesterDispositionJournal | `LegacySemesterDispositionJournals` | Remediation evidence | PRESERVE | |

---

## REVIEW (excluded from DELETE script)

| Entity | Table | Reason |
| --- | --- | --- |
| ConflictWorkspacePin | `SchedulingConflictWorkspacePin` | UI workspace ambiguity |
| ConflictWorkspaceBookmark | `SchedulingConflictWorkspaceBookmark` | UI workspace ambiguity |
| ConflictWorkspaceNote | `SchedulingConflictWorkspaceNote` | UI workspace ambiguity |
| ConflictRuleConfigChangeHistory | `SchedulingConflictRuleConfigChangeHistory` | Config audit |
| OptimizationTelemetryAggregate | `SchedulingOptimizationTelemetryAggregate` | Telemetry / unique keys |
| SectionPolicy | `SectionPolicies` | May be policy SoT |
| SectionAllocationPreference | `SectionAllocationPreferences` | Preference vs ops |
| RoomAllocationRule | `SchedulingRoomAllocationRule` | Facility rule |
| RoomAvailability | `SchedulingRoomAvailability` | Facility availability |

---

## Children-first deletion order (authoritative for reset SQL)

1. AttendanceRecognitionReviewHistory (via Recognition join)  
2. AttendanceDetail → Attendance → Recognition → SessionImage → RetryHistory → BulkHistory → AttendanceSessionSections → AttendanceSession → ClassSchedule  
3. ConflictFinding → ConflictDetectionRun  
4. Optimization children → Scenario → Engine/Simulation/Metric runs  
5. Timetable approval children → ApprovalRequest → ChangeHistory/WarningDismissal/CloneJob → TimetableSections → TimetableEntry → Timetable → ScheduleVersion  
6. TeachingGroupMembership → TeachingGroupSection → TeachingGroup → SubjectAllocation  
7. AllocationEngine* → SectionAllocationSnapshots → StudentSections → FacultySectionAssignments → Section* children → SectionGroups → Sections  

FK checks remain enabled throughout.

using Abhyanvaya.Application.Common.Interfaces;

using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Dashboards;
using Abhyanvaya.Domain.Entities.Scheduling;



namespace Abhyanvaya.Application.Common.Interfaces

{

    public interface IApplicationDbContext : IUnitOfWork

    {

        IQueryable<Student> Students { get; }

        IQueryable<Attendance> Attendances { get; }

        IQueryable<User> Users { get; }

        IQueryable<College> Colleges { get; }

        IQueryable<University> Universities { get; }

        IQueryable<Course> Courses { get; }

        IQueryable<Group> Groups { get; }

        IQueryable<Gender> Genders { get; }

        IQueryable<Medium> Mediums { get; }

        IQueryable<Language> Languages { get; }

        IQueryable<TenantSubject> TenantSubjects { get; }

        IQueryable<Subject> Subjects { get; }

        IQueryable<StudentSubject> StudentSubjects { get; }

        IQueryable<ElectiveGroup> ElectiveGroups { get; }

        IQueryable<Semester> Semesters { get; }

        IQueryable<Department> Departments { get; }

        IQueryable<Staff> StaffMembers { get; }

        IQueryable<StaffDepartment> StaffDepartments { get; }

        IQueryable<StaffDepartmentRole> StaffDepartmentRoles { get; }

        IQueryable<StaffCollegeRole> StaffCollegeRoles { get; }

        IQueryable<StaffSubjectAssignment> StaffSubjectAssignments { get; }

        IQueryable<StaffTypeLookup> StaffTypeLookups { get; }

        IQueryable<PersonTitleLookup> PersonTitleLookups { get; }

        IQueryable<DesignationLookup> DesignationLookups { get; }

        IQueryable<DepartmentRoleLookup> DepartmentRoleLookups { get; }

        IQueryable<CollegeRoleLookup> CollegeRoleLookups { get; }

        IQueryable<QualificationLookup> QualificationLookups { get; }

        IQueryable<EmploymentStatusLookup> EmploymentStatusLookups { get; }

        IQueryable<Permission> Permissions { get; }

        IQueryable<ApplicationRole> ApplicationRoles { get; }

        IQueryable<ApplicationRolePermission> ApplicationRolePermissions { get; }

        IQueryable<UserApplicationRole> UserApplicationRoles { get; }

        IQueryable<AttendanceSession> AttendanceSessions { get; }

        IQueryable<AttendanceSessionImage> AttendanceSessionImages { get; }

        IQueryable<AttendanceRetryHistory> AttendanceRetryHistories { get; }
        IQueryable<AttendanceRecoveryPreference> AttendanceRecoveryPreferences { get; }
        IQueryable<AttendanceBulkOperationHistory> AttendanceBulkOperationHistories { get; }

        IQueryable<AttendanceRecognition> AttendanceRecognitions { get; }

        IQueryable<AttendanceRecognitionReviewHistory> AttendanceRecognitionReviewHistories { get; }

        IQueryable<AttendanceDetail> AttendanceDetails { get; }

        IQueryable<AuditEntry> AuditEntries { get; }

        IQueryable<StudentFaceEmbedding> StudentFaceEmbeddings { get; }

        IQueryable<ClassSchedule> ClassSchedules { get; }

        IQueryable<StudentEnrollmentBatch> StudentEnrollmentBatches { get; }

        IQueryable<StudentEnrollmentItem> StudentEnrollmentItems { get; }

        IQueryable<StudentEnrollmentProgressSnapshot> StudentEnrollmentProgressSnapshots { get; }

        IQueryable<EnrollmentStorageRecord> EnrollmentStorageRecords { get; }

        IQueryable<EnrollmentEmbeddingVersionSnapshot> EnrollmentEmbeddingVersionSnapshots { get; }

        IQueryable<EnrollmentPersistenceAudit> EnrollmentPersistenceAudits { get; }

        IQueryable<EnrollmentWorkLease> EnrollmentWorkLeases { get; }

        IQueryable<EnrollmentDeadLetterEntry> EnrollmentDeadLetterEntries { get; }

        IQueryable<AiModelDefinition> AiModelDefinitions { get; }

        IQueryable<AiModelVersion> AiModelVersions { get; }

        IQueryable<GoldenDatasetDefinition> GoldenDatasetDefinitions { get; }

        IQueryable<ModelRolloutPlan> ModelRolloutPlans { get; }

        IQueryable<ModelLifecycleAuditEntry> ModelLifecycleAuditEntries { get; }

        IQueryable<RetrainingCandidateEntry> RetrainingCandidateEntries { get; }

        IQueryable<StudentPhotoAcquisitionBatch> StudentPhotoAcquisitionBatches { get; }

        IQueryable<StudentPhotoAcquisitionItem> StudentPhotoAcquisitionItems { get; }

        IQueryable<FaceEnrollmentBatch> FaceEnrollmentBatches { get; }

        IQueryable<FaceEnrollmentJob> FaceEnrollmentJobs { get; }

        IQueryable<ArtifactRegistryEntry> ArtifactRegistryEntries { get; }

        IQueryable<ArtifactStorageManifest> ArtifactStorageManifests { get; }

        IQueryable<AcademicYear> SchedulingAcademicYears { get; }
        IQueryable<AcademicTerm> SchedulingAcademicTerms { get; }
        IQueryable<WorkingDay> SchedulingWorkingDays { get; }
        IQueryable<Holiday> SchedulingHolidays { get; }
        IQueryable<Campus> SchedulingCampuses { get; }
        IQueryable<Building> SchedulingBuildings { get; }
        IQueryable<Floor> SchedulingFloors { get; }
        IQueryable<Room> SchedulingRooms { get; }
        IQueryable<TimeSlotSet> SchedulingTimeSlotSets { get; }
        IQueryable<TimeSlot> SchedulingTimeSlots { get; }
        IQueryable<FacultyWorkload> SchedulingFacultyWorkloads { get; }
        IQueryable<FacultyDayPreference> SchedulingFacultyDayPreferences { get; }
        IQueryable<FacultyTimeSlotPreference> SchedulingFacultyTimeSlotPreferences { get; }
        IQueryable<SubjectAllocation> SchedulingSubjectAllocations { get; }
        IQueryable<RoomAllocationRule> SchedulingRoomAllocationRules { get; }
        IQueryable<FacultyAvailability> SchedulingFacultyAvailabilities { get; }
        IQueryable<RoomAvailability> SchedulingRoomAvailabilities { get; }
        IQueryable<SubjectCategory> SchedulingSubjectCategories { get; }
        IQueryable<TimeSlotTemplate> SchedulingTimeSlotTemplates { get; }
        IQueryable<FacultyTeachingPreference> SchedulingFacultyTeachingPreferences { get; }
        IQueryable<RoomFeature> SchedulingRoomFeatures { get; }
        IQueryable<RoomFeatureAssignment> SchedulingRoomFeatureAssignments { get; }
        IQueryable<SubjectDeliveryType> SchedulingSubjectDeliveryTypes { get; }
        IQueryable<HolidayTypeCatalog> SchedulingHolidayTypeCatalogs { get; }
        IQueryable<Timetable> SchedulingTimetables { get; }
        IQueryable<TimetableEntry> SchedulingTimetableEntries { get; }
        IQueryable<ScheduleVersion> SchedulingScheduleVersions { get; }
        IQueryable<TimetableApprovalRequest> SchedulingTimetableApprovalRequests { get; }
        IQueryable<TimetableApprovalStep> SchedulingTimetableApprovalSteps { get; }
        IQueryable<TimetableApprovalHistory> SchedulingTimetableApprovalHistories { get; }
        IQueryable<TimetableCloneJob> SchedulingTimetableCloneJobs { get; }
        IQueryable<TimetableChangeHistory> SchedulingTimetableChangeHistories { get; }
        IQueryable<TimetableWarningDismissal> SchedulingTimetableWarningDismissals { get; }
        IQueryable<TimetableApprovalComment> SchedulingTimetableApprovalComments { get; }
        IQueryable<TimetableDecisionHistory> SchedulingTimetableDecisionHistories { get; }
        IQueryable<ArchiveReasonLookup> SchedulingArchiveReasons { get; }
        IQueryable<ConflictDetectionRun> SchedulingConflictDetectionRuns { get; }
        IQueryable<ConflictFinding> SchedulingConflictFindings { get; }
        IQueryable<ConflictRuleThresholdSetting> SchedulingConflictRuleThresholdSettings { get; }
        IQueryable<ConflictRuleConfigChangeHistory> SchedulingConflictRuleConfigChangeHistories { get; }
        IQueryable<ConflictWorkspacePin> SchedulingConflictWorkspacePins { get; }
        IQueryable<ConflictWorkspaceBookmark> SchedulingConflictWorkspaceBookmarks { get; }
        IQueryable<ConflictWorkspaceNote> SchedulingConflictWorkspaceNotes { get; }
        IQueryable<OptimizationSimulationRun> SchedulingOptimizationSimulationRuns { get; }
        IQueryable<OptimizationMetricSnapshot> SchedulingOptimizationMetricSnapshots { get; }
        IQueryable<OptimizationTelemetryAggregate> SchedulingOptimizationTelemetryAggregates { get; }
        IQueryable<OptimizationScenario> SchedulingOptimizationScenarios { get; }
        IQueryable<OptimizationSnapshot> SchedulingOptimizationSnapshots { get; }
        IQueryable<OptimizationScenarioFavorite> SchedulingOptimizationScenarioFavorites { get; }
        IQueryable<OptimizationScenarioNote> SchedulingOptimizationScenarioNotes { get; }
        IQueryable<OptimizationScenarioComment> SchedulingOptimizationScenarioComments { get; }
        IQueryable<OptimizationScenarioBookmark> SchedulingOptimizationScenarioBookmarks { get; }
        IQueryable<OptimizationScenarioApprovalRequest> SchedulingOptimizationScenarioApprovalRequests { get; }
        IQueryable<OptimizationScenarioShare> SchedulingOptimizationScenarioShares { get; }
        IQueryable<OptimizationScenarioHistory> SchedulingOptimizationScenarioHistories { get; }

        IQueryable<OptimizationEngineRun> SchedulingOptimizationEngineRuns { get; }

        IQueryable<WorkspacePreference> SchedulingWorkspacePreferences { get; }

        IQueryable<DashboardPreference> DashboardPreferences { get; }

        IQueryable<EnterpriseNotificationState> EnterpriseNotificationStates { get; }

        // AI29 — Academic Structure & Section Management
        IQueryable<Section> Sections { get; }
        IQueryable<StudentSection> StudentSections { get; }
        IQueryable<FacultySectionAssignment> FacultySectionAssignments { get; }
        IQueryable<TimetableSection> TimetableSections { get; }
        IQueryable<AttendanceSessionSection> AttendanceSessionSections { get; }
        IQueryable<SectionAllocationPreference> SectionAllocationPreferences { get; }

        // AI29.1B — Section lifecycle, capacity, merge/split, combined groups
        IQueryable<SectionGroup> SectionGroups { get; }
        IQueryable<SectionGroupMember> SectionGroupMembers { get; }
        IQueryable<SectionLifecycleTransition> SectionLifecycleTransitions { get; }
        IQueryable<SectionMergeTransaction> SectionMergeTransactions { get; }
        IQueryable<SectionSplitTransaction> SectionSplitTransactions { get; }
        IQueryable<SectionLineage> SectionLineages { get; }
        IQueryable<TenantSectionCapacityPolicy> TenantSectionCapacityPolicies { get; }

        // AI29.1B.5 — Section operations hardening
        IQueryable<SectionVersion> SectionVersions { get; }
        IQueryable<SectionCapacityHistory> SectionCapacityHistories { get; }
        IQueryable<SectionPolicy> SectionPolicies { get; }

        // AI29.1B.7 — Allocation platform snapshots
        IQueryable<SectionAllocationSnapshot> SectionAllocationSnapshots { get; }

        // AI29.1C — Allocation engine persistence (scenarios / drafts / sandbox)
        IQueryable<AllocationEngineSession> AllocationEngineSessions { get; }
        IQueryable<AllocationEngineScenario> AllocationEngineScenarios { get; }
        IQueryable<AllocationEngineDraft> AllocationEngineDrafts { get; }
        IQueryable<AllocationEngineSandboxItem> AllocationEngineSandboxItems { get; }

        // AI29.1C.5 — Allocation operations
        IQueryable<AllocationScenarioVersion> AllocationScenarioVersions { get; }
        IQueryable<AllocationAuditEntry> AllocationAuditEntries { get; }

        // AI29.1A — Program & academic hierarchy configuration
        IQueryable<Program> Programs { get; }
        IQueryable<TenantAcademicConfiguration> TenantAcademicConfigurations { get; }

        // AI29.1A.5 — Program policies (configuration only)
        IQueryable<ProgramPolicy> ProgramPolicies { get; }

        // AI29.1A.6 — Hierarchy snapshots (feature-flagged)
        IQueryable<AcademicHierarchySnapshot> AcademicHierarchySnapshots { get; }

        // AI29.1A.7 — Architecture trend history (observability)
        IQueryable<AcademicArchitectureTrend> AcademicArchitectureTrends { get; }

        Task AddAsync<T>(T entity) where T : class;

        void Remove<T>(T entity) where T : class;

        void AddAttendances(IEnumerable<Attendance> attendances);

        Task AddRangeAsync<T>(IEnumerable<T> entities) where T : class;

    }

}



namespace Abhyanvaya.Domain.Authorization
{
    /// <summary>Stable permission keys stored in <see cref="Entities.Permission"/> and JWT claims.</summary>
    public static class PermissionKeys
    {
        public const string StudentsView = "Students.View";
        public const string StudentsManage = "Students.Manage";
        public const string AttendanceView = "Attendance.View";
        public const string AttendanceManage = "Attendance.Manage";
        public const string ReportsView = "Reports.View";
        public const string SetupSubjectsManage = "Setup.Subjects.Manage";
        public const string SetupDepartmentsManage = "Setup.Departments.Manage";
        public const string SetupStaffManage = "Setup.Staff.Manage";
        public const string DashboardView = "Dashboard.View";
        public const string OrganizationManage = "Organization.Manage";
        public const string MasterView = "Master.View";
        public const string SetupLookupsManage = "Setup.Lookups.Manage";
        public const string SetupCoursesManage = "Setup.Courses.Manage";
        public const string SetupGroupsManage = "Setup.Groups.Manage";
        public const string SetupSemestersManage = "Setup.Semesters.Manage";
        public const string EnrollmentView = "Enrollment.View";
        public const string EnrollmentManage = "Enrollment.Manage";
        public const string SchedulingView = "Scheduling.View";
        public const string SchedulingManage = "Scheduling.Manage";
        /// <summary>Retired by AI30 AC1 — Catalog owns Department. Kept for seed/DB compatibility only.</summary>
        [Obsolete("AI30 AC1: use Setup.Departments.Manage / Catalog Department API")]
        public const string SchedulingDepartmentView = "Scheduling.Department.View";
        /// <summary>Retired by AI30 AC1 — Catalog owns Department. Kept for seed/DB compatibility only.</summary>
        [Obsolete("AI30 AC1: use Setup.Departments.Manage / Catalog Department API")]
        public const string SchedulingDepartmentManage = "Scheduling.Department.Manage";
        public const string SchedulingRoomAvailabilityView = "Scheduling.RoomAvailability.View";
        public const string SchedulingRoomAvailabilityManage = "Scheduling.RoomAvailability.Manage";
        public const string SchedulingFacultyAvailabilityView = "Scheduling.FacultyAvailability.View";
        public const string SchedulingFacultyAvailabilityManage = "Scheduling.FacultyAvailability.Manage";
        public const string SchedulingTemplateView = "Scheduling.Template.View";
        public const string SchedulingTemplateManage = "Scheduling.Template.Manage";
        public const string SchedulingFacultyPreferencesView = "Scheduling.FacultyPreferences.View";
        public const string SchedulingFacultyPreferencesManage = "Scheduling.FacultyPreferences.Manage";
        public const string SchedulingRoomFeaturesView = "Scheduling.RoomFeatures.View";
        public const string SchedulingRoomFeaturesManage = "Scheduling.RoomFeatures.Manage";
        public const string SchedulingSubjectDeliveryView = "Scheduling.SubjectDelivery.View";
        public const string SchedulingSubjectDeliveryManage = "Scheduling.SubjectDelivery.Manage";
        public const string SchedulingHolidayTypesView = "Scheduling.HolidayTypes.View";
        public const string SchedulingHolidayTypesManage = "Scheduling.HolidayTypes.Manage";
        public const string SchedulingTimetableView = "Scheduling.Timetable.View";
        public const string SchedulingTimetableManage = "Scheduling.Timetable.Manage";
        public const string SchedulingVersionView = "Scheduling.Version.View";
        public const string SchedulingVersionManage = "Scheduling.Version.Manage";
        public const string SchedulingReview = "Scheduling.Review";
        public const string SchedulingApprove = "Scheduling.Approve";
        public const string SchedulingPublish = "Scheduling.Publish";
        public const string SchedulingArchive = "Scheduling.Archive";
        public const string SchedulingClone = "Scheduling.Clone";
        public const string SchedulingHistoryView = "Scheduling.History.View";
        public const string SchedulingVersionCompareView = "Scheduling.VersionCompare.View";
        public const string SchedulingVersionCompareExport = "Scheduling.VersionCompare.Export";
        public const string SchedulingApprovalCommentsView = "Scheduling.ApprovalComments.View";
        public const string SchedulingApprovalCommentsManage = "Scheduling.ApprovalComments.Manage";
        public const string SchedulingFreeze = "Scheduling.Freeze";
        public const string SchedulingUnlock = "Scheduling.Unlock";
        public const string SchedulingArchiveView = "Scheduling.Archive.View";
        public const string SchedulingArchiveManage = "Scheduling.Archive.Manage";
        public const string SchedulingConflictView = "Scheduling.Conflict.View";
        public const string SchedulingConflictManage = "Scheduling.Conflict.Manage";

        // AI29 — Section management
        public const string SectionView = "Section.View";
        public const string SectionCreate = "Section.Create";
        public const string SectionEdit = "Section.Edit";
        public const string SectionDelete = "Section.Delete";
        public const string SectionAssignStudents = "Section.AssignStudents";
        public const string SectionAssignFaculty = "Section.AssignFaculty";

        // AI29.1B — Section lifecycle & capacity operations
        public const string SectionLifecycleView = "SectionLifecycle.View";
        public const string SectionLifecycleEdit = "SectionLifecycle.Edit";
        public const string SectionMerge = "Section.Merge";
        public const string SectionSplit = "Section.Split";
        public const string SectionCapacity = "Section.Capacity";
        public const string SectionReadiness = "Section.Readiness";

        // AI29.1C — Allocation engine (scenario / draft only)
        public const string AllocationRun = "Allocation.Run";
        public const string AllocationApprove = "Allocation.Approve";

        // AI29.1C.5 — Allocation operations
        public const string AllocationOperationsView = "Allocation.Operations.View";
        public const string AllocationScenarioView = "Allocation.Scenario.View";
        public const string AllocationScenarioCreate = "Allocation.Scenario.Create";
        public const string AllocationScenarioCompare = "Allocation.Scenario.Compare";
        public const string AllocationScenarioReplay = "Allocation.Scenario.Replay";
        public const string AllocationScenarioReview = "Allocation.Scenario.Review";
        public const string AllocationScenarioArchive = "Allocation.Scenario.Archive";
        public const string AllocationReject = "Allocation.Reject";
        public const string AllocationExport = "Allocation.Export";

        // AI29.1A — Program management
        public const string ProgramView = "Program.View";
        public const string ProgramCreate = "Program.Create";
        public const string ProgramEdit = "Program.Edit";
        public const string ProgramDelete = "Program.Delete";
        public const string ProgramManage = "Program.Manage";

        public static IReadOnlyList<string> All { get; } =
        [
            StudentsView,
            StudentsManage,
            AttendanceView,
            AttendanceManage,
            ReportsView,
            SetupSubjectsManage,
            SetupDepartmentsManage,
            SetupStaffManage,
            DashboardView,
            OrganizationManage,
            MasterView,
            SetupLookupsManage,
            SetupCoursesManage,
            SetupGroupsManage,
            SetupSemestersManage,
            EnrollmentView,
            EnrollmentManage,
            SchedulingView,
            SchedulingManage,
            SchedulingRoomAvailabilityView,
            SchedulingRoomAvailabilityManage,
            SchedulingFacultyAvailabilityView,
            SchedulingFacultyAvailabilityManage,
            SchedulingTemplateView,
            SchedulingTemplateManage,
            SchedulingFacultyPreferencesView,
            SchedulingFacultyPreferencesManage,
            SchedulingRoomFeaturesView,
            SchedulingRoomFeaturesManage,
            SchedulingSubjectDeliveryView,
            SchedulingSubjectDeliveryManage,
            SchedulingHolidayTypesView,
            SchedulingHolidayTypesManage,
            SchedulingTimetableView,
            SchedulingTimetableManage,
            SchedulingVersionView,
            SchedulingVersionManage,
            SchedulingReview,
            SchedulingApprove,
            SchedulingPublish,
            SchedulingArchive,
            SchedulingClone,
            SchedulingHistoryView,
            SchedulingVersionCompareView,
            SchedulingVersionCompareExport,
            SchedulingApprovalCommentsView,
            SchedulingApprovalCommentsManage,
            SchedulingFreeze,
            SchedulingUnlock,
            SchedulingArchiveView,
            SchedulingArchiveManage,
            SchedulingConflictView,
            SchedulingConflictManage,
            SectionView,
            SectionCreate,
            SectionEdit,
            SectionDelete,
            SectionAssignStudents,
            SectionAssignFaculty,
            SectionLifecycleView,
            SectionLifecycleEdit,
            SectionMerge,
            SectionSplit,
            SectionCapacity,
            SectionReadiness,
            AllocationRun,
            AllocationApprove,
            AllocationOperationsView,
            AllocationScenarioView,
            AllocationScenarioCreate,
            AllocationScenarioCompare,
            AllocationScenarioReplay,
            AllocationScenarioReview,
            AllocationScenarioArchive,
            AllocationReject,
            AllocationExport,
            ProgramView,
            ProgramCreate,
            ProgramEdit,
            ProgramDelete,
            ProgramManage,
        ];

        /// <summary>Fallback when <see cref="Entities.UserApplicationRole"/> rows are absent (legacy enum roles).</summary>
        public static IReadOnlyList<string> LegacyFacultySet { get; } =
        [
            StudentsView,
            AttendanceView,
            AttendanceManage,
            ReportsView,
            DashboardView,
            MasterView
        ];
    }
}

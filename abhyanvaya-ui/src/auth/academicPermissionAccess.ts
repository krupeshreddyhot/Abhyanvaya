import { PermissionKeys } from "./permissionKeys";

/**
 * AI29.1D Prompt 18 — catalog of existing server permission keys for academic UI capabilities.
 * UI uses these only to enable/disable controls and show denied states.
 * Server policies remain authoritative; never invent alternate client authorization rules.
 */
export const AcademicPermissionAccess = {
  programs: {
    view: PermissionKeys.ProgramView,
    create: PermissionKeys.ProgramCreate,
    edit: PermissionKeys.ProgramEdit,
    delete: PermissionKeys.ProgramDelete,
    manage: PermissionKeys.ProgramManage,
    /** Route / hierarchy / breadcrumb APIs under academic-structure v1. */
    structureAny: [
      PermissionKeys.ProgramView,
      PermissionKeys.ProgramCreate,
      PermissionKeys.ProgramEdit,
      PermissionKeys.ProgramManage,
    ],
  },
  sections: {
    view: PermissionKeys.SectionView,
    create: PermissionKeys.SectionCreate,
    edit: PermissionKeys.SectionEdit,
    delete: PermissionKeys.SectionDelete,
    assignStudents: PermissionKeys.SectionAssignStudents,
    assignFaculty: PermissionKeys.SectionAssignFaculty,
    routeAny: [
      PermissionKeys.SectionView,
      PermissionKeys.SectionCreate,
      PermissionKeys.SectionEdit,
      PermissionKeys.SectionDelete,
      PermissionKeys.SectionAssignStudents,
      PermissionKeys.SectionAssignFaculty,
      PermissionKeys.SectionLifecycleView,
      PermissionKeys.SectionLifecycleEdit,
      PermissionKeys.SectionCapacity,
      PermissionKeys.SectionMerge,
      PermissionKeys.SectionSplit,
      PermissionKeys.SectionReadiness,
      PermissionKeys.AllocationOperationsView,
    ],
  },
  sectionLifecycle: {
    view: PermissionKeys.SectionLifecycleView,
    edit: PermissionKeys.SectionLifecycleEdit,
  },
  sectionCapacity: {
    manage: PermissionKeys.SectionCapacity,
  },
  sectionMergeSplit: {
    merge: PermissionKeys.SectionMerge,
    split: PermissionKeys.SectionSplit,
  },
  facultyAllocation: {
    assign: PermissionKeys.SectionAssignFaculty,
    viewSections: PermissionKeys.SectionView,
  },
  allocation: {
    run: PermissionKeys.AllocationRun,
    approve: PermissionKeys.AllocationApprove,
    reject: PermissionKeys.AllocationReject,
    export: PermissionKeys.AllocationExport,
    operationsView: PermissionKeys.AllocationOperationsView,
    contextAny: [
      PermissionKeys.SectionView,
      PermissionKeys.AllocationOperationsView,
      PermissionKeys.AllocationRun,
      PermissionKeys.AllocationScenarioView,
    ],
  },
  allocationScenario: {
    view: PermissionKeys.AllocationScenarioView,
    create: PermissionKeys.AllocationScenarioCreate,
    compare: PermissionKeys.AllocationScenarioCompare,
    replay: PermissionKeys.AllocationScenarioReplay,
    review: PermissionKeys.AllocationScenarioReview,
    archive: PermissionKeys.AllocationScenarioArchive,
  },
  attendance: {
    view: PermissionKeys.AttendanceView,
    manage: PermissionKeys.AttendanceManage,
  },
  /**
   * AI29.1D Prompt 16A — GET breadcrumb/context (server policy CanViewAcademicOperationalContext).
   * OR of consumer permissions; Program.View allowed but not required. No Program write keys.
   */
  operationalContext: {
    viewAny: [
      PermissionKeys.AttendanceView,
      PermissionKeys.AttendanceManage,
      PermissionKeys.SectionView,
      PermissionKeys.SectionAssignFaculty,
      PermissionKeys.SectionLifecycleView,
      PermissionKeys.SchedulingTimetableView,
      PermissionKeys.SchedulingTimetableManage,
      PermissionKeys.SchedulingView,
      PermissionKeys.SchedulingManage,
      PermissionKeys.AllocationRun,
      PermissionKeys.AllocationOperationsView,
      PermissionKeys.AllocationScenarioView,
      PermissionKeys.ProgramView,
    ],
  },
} as const;

export const missingPermissionTooltip = (permissionKey: string): string =>
  `Requires permission ${permissionKey}. The server still enforces authorization on every request.`;

export const permissionDeniedCopy = (permissionKey: string): string =>
  `${permissionKey} permission required. Controls stay disabled until an administrator grants access; API calls remain server-authorized.`;

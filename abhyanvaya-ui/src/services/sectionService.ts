import api from "../api/axios";

export type SectionDto = {
  id: number;
  collegeId: number;
  academicYearId: number;
  academicYearName?: string | null;
  courseId: number;
  courseName?: string | null;
  groupId: number;
  groupName?: string | null;
  semesterId: number;
  semesterName?: string | null;
  sectionCode: string;
  sectionName: string;
  displayOrder: number;
  maximumStrength: number;
  status: string;
  currentStrength: number;
  remainingCapacity: number;
  sectionTypeCode?: string;
  minimumCapacity?: number;
  recommendedCapacity?: number;
  reservedSeats?: number;
  waitingListCount?: number;
  occupancyPercent?: number | null;
  capacityStatus?: string | null;
  parentSectionId?: number | null;
  sectionGroupId?: number | null;
};

export type SectionCapacitySnapshotDto = {
  sectionId: number;
  sectionCode: string;
  sectionName: string;
  lifecycleStatus: string;
  sectionTypeCode?: string;
  maximumCapacity: number;
  minimumCapacity: number;
  recommendedCapacity: number;
  currentStrength: number;
  reservedSeats: number;
  waitingList: number;
  availableSeats: number;
  occupancyPercent: number;
  capacityStatus: string;
  isOverCapacity: boolean;
  isUnderCapacity: boolean;
  isHardLimitBreached: boolean;
  hasWarning: boolean;
  warnings: string[];
};

/** Tenant capacity policy from Section Capacity Engine. */
export type TenantSectionCapacityPolicyDto = {
  id: number;
  enforceHardLimit: boolean;
  softLimitEnabled: boolean;
  warningPercent: number;
  autoWarningEnabled: boolean;
  underCapacityPercent: number;
};

export type SectionReadinessDto = {
  sectionId: number;
  sectionCode: string;
  sectionName: string;
  overallStatus: string;
  checks: { area: string; status: string; message: string }[];
};

export type SectionMergePreviewDto = {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  combinedStudentCount: number;
  combinedFacultyCount: number;
  targetMaximumCapacity: number;
  sourceSectionIds: number[];
  targetSectionId?: number | null;
};

export type SectionSplitPreviewDto = {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  sourceSectionId: number;
  sourceStudentCount: number;
  strategyCode: string;
  proposedChildren: { proposedCode: string; proposedName: string; proposedCapacity: number; plannedStudentCount: number }[];
};

export type StudentSectionDto = {
  id: number;
  studentId: number;
  studentNumber?: string | null;
  studentName?: string | null;
  sectionId: number;
  sectionCode?: string | null;
  sectionName?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isCurrent: boolean;
  transferReason?: string | null;
};

export type FacultySectionDto = {
  id: number;
  facultyId: number;
  facultyName?: string | null;
  sectionId: number;
  sectionCode?: string | null;
  sectionName?: string | null;
  academicYearId: number;
  role: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isCurrent: boolean;
};

/** Existing SectionGroup API — combined-class membership (not a faculty-section model). */
export type SectionGroupDto = {
  id: number;
  collegeId: number;
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  groupCode: string;
  groupName: string;
  status: string;
  notes?: string | null;
  currentSectionIds: number[];
};

export type TimetableSectionDto = {
  id: number;
  timetableId: number;
  timetableEntryId?: number | null;
  sectionId: number;
  sectionCode?: string | null;
  sectionName?: string | null;
};

export type SectionStatisticsDto = {
  sectionId: number;
  sectionCode: string;
  sectionName: string;
  maximumStrength: number;
  studentCount: number;
  facultyCount: number;
  remainingCapacity: number;
  utilizationPercent: number;
};

export type SectionVersionDto = {
  id: number;
  sectionId: number;
  versionNumber: number;
  versionDate: string;
  changedBy?: number | null;
  reason?: string | null;
  operation: string;
  previousVersionId?: number | null;
  sectionCode: string;
  sectionName: string;
  status: string;
  sectionTypeCode: string;
  maximumCapacity: number;
  currentStrength: number;
};

export type SectionLineageDto = {
  parentSectionId: number;
  childSectionId: number;
  relationKind: string;
  transactionId?: string | null;
  effectiveDate: string;
};

export type SectionMergeHistoryDto = {
  transactionId: string;
  targetSectionId: number;
  sourceSectionIds: number[];
  status: string;
  isReversed: boolean;
  effectiveDate?: string;
  notes?: string | null;
};

export type SectionSplitHistoryDto = {
  transactionId: string;
  sourceSectionId: number;
  childSectionIds: number[];
  strategyCode: string;
  status: string;
  isReversed: boolean;
  effectiveDate?: string;
};

const base = "/sections";

export const listSections = (
  params?: {
    academicYearId?: number;
    courseId?: number;
    groupId?: number;
    semesterId?: number;
  },
  config?: { signal?: AbortSignal },
) => api.get<SectionDto[]>(base, { params, signal: config?.signal });

export const getSection = (id: number) => api.get<SectionDto>(`${base}/${id}`);

export const createSection = (body: {
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  sectionCode: string;
  sectionName: string;
  displayOrder?: number;
  maximumStrength?: number;
  status?: string;
  sectionTypeCode?: string;
}) => api.post<SectionDto>(base, body);

export const updateSection = (
  id: number,
  body: {
    sectionCode: string;
    sectionName: string;
    displayOrder: number;
    maximumStrength: number;
    status: string;
    sectionTypeCode?: string;
  },
) => api.put<SectionDto>(`${base}/${id}`, body);

export const deleteSection = (id: number) => api.delete(`${base}/${id}`);

export const listStudentSections = (params?: { sectionId?: number; studentId?: number; currentOnly?: boolean }) =>
  api.get<StudentSectionDto[]>("/student-sections", { params });

export const assignStudentSection = (body: { studentId: number; sectionId: number; effectiveFrom?: string }) =>
  api.post<StudentSectionDto>("/student-sections", body);

export const transferStudentSection = (body: {
  studentId: number;
  targetSectionId: number;
  effectiveFrom?: string;
  reason?: string;
}) => api.post<StudentSectionDto>("/student-sections/transfer", body);

export const listFacultySections = (
  params?: { sectionId?: number; facultyId?: number; currentOnly?: boolean },
  config?: { signal?: AbortSignal },
) => api.get<FacultySectionDto[]>("/faculty-sections", { params, signal: config?.signal });

export const assignFacultySection = (body: {
  facultyId: number;
  sectionId: number;
  academicYearId: number;
  role?: string;
  effectiveFrom?: string;
}) => api.post<FacultySectionDto>("/faculty-sections", body);

export const listSectionGroups = (params?: { academicYearId?: number; semesterId?: number }) =>
  api.get<SectionGroupDto[]>("/section-groups", { params });

export const listTimetableSections = (timetableId: number) =>
  api.get<TimetableSectionDto[]>(`/timetable/${timetableId}/sections`);

export const setTimetableSections = (
  timetableId: number,
  body: { timetableEntryId?: number | null; sectionIds: number[] },
) => api.put<TimetableSectionDto[]>(`/timetable/${timetableId}/sections`, body);

export const autoAllocateSections = (body: {
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  strategy?: string;
}) => api.post<{ assignedCount: number; skippedCount: number; strategy: string; messages: string[] }>(`${base}/auto-allocate`, body);

export const getSectionStatistics = (params?: { academicYearId?: number; semesterId?: number }) =>
  api.get<SectionStatisticsDto[]>(`${base}/statistics`, { params });

// AI29.1B — Lifecycle / capacity / merge / split / readiness
export const listLifecycleStates = () => api.get<string[]>(`${base}/lifecycle/states`);
export const transitionSectionLifecycle = (sectionId: number, body: { targetStatus: string; reason?: string }) =>
  api.post<SectionDto>(`${base}/lifecycle/${sectionId}/transition`, body);
export const getLifecycleHistory = (sectionId: number) =>
  api.get<{ id: number; fromStatus: string; toStatus: string; reason?: string; transitionedUtc: string }[]>(
    `${base}/lifecycle/${sectionId}/history`,
  );

export const getCapacitySummary = (params?: { academicYearId?: number; semesterId?: number }) =>
  api.get<{
    sectionCount: number;
    totalMaximumCapacity: number;
    totalCurrentStrength: number;
    totalAvailableSeats: number;
    overCapacityCount: number;
    underCapacityCount: number;
    warningCount: number;
    averageOccupancyPercent: number;
  }>(`${base}/capacity/summary`, { params });

/** Capacity occupancy — pass sectionIds from Allocation Context to avoid year/semester-wide catalogs. */
export const getSectionOccupancy = (params?: {
  academicYearId?: number;
  semesterId?: number;
  /** Authoritative eligible section ids (Allocation Context). */
  sectionIds?: number[];
}) => {
  const query: Record<string, number | number[]> = {};
  if (params?.academicYearId != null) query.academicYearId = params.academicYearId;
  if (params?.semesterId != null) query.semesterId = params.semesterId;
  if (params?.sectionIds?.length) query.sectionIds = params.sectionIds;
  return api.get<SectionCapacitySnapshotDto[]>(`${base}/capacity/occupancy`, {
    params: query,
    paramsSerializer: {
      // ASP.NET Core binds int[] from repeated query keys.
      indexes: null,
    },
  });
};

/** Capacity policy (hard/soft limit, warning threshold, under-capacity) — capacity engine authoritative. */
export const getCapacityPolicy = () =>
  api.get<TenantSectionCapacityPolicyDto>(`${base}/capacity/policy`);

/** Capacity health snapshot (capacity engine). */
export const getSectionCapacityHealth = () => api.get<SectionReadinessDto[]>(`${base}/capacity/health`);
/** @deprecated Prefer listSectionReadiness — kept for existing callers. */
export const getSectionHealth = () => listSectionReadiness();
export const getSectionReadiness = (sectionId: number) => api.get<SectionReadinessDto>(`${base}/readiness/${sectionId}`);
export const listSectionReadiness = (params?: { academicYearId?: number; semesterId?: number }) =>
  api.get<SectionReadinessDto[]>(`${base}/readiness`, { params });

export const getSectionVersions = (sectionId: number) =>
  api.get<SectionVersionDto[]>(`${base}/ops/${sectionId}/versions`);
export const getSectionLineage = (sectionId: number) =>
  api.get<SectionLineageDto[]>(`${base}/${sectionId}/lineage`);

export const previewMerge = (body: { sourceSectionIds: number[]; targetSectionId: number }) =>
  api.post<SectionMergePreviewDto>(`${base}/merge/preview`, body);
export const commitMerge = (body: { sourceSectionIds: number[]; targetSectionId: number; effectiveDate: string; notes?: string }) =>
  api.post<{ transactionId: string; status: string }>(`${base}/merge/commit`, body);
export const getMergeHistory = () => api.get<SectionMergeHistoryDto[]>(`${base}/merge/history`);

export const previewSplit = (body: { sourceSectionId: number; childCount?: number; strategyCode?: string }) =>
  api.post<SectionSplitPreviewDto>(`${base}/split/preview`, body);
export const commitSplit = (body: {
  sourceSectionId: number;
  strategyCode?: string;
  effectiveDate: string;
  children?: { proposedCode: string; proposedName: string; proposedCapacity: number; plannedStudentCount: number }[];
}) => api.post<{ transactionId: string; status: string }>(`${base}/split/commit`, body);
export const getSplitHistory = () => api.get<SectionSplitHistoryDto[]>(`${base}/split/history`);

export const exportSectionReport = (kind: string, format: string) =>
  api.get<Blob>(`${base}/reports/export`, { params: { kind, format }, responseType: "blob" });

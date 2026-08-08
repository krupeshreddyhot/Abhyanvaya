import api from "../api/axios";

export type AllocationScope = {
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
};

export type SectionAllocationContext = {
  contextId: string;
  contextVersion: string;
  schemaVersion: string;
  generatedAt: string;
  checksum: string;
  hierarchy: {
    academicYearId: number;
    academicYearName?: string | null;
    programId?: number | null;
    programName?: string | null;
    courseId: number;
    courseName?: string | null;
    groupId: number;
    groupName?: string | null;
    semesterId: number;
    semesterName?: string | null;
  };
  sections: { sectionId: number; sectionCode: string; sectionName: string; sectionType: string; lifecycle: string; health: string; readiness: string }[];
  capacities: { sectionId: number; maximumCapacity: number; currentStrength: number; availableCapacity: number; occupancyPercent: number; capacityStatus: string }[];
  students: { studentId: number; studentNumber?: string | null; studentName?: string | null; currentSectionId?: number | null; currentSectionCode?: string | null }[];
  facultyAssignments: { facultyId: number; facultyName?: string | null; sectionId: number; role: string }[];
  subjectAssignments: { subjectId: number; subjectCode?: string | null; subjectName?: string | null }[];
  roomAvailability: { roomCode?: string | null; timetableMappingCount: number; status: string }[];
  policies: string[];
  recommendations: string[];
  overallHealth: string;
  overallReadiness: string;
  timetableStatus: string;
};

export type AllocationReadinessReport = {
  overallStatus: string;
  checks: { area: string; status: string; message: string }[];
};

export type AllocationHealthReport = {
  overallStatus: string;
  dimensions: { area: string; status: string; message: string }[];
};

export type AllocationValidationReport = {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  checks: string[];
};

export type AllocationSnapshotDto = {
  snapshotId: string;
  contextVersion: string;
  schemaVersion: string;
  checksum: string;
  generatedDate: string;
};

const params = (scope: AllocationScope) => ({
  academicYearId: scope.academicYearId,
  courseId: scope.courseId,
  groupId: scope.groupId,
  semesterId: scope.semesterId,
});

export const getAllocationContext = (scope: AllocationScope, refresh = false) =>
  api.get<SectionAllocationContext>("/allocation/context", { params: { ...params(scope), refresh } });

export const getAllocationReadiness = (scope: AllocationScope) =>
  api.get<AllocationReadinessReport>("/allocation/readiness", { params: params(scope) });

export const getAllocationHealth = (scope: AllocationScope) =>
  api.get<AllocationHealthReport>("/allocation/health", { params: params(scope) });

export const getAllocationValidation = (scope: AllocationScope) =>
  api.get<AllocationValidationReport>("/allocation/validation", { params: params(scope) });

export const createAllocationSnapshot = (scope: AllocationScope) =>
  api.get<AllocationSnapshotDto>("/allocation/snapshot", { params: { ...params(scope), create: true } });

export const listAllocationSnapshots = (scope: AllocationScope) =>
  api.get<AllocationSnapshotDto[]>("/allocation/snapshot", { params: params(scope) });

export const getAllocationArchitectureReport = () =>
  api.get<{ passed: boolean; checks: string[]; violations: string[] }>("/allocation/architecture-report");

export type AllocationRunRequest = AllocationScope & {
  groupingMode?: string;
  enabledStrategies?: Record<string, boolean>;
};

export type AllocationStudentRecommendation = {
  studentId: number;
  studentNumber?: string | null;
  studentName?: string | null;
  fromSectionId?: number | null;
  fromSectionCode?: string | null;
  toSectionId: number;
  toSectionCode: string;
  explanations: string[];
};

export type AllocationTraceStep = {
  order: number;
  strategyCode: string;
  enabled: boolean;
  executed: boolean;
  durationMs: number;
  scoreAfter: number;
  summary: string;
  constraintNotes: string[];
};

export type AllocationScoreBreakdown = {
  totalScore: number;
  capacityUtilization: number;
  policyCompliance: number;
  genderBalance: number;
  summary: string;
};

export type AllocationExecutionResult = {
  sessionId: string;
  scenarioId: string;
  succeeded: boolean;
  status: string;
  score: AllocationScoreBreakdown;
  warnings: string[];
  errors: string[];
  durationMs: number;
  scenario: {
    scenarioId: string;
    recommendations: AllocationStudentRecommendation[];
    sectionSummaries: { sectionId: number; sectionCode: string; assignedCount: number; maximumCapacity: number; occupancyPercent: number }[];
    constraints: { constraintCode: string; priority: string; satisfied: boolean; summary: string }[];
    score: AllocationScoreBreakdown;
  };
  trace: { traceId: string; steps: AllocationTraceStep[] };
};

export type AllocationComparisonReport = {
  scenarioId: string;
  originalAverageOccupancy: number;
  allocatedAverageOccupancy: number;
  capacityImprovement: number;
  genderBalanceScore: number;
  policyComplianceScore: number;
  summary: string;
  constraintViolations: { constraintCode: string; summary: string }[];
};

export type AllocationDraft = {
  draftId: string;
  scenarioId: string;
  status: string;
  note: string;
};

export type AllocationDashboardDto = {
  totalRuns: number;
  bestScore: number;
  averageCapacityUtilization: number;
  averageConstraintCompliance: number;
  recentRuns: { sessionId: string; scenarioId?: string; createdAt: string; status: string; score: number; groupingMode: string }[];
};

export const runAllocation = (payload: AllocationRunRequest) =>
  api.post<AllocationExecutionResult>("/allocation/run", payload);

export const simulateAllocation = (payload: AllocationRunRequest) =>
  api.post<AllocationExecutionResult>("/allocation/simulate", payload);

export const compareAllocation = (scenarioId: string) =>
  api.get<AllocationComparisonReport>("/allocation/compare", { params: { scenarioId } });

export const approveAllocation = (scenarioId: string) =>
  api.post<AllocationDraft>("/allocation/approve", null, { params: { scenarioId } });

export const getAllocationHistory = () => api.get<{ sessionId: string; scenarioId?: string; createdAt: string; status: string; score: number; groupingMode: string }[]>("/allocation/history");

export const getAllocationDashboard = () => api.get<AllocationDashboardDto>("/allocation/dashboard");

export const exportAllocationReport = (kind: string, format: string, scenarioId?: string) =>
  api.get<Blob>("/allocation/reports/export", {
    params: { kind, format, scenarioId },
    responseType: "blob",
  });

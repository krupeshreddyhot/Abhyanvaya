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
  capacities: {
    sectionId: number;
    maximumCapacity: number;
    minimumCapacity?: number;
    recommendedCapacity?: number;
    currentStrength: number;
    availableCapacity: number;
    reservedSeats?: number;
    waitingList?: number;
    occupancyPercent: number;
    capacityStatus: string;
  }[];
  students: {
    studentId: number;
    studentNumber?: string | null;
    studentName?: string | null;
    currentSectionId?: number | null;
    currentSectionCode?: string | null;
    /** AI29.1D population filter facets (from Allocation Context projection). */
    genderId?: number | null;
    gender?: string | null;
    languageId?: number | null;
    language?: string | null;
    scholarshipCategory?: string | null;
    minorSubject?: string | null;
    transportRoute?: string | null;
    hostel?: string | null;
    electiveCombination?: string | null;
    merit?: string | null;
  }[];
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

export type AllocationConstraintPriority = "Mandatory" | "Preferred" | "Informational";

/** AI29.1D Prompt 10A — population selection criteria (resolved server-side against Allocation Context). */
export type AllocationPopulationSelection = {
  mode: string;
  fromStudentNumber?: string | null;
  toStudentNumber?: string | null;
  studentIds?: number[] | null;
  facetValue?: string | null;
};

export type AllocationRunRequest = AllocationScope & {
  groupingMode?: string;
  enabledStrategies?: Record<string, boolean>;
  /** AI29.1C constraint priorities by constraint code. */
  constraintPriorities?: Record<string, AllocationConstraintPriority | string>;
  populationSelection?: AllocationPopulationSelection | null;
  targetSectionIds?: number[] | null;
  /** AI29.1D.24B.4 — optional band size for RollNumberBands (null = first section capacity). */
  rollNumberBandSize?: number | null;
  /** AI29.1D.24B.4A — PreserveExisting | Reallocate (omit = legacy server default). */
  existingAssignmentPolicy?: string | null;
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
  meritDistribution?: number;
  languageDistribution?: number;
  hostelDistribution?: number;
  electiveBalance?: number;
  transportBalance?: number;
  summary: string;
};

export type AllocationSandboxItem = {
  sandboxId: string;
  name: string;
  scenarioId: string;
  sessionId?: string;
  savedAt?: string;
  isArchived?: boolean;
  tags?: string | null;
};

/**
 * AI29.1D.24B.4A.2 — Matches C# AllocationExecutionResult.
 * Student placement recommendations are ONLY under scenario.recommendations
 * (not at result.recommendations). Constraint.priority may be a numeric enum (0/1/2)
 * from System.Text.Json without JsonStringEnumConverter.
 */
export type AllocationConstraintEvaluationDto = {
  constraintCode: string;
  /** String name or numeric enum (Mandatory=0, Preferred=1, Informational=2). */
  priority: string | number;
  satisfied: boolean;
  summary: string;
  scoreImpact?: number;
};

export type AllocationScenarioDto = {
  scenarioId: string;
  sessionId?: string;
  contextId?: string;
  contextChecksum?: string;
  generatedAt?: string;
  status?: string;
  recommendations?: AllocationStudentRecommendation[] | null;
  sectionSummaries?: {
    sectionId: number;
    sectionCode: string;
    assignedCount: number;
    maximumCapacity: number;
    reservedSeats?: number;
    occupancyPercent: number;
  }[] | null;
  constraints?: AllocationConstraintEvaluationDto[] | null;
  score?: AllocationScoreBreakdown | null;
  metadata?: Record<string, string> | null;
};

export type AllocationExecutionResult = {
  sessionId: string;
  scenarioId: string;
  succeeded: boolean;
  status: string;
  score?: AllocationScoreBreakdown | null;
  warnings?: string[] | null;
  errors?: string[] | null;
  durationMs: number;
  /** May be absent/partial on malformed payloads — UI must tolerate. */
  scenario?: AllocationScenarioDto | null;
  trace?: { traceId: string; steps?: AllocationTraceStep[] | null } | null;
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

/** Existing engine catalog — UI must not invent grouping modes. */
export const listAllocationGroupingModes = () => api.get<string[]>("/allocation/grouping-modes");

export const listAllocationPipelineStrategies = () => api.get<string[]>("/allocation/pipeline-strategies");

export const getAllocationConstraintPriorityDefaults = () =>
  api.get<Record<string, string>>("/allocation/constraint-priorities");

/** Default strategy toggles aligned with AllocationPipelineConfig.Default (server remains authoritative). */
export const DEFAULT_ALLOCATION_STRATEGIES: Record<string, boolean> = {
  Validation: true,
  Capacity: true,
  RollNumberBands: false,
  Policy: true,
  Gender: true,
  Language: true,
  Scholarship: false,
  Elective: false,
  Transport: false,
  Hostel: false,
  Merit: false,
  Scoring: true,
};

/** Default constraint priorities aligned with AllocationPipelineConfig.Default. */
export const DEFAULT_CONSTRAINT_PRIORITIES: Record<string, AllocationConstraintPriority> = {
  Capacity: "Mandatory",
  ReservedSeats: "Mandatory",
  GenderBalance: "Preferred",
  Language: "Preferred",
  Merit: "Preferred",
  Hostel: "Informational",
  Transport: "Informational",
  ElectiveCombination: "Preferred",
  MinorSubject: "Informational",
  Scholarship: "Preferred",
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

/** Save scenario to allocation sandbox (draft) — no live StudentSection writes. */
export const saveAllocationSandboxDraft = (scenarioId: string, name: string, tags?: string) =>
  api.post<AllocationSandboxItem>("/allocation/sandbox", null, {
    params: { scenarioId, name, tags },
  });

export const listAllocationSandbox = (includeArchived = false) =>
  api.get<AllocationSandboxItem[]>("/allocation/sandbox", { params: { includeArchived } });

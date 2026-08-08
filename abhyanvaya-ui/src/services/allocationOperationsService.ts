import api from "../api/axios";
import type { AllocationExecutionResult } from "./allocationPlatformService";

export type AllocationHistoryRow = {
  sessionId: string;
  scenarioId: string;
  createdAt: string;
  status: string;
  lifecycleStatus: string;
  score: number;
  groupingMode: string;
  versionNumber: number;
  courseId: number;
  groupId: number;
  semesterId: number;
};

export type AllocationGovernanceResult = {
  success: boolean;
  operation: string;
  scenarioId?: string;
  scenarioVersion?: number;
  message: string;
  canApprove: boolean;
  blockingReasons: string[];
  warnings: string[];
  errors: string[];
  contextStale: boolean;
  checksumInvalid: boolean;
  concurrencyConflict: boolean;
  authorizationFailure: boolean;
  scenarioContextVersion?: string;
  currentContextVersion?: string;
  contextCurrent: boolean;
};

export type AllocationScenarioVersionDto = {
  scenarioId: string;
  versionNumber: number;
  contextVersion: string;
  createdAt: string;
  createdBy?: number;
  reason: string;
  operation: string;
  status: string;
  score: number;
  checksum: string;
};

export type AllocationScenarioDetailDto = {
  scenarioId: string;
  sessionId: string;
  lifecycleStatus: string;
  status: string;
  currentVersionNumber: number;
  totalScore: number;
  contextVersion: string;
  currentContextVersion?: string;
  contextCurrent: boolean;
  scenarioChecksum: string;
  governance: AllocationGovernanceResult;
  versions: AllocationScenarioVersionDto[];
};

export type AllocationOpsDashboardDto = {
  totalRuns: number;
  successfulRuns: number;
  failedRuns: number;
  cancelledRuns: number;
  timedOutRuns: number;
  runningRuns: number;
  studentsAllocated: number;
  studentsUnallocated: number;
  averageScore: number;
  overCapacitySections: number;
  nearCapacitySections: number;
  underUtilizedSections: number;
  optimalSections: number;
  mandatoryViolations: number;
  preferredWarnings: number;
  informationalFindings: number;
  mandatoryCompliance: number;
  preferredCompliance: number;
  compliancePercent: number;
  draftCount: number;
  underReviewCount: number;
  approvedCount: number;
  rejectedCount: number;
  archivedCount: number;
  recentRuns: AllocationHistoryRow[];
  heatmap: {
    title: string;
    scopeNote: string;
    scenarioId?: string;
    lifecycleStatus?: string;
    cells: {
      sectionId: number;
      sectionCode: string;
      occupancyPercent: number;
      band: string;
      studentCount: number;
      maximumCapacity: number;
    }[];
    averageOccupancy: number;
  };
  constraints: {
    totalConstraints: number;
    mandatoryViolations: number;
    preferredViolations: number;
    informationalFindings: number;
    mandatoryCompliance: number;
    preferredCompliance: number;
    compliancePercent: number;
    rows: { constraintCode: string; priority: string; satisfied: boolean; summary: string }[];
  };
};

export type AllocationAnalyticsDto = {
  period: string;
  totalRuns: number;
  successRate: number;
  successfulRuns: number;
  failedRuns: number;
  cancelledRuns: number;
  timedOutRuns: number;
  runningRuns: number;
  studentsAllocated: number;
  averageSectionOccupancy: number;
  mandatoryCompliance: number;
  preferredCompliance: number;
  informationalFindings: number;
  averageScore: number;
};

export type AllocationMultiCompareReport = {
  originalScore: number;
  bestScenarioId?: string;
  bestScenarioLabel?: string;
  improvementVsOriginal: number;
  summary: string;
  scenarios: { scenarioId: string; label: string; score: number }[];
};

export const getAllocationOperations = () => api.get<AllocationOpsDashboardDto>("/allocation/operations");
export const getAllocationScenarios = () => api.get<AllocationHistoryRow[]>("/allocation/scenarios");
export const getAllocationScenarioDetail = (id: string) =>
  api.get<AllocationScenarioDetailDto>(`/allocation/scenarios/${id}`);
export const getAllocationScenarioVersions = (id: string) =>
  api.get<AllocationScenarioVersionDto[]>(`/allocation/scenarios/${id}/versions`);
export const getAllocationAnalytics = (period = "AcademicYear") =>
  api.get<AllocationAnalyticsDto>("/allocation/analytics", { params: { period } });
export const compareAllocationScenarios = (scenarioIds: string[]) =>
  api.post<AllocationMultiCompareReport>("/allocation/scenarios/compare", scenarioIds);
export const replayAllocationScenario = (id: string) =>
  api.post<AllocationExecutionResult>(`/allocation/scenarios/${id}/replay`);
export const reviewAllocationScenario = (id: string, notes?: string) =>
  api.post<AllocationGovernanceResult>(`/allocation/scenarios/${id}/review`, null, { params: { notes } });
export const archiveAllocationScenario = (id: string) =>
  api.post<AllocationGovernanceResult>(`/allocation/scenarios/${id}/archive`);
export const approveAllocationScenario = (id: string) =>
  api.post<AllocationGovernanceResult>(`/allocation/scenarios/${id}/approve`);
export const rejectAllocationScenario = (id: string, reason?: string) =>
  api.post<AllocationGovernanceResult>(`/allocation/scenarios/${id}/reject`, null, { params: { reason } });

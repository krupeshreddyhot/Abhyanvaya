/**
 * AI29.1D.24B.4A.2 — Safe accessors for AllocationExecutionResult.
 *
 * Contract (C# AllocationExecutionResult + AllocationScenario):
 * - Student recommendations live at result.scenario.recommendations
 * - There is NO root-level result.recommendations on AllocationExecutionResult
 * - SectionAllocationContext.recommendations is a different type (string[] health tips)
 *
 * Callers must use these helpers (or equivalent optional chaining) so a missing
 * scenario / recommendations never crashes Preview / Test Allocation rendering.
 */

import type {
  AllocationConstraintEvaluationDto,
  AllocationExecutionResult,
  AllocationStudentRecommendation,
  AllocationTraceStep,
} from "../services/allocationPlatformService";

export type ExecutionSectionSummary = {
  sectionId: number;
  sectionCode: string;
  assignedCount: number;
  maximumCapacity: number;
  reservedSeats?: number;
  occupancyPercent: number;
};

export function getExecutionRecommendations(
  result: AllocationExecutionResult | null | undefined,
): AllocationStudentRecommendation[] {
  const list = result?.scenario?.recommendations;
  return Array.isArray(list) ? list : [];
}

export function getExecutionSectionSummaries(
  result: AllocationExecutionResult | null | undefined,
): ExecutionSectionSummary[] {
  const list = result?.scenario?.sectionSummaries;
  return Array.isArray(list) ? list : [];
}

export function getExecutionConstraints(
  result: AllocationExecutionResult | null | undefined,
): AllocationConstraintEvaluationDto[] {
  const list = result?.scenario?.constraints;
  return Array.isArray(list) ? list : [];
}

export function getExecutionWarnings(result: AllocationExecutionResult | null | undefined): string[] {
  const list = result?.warnings;
  return Array.isArray(list) ? list : [];
}

export function getExecutionErrors(result: AllocationExecutionResult | null | undefined): string[] {
  const list = result?.errors;
  return Array.isArray(list) ? list : [];
}

export function getExecutionTraceSteps(
  result: AllocationExecutionResult | null | undefined,
): AllocationTraceStep[] {
  const list = result?.trace?.steps;
  return Array.isArray(list) ? list : [];
}

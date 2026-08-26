import { describe, expect, it } from "vitest";
import { normalizeAllocationConstraintPriority } from "./allocationConstraintPriority";
import {
  getExecutionErrors,
  getExecutionRecommendations,
  getExecutionWarnings,
} from "./allocationExecutionResultAccessors";
import type { AllocationExecutionResult } from "../services/allocationPlatformService";

describe("AI29.1D.24B.4A.2 allocationConstraintPriority", () => {
  it("normalizes string and numeric enum forms", () => {
    expect(normalizeAllocationConstraintPriority(0)).toBe("Mandatory");
    expect(normalizeAllocationConstraintPriority("0")).toBe("Mandatory");
    expect(normalizeAllocationConstraintPriority("Mandatory")).toBe("Mandatory");
    expect(normalizeAllocationConstraintPriority(1)).toBe("Preferred");
    expect(normalizeAllocationConstraintPriority(2)).toBe("Informational");
    expect(normalizeAllocationConstraintPriority(null)).toBe("Preferred");
  });
});

describe("AI29.1D.24B.4A.2 allocationExecutionResultAccessors", () => {
  it("reads recommendations only from scenario.recommendations", () => {
    const result = {
      sessionId: "s",
      scenarioId: "sc",
      succeeded: true,
      status: "Completed",
      durationMs: 1,
      scenario: {
        scenarioId: "sc",
        recommendations: [
          { studentId: 1, toSectionId: 10, toSectionCode: "A", explanations: ["ok"] },
        ],
      },
    } as AllocationExecutionResult;
    expect(getExecutionRecommendations(result)).toHaveLength(1);
    expect(getExecutionRecommendations({ ...result, scenario: null })).toEqual([]);
    expect(getExecutionWarnings({ ...result, warnings: undefined })).toEqual([]);
    expect(getExecutionErrors({ ...result, errors: null })).toEqual([]);
  });
});

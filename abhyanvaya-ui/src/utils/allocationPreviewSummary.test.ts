import { describe, expect, it } from "vitest";
import type { AllocationExecutionResult } from "../services/allocationPlatformService";
import { buildAllocationPreviewRows, buildAllocationPreviewSummary } from "./allocationPreviewSummary";

const sample: AllocationExecutionResult = {
  sessionId: "s1",
  scenarioId: "sc1",
  succeeded: true,
  status: "Completed",
  durationMs: 10,
  warnings: [],
  errors: [],
  score: { totalScore: 88, capacityUtilization: 80, policyCompliance: 90, genderBalance: 70, summary: "Good" },
  scenario: {
    scenarioId: "sc1",
    recommendations: [
      {
        studentId: 1,
        studentNumber: "A1",
        studentName: "Ada",
        fromSectionCode: null,
        toSectionId: 10,
        toSectionCode: "A",
        explanations: ["✓ Capacity available"],
      },
      {
        studentId: 2,
        studentNumber: "B2",
        studentName: "Bob",
        fromSectionCode: "A",
        toSectionId: 11,
        toSectionCode: "B",
        explanations: ["✓ Gender balance improved"],
      },
    ],
    sectionSummaries: [
      { sectionId: 10, sectionCode: "A", assignedCount: 1, maximumCapacity: 30, occupancyPercent: 3 },
      { sectionId: 11, sectionCode: "B", assignedCount: 1, maximumCapacity: 30, occupancyPercent: 3 },
      { sectionId: 12, sectionCode: "C", assignedCount: 0, maximumCapacity: 30, occupancyPercent: 0 },
    ],
    constraints: [
      { constraintCode: "Capacity", priority: "Mandatory", satisfied: true, summary: "ok" },
      { constraintCode: "GenderBalance", priority: "Preferred", satisfied: false, summary: "spread" },
      { constraintCode: "Hostel", priority: "Informational", satisfied: false, summary: "note" },
    ],
    score: { totalScore: 88, capacityUtilization: 80, policyCompliance: 90, genderBalance: 70, summary: "Good" },
  },
  trace: {
    traceId: "t1",
    steps: [
      {
        order: 1,
        strategyCode: "Capacity",
        enabled: true,
        executed: true,
        durationMs: 1,
        scoreAfter: 50,
        summary: "placed",
        constraintNotes: [],
      },
      {
        order: 2,
        strategyCode: "Gender",
        enabled: true,
        executed: true,
        durationMs: 1,
        scoreAfter: 70,
        summary: "balanced",
        constraintNotes: ["Gender preferred"],
      },
    ],
  },
};

describe("allocationPreviewSummary", () => {
  it("builds summary counts from engine scenario", () => {
    const summary = buildAllocationPreviewSummary(sample, { totalEligibleStudents: 3, groupingMode: "Alphabetical" });
    expect(summary?.totalStudents).toBe(3);
    expect(summary?.allocated).toBe(2);
    expect(summary?.unallocated).toBe(1);
    expect(summary?.sectionA?.assignedCount).toBe(1);
    expect(summary?.sectionB?.sectionCode).toBe("B");
    expect(summary?.sectionC?.assignedCount).toBe(0);
    expect(summary?.constraints.preferredViolations).toBe(1);
    expect(summary?.constraints.informationalFindings).toBe(1);
    expect(summary?.constraints.mandatoryViolations).toBe(0);
    expect(summary?.strategiesSummary).toContain("Alphabetical");
  });

  it("builds rows from recommendations and explanations only", () => {
    const rows = buildAllocationPreviewRows(sample, {
      groupingMode: "Gender",
      eligibleStudents: [
        { studentId: 1, studentNumber: "A1", studentName: "Ada" },
        { studentId: 3, studentNumber: "C3", studentName: "Cara", currentSectionCode: null },
      ],
    });
    expect(rows).toHaveLength(3);
    expect(rows[0].allocationReason).toContain("Capacity");
    expect(rows[0].strategy).toContain("Gender");
    expect(rows.find((r) => r.studentId === 3)?.allocated).toBe(false);
  });

  it("caps preview rows for enterprise UI windows", () => {
    const rows = buildAllocationPreviewRows(sample, {
      groupingMode: "Gender",
      eligibleStudents: [
        { studentId: 1, studentNumber: "A1", studentName: "Ada" },
        { studentId: 3, studentNumber: "C3", studentName: "Cara", currentSectionCode: null },
      ],
      maxRows: 2,
    });
    expect(rows).toHaveLength(2);
  });

  it("reads recommendations from scenario.recommendations (not root)", () => {
    const withRootNoise = {
      ...sample,
      recommendations: [{ studentId: 999, toSectionId: 1, toSectionCode: "X", explanations: [] }],
    } as AllocationExecutionResult & { recommendations: unknown };
    const rows = buildAllocationPreviewRows(withRootNoise);
    expect(rows.every((r) => r.studentId !== 999)).toBe(true);
    expect(rows.map((r) => r.studentId).sort()).toEqual([1, 2]);
  });

  it("does not crash when constraint priority is numeric enum 0/1/2", () => {
    const numeric = {
      ...sample,
      scenario: {
        ...sample.scenario!,
        constraints: [
          { constraintCode: "Capacity", priority: 0, satisfied: true, summary: "ok" },
          { constraintCode: "GenderBalance", priority: 1, satisfied: false, summary: "spread" },
          { constraintCode: "Hostel", priority: 2, satisfied: false, summary: "note" },
        ],
      },
    } as AllocationExecutionResult;
    const summary = buildAllocationPreviewSummary(numeric, { totalEligibleStudents: 2 });
    expect(summary?.constraints.preferredViolations).toBe(1);
    expect(summary?.constraints.informationalFindings).toBe(1);
    expect(buildAllocationPreviewRows(numeric).length).toBeGreaterThan(0);
  });

  it("tolerates missing scenario / warnings / trace without throwing", () => {
    const thin = {
      sessionId: "s",
      scenarioId: "sc",
      succeeded: true,
      status: "Completed",
      durationMs: 1,
    } as AllocationExecutionResult;
    expect(buildAllocationPreviewSummary(thin)?.allocated).toBe(0);
    expect(buildAllocationPreviewRows(thin)).toEqual([]);
  });
});

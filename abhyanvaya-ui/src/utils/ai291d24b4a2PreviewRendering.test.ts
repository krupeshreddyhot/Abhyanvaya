/**
 * AI29.1D.24B.4A.2 Prompt 5 — Focused component-level regression & error recovery.
 * No skipped tests. Covers numeric priorities, sparse simulate payloads, ErrorBoundary recovery.
 */
import { act, createElement, useState, type ReactNode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import type { AllocationExecutionResult } from "../services/allocationPlatformService";
import {
  buildAllocationPreviewRows,
  buildAllocationPreviewSummary,
} from "./allocationPreviewSummary";
import { extractCapacityViolations } from "./allocationCapacityViolations";
import { normalizeAllocationConstraintPriority } from "./allocationConstraintPriority";
import { priorityDisplayLabel } from "./allocationAdministratorCopy";
import {
  getExecutionRecommendations,
  getExecutionConstraints,
  getExecutionTraceSteps,
  getExecutionWarnings,
  getExecutionErrors,
} from "./allocationExecutionResultAccessors";
import AllocationPreviewErrorBoundary from "../components/allocation/AllocationPreviewErrorBoundary";

const realisticSimulate: AllocationExecutionResult = {
  sessionId: "11111111-1111-1111-1111-111111111111",
  scenarioId: "22222222-2222-2222-2222-222222222222",
  succeeded: true,
  status: "Completed",
  durationMs: 42,
  warnings: ["Your allocation band contains more students than section SCCA02 can hold."],
  errors: [],
  score: {
    totalScore: 68.78,
    capacityUtilization: 70,
    policyCompliance: 80,
    genderBalance: 60,
    summary: "Balanced",
  },
  scenario: {
    scenarioId: "22222222-2222-2222-2222-222222222222",
    recommendations: [
      {
        studentId: 5,
        studentNumber: "105325405005",
        studentName: "AGISHALA VARALAXMI",
        fromSectionId: 5,
        fromSectionCode: "CA-B",
        toSectionId: 5,
        toSectionCode: "CA-B",
        explanations: ["Kept in Section CA-B because Preserve Existing Assignments is selected."],
      },
      {
        studentId: 6,
        studentNumber: "105325405006",
        studentName: "Sample Student",
        fromSectionCode: null,
        toSectionId: 3,
        toSectionCode: "SCCA01",
        explanations: ["Assigned to Section SCCA01 because capacity was available."],
      },
    ],
    sectionSummaries: [
      { sectionId: 3, sectionCode: "SCCA01", assignedCount: 1, maximumCapacity: 60, occupancyPercent: 1.67 },
      { sectionId: 5, sectionCode: "CA-B", assignedCount: 1, maximumCapacity: 60, occupancyPercent: 1.67 },
      { sectionId: 8, sectionCode: "SCCA02", assignedCount: 0, maximumCapacity: 50, occupancyPercent: 0 },
    ],
    constraints: [
      { constraintCode: "Capacity", priority: 0, satisfied: true, summary: "All sections within hard capacity." },
      { constraintCode: "GenderBalance", priority: 1, satisfied: false, summary: "Preferred balance not met." },
      { constraintCode: "Hostel", priority: 2, satisfied: false, summary: "Informational hostel note." },
    ],
    score: {
      totalScore: 68.78,
      capacityUtilization: 70,
      policyCompliance: 80,
      genderBalance: 60,
      summary: "Balanced",
    },
  },
  trace: {
    traceId: "33333333-3333-3333-3333-333333333333",
    steps: [
      {
        order: 1,
        strategyCode: "RollNumberBands",
        enabled: true,
        executed: true,
        durationMs: 5,
        scoreAfter: 50,
        summary: "Bands applied",
        constraintNotes: [],
      },
    ],
  },
};

describe("AI29.1D.24B.4A.2 Prompt 5 — numeric priority", () => {
  it("1. numeric priority 0 → Mandatory / Required", () => {
    expect(normalizeAllocationConstraintPriority(0)).toBe("Mandatory");
    expect(priorityDisplayLabel(0)).toBe("Required");
    const v = extractCapacityViolations([
      { constraintCode: "Capacity", priority: 0, satisfied: false, summary: "Over" },
    ]);
    expect(v[0]?.isMandatory).toBe(true);
  });

  it("2. numeric priority 1 → Preferred", () => {
    expect(normalizeAllocationConstraintPriority(1)).toBe("Preferred");
    expect(priorityDisplayLabel(1)).toBe("Preferred");
    const summary = buildAllocationPreviewSummary({
      ...realisticSimulate,
      scenario: {
        ...realisticSimulate.scenario!,
        constraints: [{ constraintCode: "GenderBalance", priority: 1, satisfied: false, summary: "spread" }],
      },
    });
    expect(summary?.constraints.preferredViolations).toBe(1);
  });

  it("3. numeric priority 2 → Informational", () => {
    expect(normalizeAllocationConstraintPriority(2)).toBe("Informational");
    expect(priorityDisplayLabel(2)).toBe("Informational");
    const summary = buildAllocationPreviewSummary({
      ...realisticSimulate,
      scenario: {
        ...realisticSimulate.scenario!,
        constraints: [{ constraintCode: "Hostel", priority: 2, satisfied: false, summary: "note" }],
      },
    });
    expect(summary?.constraints.informationalFindings).toBe(1);
  });
});

describe("AI29.1D.24B.4A.2 Prompt 5 — simulate payload resilience", () => {
  it("4. successful realistic simulation response builds summary and rows", () => {
    const summary = buildAllocationPreviewSummary(realisticSimulate, {
      totalEligibleStudents: 10,
      groupingMode: "LastThreeDigits",
    });
    expect(summary).not.toBeNull();
    expect(summary!.allocated).toBe(2);
    expect(summary!.unallocated).toBe(8);
    expect(summary!.sectionCounts.length).toBe(3);
    expect(summary!.constraints.preferredViolations).toBe(1);
    expect(summary!.constraints.informationalFindings).toBe(1);
    expect(summary!.totalScore).toBe(68.78);

    const rows = buildAllocationPreviewRows(realisticSimulate, { groupingMode: "LastThreeDigits" });
    expect(rows.length).toBe(2);
    expect(rows[0].allocationReason).toMatch(/Preserve Existing/i);
    expect(getExecutionRecommendations(realisticSimulate)).toHaveLength(2);
    expect(getExecutionWarnings(realisticSimulate)).toHaveLength(1);
  });

  it("5. missing scenario does not throw and yields empty recommendations", () => {
    const thin = {
      sessionId: "s",
      scenarioId: "sc",
      succeeded: true,
      status: "Completed",
      durationMs: 1,
    } as AllocationExecutionResult;
    expect(() => buildAllocationPreviewSummary(thin)).not.toThrow();
    expect(buildAllocationPreviewSummary(thin)?.allocated).toBe(0);
    expect(getExecutionRecommendations(thin)).toEqual([]);
    expect(buildAllocationPreviewRows(thin)).toEqual([]);
  });

  it("6. missing recommendations does not throw", () => {
    const payload: AllocationExecutionResult = {
      ...realisticSimulate,
      scenario: { ...realisticSimulate.scenario!, recommendations: undefined },
    };
    expect(getExecutionRecommendations(payload)).toEqual([]);
    expect(buildAllocationPreviewSummary(payload, { totalEligibleStudents: 5 })?.allocated).toBe(0);
    expect(buildAllocationPreviewRows(payload)).toEqual([]);
  });

  it("7. missing constraints does not throw", () => {
    const payload: AllocationExecutionResult = {
      ...realisticSimulate,
      scenario: { ...realisticSimulate.scenario!, constraints: undefined },
    };
    expect(getExecutionConstraints(payload)).toEqual([]);
    const summary = buildAllocationPreviewSummary(payload);
    expect(summary?.constraints.mandatoryViolations).toBe(0);
    expect(summary?.constraintResultSummary).toBe("—");
  });

  it("8. missing trace does not throw", () => {
    const payload: AllocationExecutionResult = { ...realisticSimulate, trace: undefined };
    expect(getExecutionTraceSteps(payload)).toEqual([]);
    const rows = buildAllocationPreviewRows(payload);
    expect(rows[0]?.strategy).toBe("—");
  });

  it("9. missing score does not throw", () => {
    const payload: AllocationExecutionResult = {
      ...realisticSimulate,
      score: undefined,
      scenario: { ...realisticSimulate.scenario!, score: undefined },
    };
    const summary = buildAllocationPreviewSummary(payload);
    expect(summary?.totalScore).toBeUndefined();
    expect(buildAllocationPreviewRows(payload)[0]?.score).toBe("—");
  });

  it("10. missing explanations does not throw", () => {
    const payload: AllocationExecutionResult = {
      ...realisticSimulate,
      scenario: {
        ...realisticSimulate.scenario!,
        recommendations: [
          {
            studentId: 9,
            toSectionId: 3,
            toSectionCode: "SCCA01",
            explanations: undefined as unknown as string[],
          },
        ],
      },
    };
    const rows = buildAllocationPreviewRows(payload);
    expect(rows).toHaveLength(1);
    expect(rows[0].allocationReason).toBe("—");
  });
});

describe("AI29.1D.24B.4A.2 Prompt 5 — ErrorBoundary recovery", () => {
  let container: HTMLDivElement;
  let root: Root;

  beforeEach(() => {
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
    container = document.createElement("div");
    document.body.appendChild(container);
    root = createRoot(container);
  });

  afterEach(() => {
    act(() => {
      root.unmount();
    });
    container.remove();
  });

  function Boom({ message }: { message: string }): ReactNode {
    throw new Error(message);
  }

  it("11. ErrorBoundary recovery UI hides stack / checksum / claims / API paths", () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    act(() => {
      root.render(
        createElement(
          AllocationPreviewErrorBoundary,
          null,
          createElement(Boom, {
            message: "TypeError at /api/allocation/simulate checksum=abc claim=Allocation.Run stack: Error: boom",
          }),
        ),
      );
    });

    const text = container.textContent ?? "";
    expect(text).toMatch(/Allocation preview could not be displayed/i);
    expect(text).not.toMatch(/\/api\//i);
    expect(text).not.toMatch(/checksum/i);
    expect(text).not.toMatch(/Allocation\.Run/i);
    expect(text).not.toMatch(/TypeError/i);
    expect(text).not.toMatch(/stack/i);
    expect(text).not.toMatch(/componentStack/i);
    expect(container.querySelector('[data-testid="allocation-preview-error-recovery"]')).toBeTruthy();
    spy.mockRestore();
  });

  it("12. Preview after recovery invokes onPreview and clears fault UI", () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    let previewCalls = 0;

    function Harness() {
      const [boom, setBoom] = useState(true);
      return createElement(
        AllocationPreviewErrorBoundary,
        {
          onPreview: () => {
            previewCalls += 1;
            setBoom(false);
          },
          onTestAllocation: () => {},
        },
        boom
          ? createElement(Boom, { message: "render fault" })
          : createElement("div", { "data-testid": "preview-ok" }, "Preview recovered"),
      );
    }

    act(() => {
      root.render(createElement(Harness));
    });
    expect(container.querySelector('[data-testid="allocation-preview-recover-preview"]')).toBeTruthy();

    act(() => {
      container.querySelector<HTMLButtonElement>('[data-testid="allocation-preview-recover-preview"]')?.click();
    });

    expect(previewCalls).toBe(1);
    expect(container.textContent).toMatch(/Preview recovered/);
    expect(container.querySelector('[data-testid="allocation-preview-error-recovery"]')).toBeNull();
    spy.mockRestore();
  });

  it("13. Test Allocation after recovery invokes onTestAllocation and clears fault UI", () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    let testCalls = 0;

    function Harness() {
      const [boom, setBoom] = useState(true);
      return createElement(
        AllocationPreviewErrorBoundary,
        {
          onPreview: () => {},
          onTestAllocation: () => {
            testCalls += 1;
            setBoom(false);
          },
        },
        boom
          ? createElement(Boom, { message: "render fault" })
          : createElement("div", { "data-testid": "test-ok" }, "Test Allocation recovered"),
      );
    }

    act(() => {
      root.render(createElement(Harness));
    });

    act(() => {
      container.querySelector<HTMLButtonElement>('[data-testid="allocation-preview-recover-test"]')?.click();
    });

    expect(testCalls).toBe(1);
    expect(container.textContent).toMatch(/Test Allocation recovered/);
    expect(container.querySelector('[data-testid="allocation-preview-error-recovery"]')).toBeNull();
    spy.mockRestore();
  });
});

describe("AI29.1D.24B.4A.2 Prompt 5 — Operations.View technical gate (static)", () => {
  it("workspace gates Technical Details and ops links with AllocationOperationsView", () => {
    const workspace = readFileSync(
      resolve(__dirname, "../components/allocation/EnterpriseAllocationWorkspace.tsx"),
      "utf8",
    );
    expect(workspace).toContain("PermissionKeys.AllocationOperationsView");
    expect(workspace).toMatch(/showTechnicalDetails\s*=\s*hasPermission\(PermissionKeys\.AllocationOperationsView\)/);
    expect(workspace).toContain("onPreview={() => void doSimulate()}");
    expect(workspace).toContain("onTestAllocation=");
  });

  it("ErrorBoundary source never renders stack/checksum/claims/API paths", () => {
    const src = readFileSync(
      resolve(__dirname, "../components/allocation/AllocationPreviewErrorBoundary.tsx"),
      "utf8",
    );
    expect(src).not.toMatch(/error\.message/);
    expect(src).not.toMatch(/error\.stack/);
    expect(src).not.toMatch(/componentStack\}/);
    expect(src).toContain("redacted from UI");
    expect(src).toContain("onPreview");
    expect(src).toContain("onTestAllocation");
  });

  it("accessors never read root-level result.recommendations", () => {
    const src = readFileSync(
      resolve(__dirname, "./allocationExecutionResultAccessors.ts"),
      "utf8",
    );
    expect(src).toContain("scenario?.recommendations");
    expect(src).not.toMatch(/result\?\.recommendations(?!\s*\?)/);
    expect(getExecutionErrors({ ...realisticSimulate, errors: undefined })).toEqual([]);
  });
});

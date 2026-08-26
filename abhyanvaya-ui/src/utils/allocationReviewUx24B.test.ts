import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const workspace = readFileSync(
  resolve(__dirname, "../components/allocation/EnterpriseAllocationWorkspace.tsx"),
  "utf8",
);
const strategy = readFileSync(
  resolve(__dirname, "../components/allocation/AllocationStrategyConfigPanel.tsx"),
  "utf8",
);
const governance = readFileSync(
  resolve(__dirname, "../components/allocation/AllocationGovernancePanel.tsx"),
  "utf8",
);
const preview = readFileSync(
  resolve(__dirname, "../components/allocation/AllocationPreviewPanel.tsx"),
  "utf8",
);

describe("AI29.1D.24B Allocation Review UX separation", () => {
  it("uses business workflow labels in the workspace", () => {
    expect(workspace).toContain("Allocation Rules");
    expect(workspace).toContain("Review Allocation");
    expect(workspace).toContain("Approve Allocation");
    expect(workspace).toContain("ALLOCATION_WORKSPACE_BANNER");
    expect(workspace).not.toContain("Guided AI29.1C");
    expect(workspace).not.toContain("Review — Governance Lifecycle");
  });

  it("hides engine payload JSON from default strategy UI", () => {
    expect(strategy).toContain("Student Order");
    expect(strategy).toContain("Section Allocation Method");
    expect(strategy).toContain("Selected Allocation Rules");
    expect(strategy).toContain("Advanced Allocation Options");
    expect(strategy).toContain("showTechnicalDetails");
    expect(strategy).not.toContain("Engine payload preview");
    expect(strategy).not.toContain("Pipeline strategies");
  });

  it("review panel uses AcademicConfirmDialog and business blockers", () => {
    expect(governance).toContain("AcademicConfirmDialog");
    expect(governance).toContain("Approve Allocation");
    expect(governance).toContain("LABEL_REVIEW_ACADEMIC_SCOPE");
    expect(governance).toContain("Allocation Status");
    expect(governance).not.toContain("Flag: stale context");
    expect(governance).not.toContain("Approval blocked — exact governance reasons");
    expect(governance).toContain("showTechnicalDetails");
    expect(governance).toContain("Technical Details");
  });

  it("preview uses business column labels and hides engine jargon by default", () => {
    expect(preview).toContain("Test Allocation");
    expect(preview).toContain("Allocation Summary");
    expect(preview).toContain("Rule Applied");
    expect(preview).toContain("Capacity Status");
    expect(preview).not.toContain("Allocation Engine scenario");
    expect(preview).not.toContain("Allocation trace (engine)");
  });

  it("does not recreate approval rules in React — still reads canApprove from governance", () => {
    expect(governance).toContain("activeGov?.canApprove === true");
    expect(workspace).toContain("latest.governance.canApprove === false");
  });
});

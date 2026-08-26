import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  ALLOCATION_WORKSPACE_BANNER,
  LABEL_REPLAY_ALLOCATION,
  LABEL_REVIEW_ACADEMIC_SCOPE,
  MSG_ALLOCATION_CREATED,
  MSG_REPLAY_COMPLETED,
  MSG_REVIEW_SCOPE_THEN_GENERATE,
  MSG_TEST_ALLOCATION_SUCCESS,
  presentAllocationIssue,
  sanitizeAdministratorMessage,
  versionActionLabel,
} from "./allocationAdministratorCopy";

const workspace = readFileSync(
  resolve(__dirname, "../components/allocation/EnterpriseAllocationWorkspace.tsx"),
  "utf8",
);
const governance = readFileSync(
  resolve(__dirname, "../components/allocation/AllocationGovernancePanel.tsx"),
  "utf8",
);
const population = readFileSync(
  resolve(__dirname, "../components/allocation/StudentPopulationFilterPanel.tsx"),
  "utf8",
);
const capacity = readFileSync(
  resolve(__dirname, "../components/allocation/AllocationCapacityPanel.tsx"),
  "utf8",
);
const opsPage = readFileSync(
  resolve(__dirname, "../pages/setup/AllocationOperationsPage.tsx"),
  "utf8",
);
const opsService = readFileSync(
  resolve(__dirname, "../services/allocationOperationsService.ts"),
  "utf8",
);
const permissionKeys = readFileSync(resolve(__dirname, "../auth/permissionKeys.ts"), "utf8");

describe("AI29.1D.24B.1 Prompt 2 / 2A — administrator language", () => {
  it("uses concise business banner without AI29/engine marketing", () => {
    expect(ALLOCATION_WORKSPACE_BANNER).toMatch(/prepare, test, review, and approve/i);
    expect(ALLOCATION_WORKSPACE_BANNER).not.toMatch(/AI29|Engine|governance/i);
    expect(workspace).toContain("ALLOCATION_WORKSPACE_BANNER");
  });

  it("normal success messages omit scenario GUID, engine, and StudentSection", () => {
    expect(MSG_TEST_ALLOCATION_SUCCESS).toBe("Test allocation completed. No student records were changed.");
    expect(MSG_ALLOCATION_CREATED).toBe("Allocation created successfully.");
    expect(MSG_TEST_ALLOCATION_SUCCESS).not.toMatch(/scenario|engine|StudentSection|GUID/i);
    expect(workspace).toContain("MSG_TEST_ALLOCATION_SUCCESS");
    expect(workspace).not.toMatch(/no live StudentSection/i);
    expect(workspace).not.toMatch(/Engine-produced/i);
  });

  it("sanitizer strips GUIDs and technical tokens from admin messages", () => {
    const msg = sanitizeAdministratorMessage(
      "Simulation completed (scenario 550e8400-e29b-41d4-a716-446655440000). Engine StudentSection checksum canApprove",
    );
    expect(msg).not.toMatch(/550e8400/i);
    expect(msg).not.toMatch(/StudentSection/);
    expect(msg).not.toMatch(/canApprove/);
    expect(msg).not.toMatch(/\bchecksum\b/i);
  });

  it("normal workflow omits engine terminology in population/capacity panels", () => {
    expect(population).not.toContain("Allocation Engine");
    expect(population).not.toContain("Students API");
    expect(capacity).not.toContain("Refresh Capacity Engine");
    expect(capacity).not.toContain("/sections/capacity");
    expect(capacity).toContain("Refresh Capacity");
  });

  it("checksum / canApprove property names stay out of normal governance UI", () => {
    expect(governance).not.toContain("canApprove=");
    expect(governance).not.toContain("Governance evaluation: canApprove");
    expect(governance).not.toContain("Flag: stale context");
    const techIdx = governance.indexOf("Technical Details");
    const checksumIdx = governance.indexOf("Checksum:");
    expect(techIdx).toBeGreaterThan(-1);
    expect(checksumIdx).toBeGreaterThan(techIdx);
  });

  it("Technical Details retains diagnostics and remains permission-gated", () => {
    expect(governance).toContain("showTechnicalDetails");
    expect(governance).toContain("Technical Details");
    expect(governance).toContain("scenarioDetail.scenarioId");
    expect(governance).toContain("scenarioChecksum");
    expect(workspace).toContain("AllocationOperationsView");
    expect(workspace).toContain("showTechnicalDetails={showTechnicalDetails}");
  });

  it("A–C: Replay labels and version history (not Regenerate)", () => {
    expect(LABEL_REPLAY_ALLOCATION).toBe("Replay Allocation");
    expect(MSG_REPLAY_COMPLETED).toBe("Allocation replay completed. Student records were not changed.");
    expect(versionActionLabel("Replay")).toBe("Replayed");
    expect(governance).toContain("LABEL_REPLAY_ALLOCATION");
    expect(governance).not.toContain("Regenerate Allocation");
    expect(governance).not.toContain("LABEL_REGENERATE");
  });

  it("D–I: stale context title/description and Review Academic Scope action", () => {
    const stale = presentAllocationIssue({ contextStale: true });
    expect(stale.title).toBe("Allocation needs to be rebuilt");
    expect(stale.description).not.toMatch(/stale context/i);
    expect(stale.description).not.toMatch(/checksum/i);
    expect(LABEL_REVIEW_ACADEMIC_SCOPE).toBe("Review Academic Scope");
    expect(governance).toContain("LABEL_REVIEW_ACADEMIC_SCOPE");
    expect(governance).not.toContain("Rebuild Allocation");
    expect(governance).not.toContain("Regenerate Allocation");
  });

  it("J–K: replay still uses replayAllocationScenario; no new API", () => {
    expect(workspace).toContain("replayAllocationScenario");
    expect(workspace).toContain("onReplay={() => void doReplay()}");
    expect(workspace).toMatch(/const doReplay = async \(\) => \{[\s\S]*replayAllocationScenario\(activeScenarioId\)/);
    expect(workspace).not.toContain("regenerateAllocation");
    expect(workspace).not.toContain("/allocation/regenerate");
  });
});

describe("AI29.1D.24B.1 Prompt 3 — rebuild / replay semantics", () => {
  it("1–3: stale context heading, no Flag jargon, Review Academic Scope action", () => {
    const stale = presentAllocationIssue({ contextStale: true });
    expect(stale.title).toBe("Allocation needs to be rebuilt");
    expect(stale.description).toBe(
      "The academic information used for this allocation has changed. Review the academic scope and generate the allocation again.",
    );
    expect(governance).toContain("Allocation needs to be rebuilt");
    expect(governance).not.toContain("Flag: stale context");
    expect(opsPage).not.toContain("Flag: stale context");
    expect(LABEL_REVIEW_ACADEMIC_SCOPE).toBe("Review Academic Scope");
    expect(governance).toContain("LABEL_REVIEW_ACADEMIC_SCOPE");
    expect(governance).not.toContain("Rebuild Allocation");
  });

  it("4–6: Review Academic Scope only navigates; no rebuild API; no auto runAllocation", () => {
    const rebuildHandlers = [...workspace.matchAll(/onRebuildAllocation=\{\(\) => \{([\s\S]*?)\}\}/g)].map((m) => m[1]);
    expect(rebuildHandlers.length).toBeGreaterThanOrEqual(1);
    for (const body of rebuildHandlers) {
      expect(body).toContain("setActiveStep(0)");
      expect(body).toContain("MSG_REVIEW_SCOPE_THEN_GENERATE");
      expect(body).not.toMatch(/replayAllocationScenario|runAllocation|api\.|fetch\(/);
    }
    expect(MSG_REVIEW_SCOPE_THEN_GENERATE).toMatch(/generate the allocation again/i);
    expect(MSG_REVIEW_SCOPE_THEN_GENERATE).not.toMatch(/rebuilt|rebuild completed/i);
    expect(workspace).not.toContain("/allocation/rebuild");
    expect(opsService).not.toContain("/allocation/rebuild");
    expect(opsService).not.toContain("rebuildAllocation");
  });

  it("7–8: Replay Allocation uses replayAllocationScenario and POST .../replay", () => {
    expect(LABEL_REPLAY_ALLOCATION).toBe("Replay Allocation");
    expect(opsPage).toContain("Replay Allocation");
    expect(workspace).toMatch(/const doReplay = async \(\) => \{[\s\S]*replayAllocationScenario\(activeScenarioId\)/);
    expect(opsPage).toMatch(/replayAllocationScenario\(id\)/);
    expect(opsService).toContain('api.post<AllocationExecutionResult>(`/allocation/scenarios/${id}/replay`)');
  });

  it("9–10: no new API or permission introduced for rebuild/replay labels", () => {
    expect(opsService).not.toMatch(/rebuildAllocation|regenerateAllocation|\/allocation\/rebuild|\/allocation\/regenerate/);
    expect(permissionKeys).toContain("AllocationScenarioReplay");
    expect(permissionKeys).not.toMatch(/AllocationScenarioRebuild|Allocation\.Rebuild|AllocationScenarioRegenerate/);
    expect(opsPage).toContain("PermissionKeys.AllocationScenarioReplay");
  });

  it("11: Approve remains gated by server governance.canApprove", () => {
    expect(governance).toContain("activeGov?.canApprove === true");
    expect(governance).toContain("approveAllowedByGovernance");
    expect(opsPage).toContain("!detail.governance?.canApprove");
    expect(governance).not.toMatch(/canApprove\s*=\s*!.*contextStale/);
    expect(opsPage).not.toMatch(/canApprove\s*=\s*!detail\.contextCurrent/);
  });
});

import { describe, expect, it } from "vitest";
import {
  governanceBlockingPresentations,
  governanceBlockingReasons,
  governancePrefersRebuild,
  toGovernanceLifecycleDisplay,
} from "./allocationGovernanceLifecycle";

describe("allocationGovernanceLifecycle", () => {
  it("maps engine lifecycle to Draft/Review/Approved/Rejected/Archived", () => {
    expect(toGovernanceLifecycleDisplay("Draft")).toBe("Draft");
    expect(toGovernanceLifecycleDisplay("Simulated")).toBe("Draft");
    expect(toGovernanceLifecycleDisplay("Reviewed")).toBe("Review");
    expect(toGovernanceLifecycleDisplay("Compared")).toBe("Review");
    expect(toGovernanceLifecycleDisplay("Approved")).toBe("Approved");
    expect(toGovernanceLifecycleDisplay("Rejected")).toBe("Rejected");
    expect(toGovernanceLifecycleDisplay("Archived")).toBe("Archived");
  });

  it("surfaces governance blockers in business language without inventing rules", () => {
    const reasons = governanceBlockingReasons({
      canApprove: false,
      blockingReasons: ["Scenario is archived.", "Mandatory constraints unresolved: Capacity."],
      contextStale: true,
      checksumInvalid: true,
      concurrencyConflict: false,
    });
    expect(reasons.some((r) => /archived/i.test(r))).toBe(true);
    expect(reasons.some((r) => /Required allocation rules|Capacity/i.test(r))).toBe(true);
    expect(reasons.some((r) => /needs to be rebuilt/i.test(r))).toBe(true);
    expect(reasons.some((r) => /data has changed/i.test(r))).toBe(true);
    expect(reasons.join(" ")).not.toMatch(/Flag: stale context/i);
  });

  it("prefers rebuild for stale / checksum conditions", () => {
    expect(governancePrefersRebuild({ contextStale: true })).toBe(true);
    expect(governancePrefersRebuild({ checksumInvalid: true })).toBe(true);
    expect(governancePrefersRebuild({ contextStale: false, checksumInvalid: false })).toBe(false);
  });

  it("presentations omit property names like canApprove", () => {
    const presentations = governanceBlockingPresentations({
      canApprove: false,
      contextStale: true,
      blockingReasons: [],
    });
    expect(presentations[0]?.title).toBe("Allocation needs to be rebuilt");
    expect(JSON.stringify(presentations)).not.toMatch(/canApprove/);
  });
});

import { describe, expect, it } from "vitest";
import {
  presentAllocationIssue,
  priorityDisplayLabel,
  priorityHelpText,
  sanitizeAdministratorMessage,
  versionActionLabel,
} from "./allocationAdministratorCopy";

describe("AI29.1D.24B allocationAdministratorCopy", () => {
  it("maps Mandatory to Required for display", () => {
    expect(priorityDisplayLabel("Mandatory")).toBe("Required");
    expect(priorityHelpText("Mandatory")).toMatch(/must satisfy/i);
  });

  it("maps numeric enum priorities without throwing", () => {
    expect(priorityDisplayLabel(0)).toBe("Required");
    expect(priorityDisplayLabel(1)).toBe("Preferred");
    expect(priorityDisplayLabel(2)).toBe("Informational");
  });

  it("presents stale context in business language", () => {
    const issue = presentAllocationIssue({ contextStale: true });
    expect(issue.title).toBe("Allocation needs to be rebuilt");
    expect(issue.prefersRebuild).toBe(true);
    expect(issue.description).toBe(
      "The academic information used for this allocation has changed. Review the academic scope and generate the allocation again.",
    );
    expect(issue.description).not.toMatch(/stale context/i);
    expect(issue.description).not.toMatch(/checksum/i);
  });

  it("presents checksum / concurrency / mandatory cases", () => {
    expect(presentAllocationIssue({ checksumInvalid: true }).title).toMatch(/data has changed/i);
    expect(presentAllocationIssue({ concurrencyConflict: true }).title).toMatch(/updated elsewhere/i);
    expect(presentAllocationIssue({ rawReason: "Mandatory constraints unresolved" }).title).toMatch(/Required/i);
    expect(presentAllocationIssue({ rawReason: "Scenario is archived." }).title).toMatch(/archived/i);
  });

  it("sanitizes technical tokens from messages", () => {
    const msg = sanitizeAdministratorMessage("canApprove false due to stale context checksum");
    expect(msg).not.toMatch(/canApprove/);
    expect(msg).not.toMatch(/stale context/i);
  });

  it("maps version operations to action labels", () => {
    expect(versionActionLabel("Approve")).toBe("Approved");
    expect(versionActionLabel("Generated")).toBe("Created");
    expect(versionActionLabel("Replay")).toBe("Replayed");
  });
});

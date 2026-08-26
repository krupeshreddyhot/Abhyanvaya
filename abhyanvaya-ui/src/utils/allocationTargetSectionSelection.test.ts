import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  MSG_NO_ELIGIBLE_SECTIONS,
  MSG_SELECT_AT_LEAST_ONE_SECTION,
  MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS,
  allocationScopeKey,
  canContinueWithTargetSections,
  filterOccupancyToContextSections,
  formatSelectedTargetSectionsLabel,
  selectedTargetSectionCount,
  targetSectionMode,
  toggleExplicitSectionId,
} from "./allocationTargetSectionSelection";

const panel = readFileSync(
  resolve(__dirname, "../components/allocation/AllocationCapacityPanel.tsx"),
  "utf8",
);
const workspace = readFileSync(
  resolve(__dirname, "../components/allocation/EnterpriseAllocationWorkspace.tsx"),
  "utf8",
);
const sectionService = readFileSync(resolve(__dirname, "../services/sectionService.ts"), "utf8");

describe("AI29.1D.24B.2 Prompt 6 — target section final gate", () => {
  it("1. context.sections = 0 → cannot continue (even when targetSectionIds is null)", () => {
    expect(canContinueWithTargetSections(null, 0)).toBe(false);
    expect(canContinueWithTargetSections([], 0)).toBe(false);
    expect(MSG_NO_ELIGIBLE_SECTIONS).toBe(
      "No eligible Sections are available for the selected academic scope.",
    );
    expect(panel).toContain("MSG_NO_ELIGIBLE_SECTIONS");
    expect(workspace).toContain("canContinueWithTargetSections(targetSectionIds, context?.sections?.length ?? 0)");
  });

  it("2. context.sections > 0 + targetSectionIds = null → can continue", () => {
    expect(targetSectionMode(null)).toBe("all");
    expect(canContinueWithTargetSections(null, 3)).toBe(true);
    expect(selectedTargetSectionCount(null)).toBe(0);
  });

  it("3–4. explicit selection: zero cannot continue; one can continue", () => {
    expect(targetSectionMode([])).toBe("explicit");
    expect(canContinueWithTargetSections([], 3)).toBe(false);
    expect(MSG_SELECT_AT_LEAST_ONE_SECTION).toBe("Select at least one Section to continue.");

    let ids = toggleExplicitSectionId([], 11, true);
    expect(ids).toEqual([11]);
    expect(canContinueWithTargetSections(ids, 3)).toBe(true);
    expect(formatSelectedTargetSectionsLabel(1)).toBe("Selected: 1 section");

    ids = toggleExplicitSectionId(ids, 12, true);
    expect(ids).toEqual([11, 12]);
    expect(formatSelectedTargetSectionsLabel(2)).toBe("Selected: 2 sections");

    ids = toggleExplicitSectionId(ids, 11, false);
    expect(ids).toEqual([12]);
  });

  it("5. scope change → targetSectionIds cleared (workspace scopeKey)", () => {
    const a = allocationScopeKey({
      academicYearId: 1,
      programId: 1,
      courseId: 10,
      groupId: 2,
      semesterId: 3,
    });
    const groupChange = allocationScopeKey({
      academicYearId: 1,
      programId: 1,
      courseId: 10,
      groupId: 1,
      semesterId: 3,
    });
    expect(a).not.toBe(groupChange);
    expect(workspace).toContain("allocationScopeKey");
    expect(workspace).toContain("setTargetSectionIds(null)");
  });

  it("6. context load failure → cannot continue", () => {
    expect(MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS).toBe("Unable to load eligible Sections.");
    expect(workspace).toContain("eligibleSectionsError");
    expect(workspace).toContain("setContext(null)");
    expect(workspace).toContain("setEligibleSectionsError(true)");
    expect(panel).toContain("MSG_UNABLE_TO_LOAD_ELIGIBLE_SECTIONS");
    expect(panel).toContain("Retry");
    // Fail-closed: never unfiltered occupancy catalog
    expect(panel).toContain("filterOccupancyToContextSections");
    expect(panel).not.toContain("targetIds.size > 0 ? all.filter((r) => targetIds.has(r.sectionId)) : all");
  });

  it("7. unauthorized targetSectionIds → server rejects (10A contract preserved)", () => {
    expect(sectionService).toContain("sectionIds");
    expect(workspace).toContain("targetSectionIds");
    // UI must not invent eligibility; server validator remains authority.
    expect(panel).not.toMatch(/Computer Applications/);
  });

  it("Panel UX: radios, Selected count, accessibility", () => {
    expect(panel).toContain("All eligible sections");
    expect(panel).toContain("Explicit selection");
    expect(panel).toContain("formatSelectedTargetSectionsLabel");
    expect(panel).toContain("RadioGroup");
    expect(panel).toMatch(/aria-label/);
    expect(filterOccupancyToContextSections([{ sectionId: 1 }, { sectionId: 99 }], new Set([1])).map((r) => r.sectionId)).toEqual([
      1,
    ]);
    expect(filterOccupancyToContextSections([{ sectionId: 1 }], new Set())).toEqual([]);
  });
});

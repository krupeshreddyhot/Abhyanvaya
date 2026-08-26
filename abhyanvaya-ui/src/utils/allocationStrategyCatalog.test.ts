import { describe, expect, it } from "vitest";
import {
  buildSelectedAllocationRulesSummary,
  buildSelectedCriteriaExplanations,
  COMBINED_STRATEGY_PRESET,
  filterGroupingOptionsByServer,
  GROUPING_STRATEGY_OPTIONS,
} from "./allocationStrategyCatalog";

describe("allocationStrategyCatalog", () => {
  it("filters grouping options to server modes", () => {
    const filtered = filterGroupingOptionsByServer(["StudentNumber", "LastThreeDigits", "Alphabetical"]);
    expect(filtered.map((f) => f.code)).toEqual(["StudentNumber", "LastThreeDigits", "Alphabetical"]);
  });

  it("exposes required human-facing strategies", () => {
    const labels = GROUPING_STRATEGY_OPTIONS.map((g) => g.label);
    expect(labels).toContain("Student Number");
    expect(labels).toContain("Student Number (Last 3 Digits)");
    expect(labels).toContain("Alphabetical Order");
    expect(labels).toContain("Gender Balance");
    expect(labels).toContain("Merit");
    expect(COMBINED_STRATEGY_PRESET.label).toMatch(/Balanced|Combined/i);
  });

  it("builds administrator summary without engine jargon", () => {
    const summary = buildSelectedAllocationRulesSummary({
      groupingMode: "Gender",
      enabledStrategies: { Gender: true, Scoring: true, Capacity: true, Language: true },
      constraintPriorities: { GenderBalance: "Preferred", Capacity: "Mandatory" },
      combinedPresetActive: false,
    });
    expect(summary.primaryRule).toBe("Gender Balance");
    expect(summary.additionalRules).toContain("Gender Balance");
    expect(summary.additionalRules).toContain("Language");
    expect(summary.additionalRules).not.toContain("Scoring");
    expect(summary.sectionCapacityRequired).toBe(true);
  });

  it("builds human-readable explanations without inventing scores", () => {
    const lines = buildSelectedCriteriaExplanations({
      groupingMode: "Gender",
      enabledStrategies: { Gender: true, Scoring: true, Capacity: true },
      constraintPriorities: { GenderBalance: "Preferred", Capacity: "Mandatory" },
      combinedPresetActive: true,
    });
    expect(lines.some((l) => l.includes("Gender Balance"))).toBe(true);
    expect(lines.some((l) => l.includes("Required"))).toBe(true);
    expect(lines.join(" ")).not.toMatch(/Pipeline enabled/i);
    expect(lines.join(" ")).not.toMatch(/totalScore\s*=/);
  });
});

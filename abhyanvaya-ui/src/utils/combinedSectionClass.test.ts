import { describe, expect, it } from "vitest";
import { buildCombinedSectionClassView, formatOperationalClassLabel } from "./combinedSectionClass";

describe("Prompt 13 — Combined Section UI", () => {
  it("formats single Section A", () => {
    expect(formatOperationalClassLabel(["A"])).toBe("A");
    const view = buildCombinedSectionClassView({ sectionIds: [11], sectionCodes: ["A"] });
    expect(view.isCombined).toBe(false);
    expect(view.displayTitle).toBe("Section A");
  });

  it("formats combined Section A + B as one operational class", () => {
    const view = buildCombinedSectionClassView({
      sectionIds: [11, 12],
      sectionCodes: ["A", "B"],
      isCombinedClass: true,
    });
    expect(view.operationalLabel).toBe("A + B");
    expect(view.isCombined).toBe(true);
    expect(view.displayTitle).toContain("Combined class");
    expect(view.subtitle).toContain("underlying Section identity");
  });

  it("formats combined Section A + B + C", () => {
    expect(formatOperationalClassLabel(["A", "B", "C"])).toBe("A + B + C");
    const view = buildCombinedSectionClassView({
      sectionIds: [1, 2, 3],
      sectionCodes: ["A", "B", "C"],
    });
    expect(view.isCombined).toBe(true);
    expect(view.operationalLabel).toBe("A + B + C");
  });

  it("prefers server operationalClassLabel from TimetableSections contract", () => {
    const view = buildCombinedSectionClassView({
      operationalClassLabel: "A + B",
      sectionIds: [11, 12],
      sectionCodes: ["A", "B"],
    });
    expect(view.operationalLabel).toBe("A + B");
  });

  it("empty sections → no operational class chrome", () => {
    const view = buildCombinedSectionClassView({ sectionIds: [], sectionCodes: [] });
    expect(view.displayTitle).toBeNull();
    expect(view.isCombined).toBe(false);
  });
});

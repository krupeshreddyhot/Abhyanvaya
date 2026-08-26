import { describe, expect, it } from "vitest";
import {
  extractCapacityViolations,
  hasMandatoryCapacityViolation,
  proposedOverCapacitySections,
} from "./allocationCapacityViolations";

describe("allocationCapacityViolations", () => {
  it("extracts unsatisfied Capacity/ReservedSeats with priority labels", () => {
    const v = extractCapacityViolations([
      { constraintCode: "Capacity", priority: "Mandatory", satisfied: false, summary: "Over capacity: A" },
      { constraintCode: "GenderBalance", priority: "Preferred", satisfied: false, summary: "spread" },
      { constraintCode: "ReservedSeats", priority: "1", satisfied: false, summary: "Reserved seats violated." },
      { constraintCode: "Capacity", priority: "Mandatory", satisfied: true, summary: "ok" },
    ]);
    expect(v).toHaveLength(2);
    expect(v[0].isMandatory).toBe(true);
    expect(v[1].isPreferred).toBe(true);
    expect(v[1].priority).toBe("Preferred");
  });

  it("detects mandatory capacity blocks", () => {
    expect(
      hasMandatoryCapacityViolation([
        { constraintCode: "Capacity", priority: "Mandatory", satisfied: false, summary: "x" },
      ]),
    ).toBe(true);
    expect(
      hasMandatoryCapacityViolation([
        { constraintCode: "Capacity", priority: "Preferred", satisfied: false, summary: "x" },
      ]),
    ).toBe(false);
  });

  it("lists proposed over-capacity from engine summaries only", () => {
    const rows = proposedOverCapacitySections([
      { sectionId: 1, sectionCode: "A", assignedCount: 40, maximumCapacity: 30, occupancyPercent: 133 },
      { sectionId: 2, sectionCode: "B", assignedCount: 10, maximumCapacity: 30 },
    ]);
    expect(rows).toEqual([{ sectionCode: "A", assignedCount: 40, maximumCapacity: 30, occupancyPercent: 133 }]);
  });

  it("accepts numeric enum priority from System.Text.Json (Mandatory=0)", () => {
    const v = extractCapacityViolations([
      { constraintCode: "Capacity", priority: 0, satisfied: false, summary: "Over capacity: A" },
      { constraintCode: "ReservedSeats", priority: 1, satisfied: false, summary: "Reserved seats violated." },
    ]);
    expect(v).toHaveLength(2);
    expect(v[0].isMandatory).toBe(true);
    expect(v[0].priority).toBe("Mandatory");
    expect(v[1].isPreferred).toBe(true);
    expect(v[1].priority).toBe("Preferred");
  });
});

import { describe, expect, it } from "vitest";
import { safeMultiSelectValues, safePeriodValue, safeSelectValue } from "./safeSelectValue";

describe("safeSelectValue — faculty attendance cold start", () => {
  it("keeps value when present in options", () => {
    expect(safeSelectValue(3, [{ id: 1 }, { id: 3 }])).toBe(3);
  });

  it("falls back when options empty (persisted id before load)", () => {
    expect(safeSelectValue(5, [])).toBe(0);
  });

  it("falls back when id missing from options", () => {
    expect(safeSelectValue(9, [{ id: 1 }, { id: 2 }])).toBe(0);
  });

  it("filters multi-select to known options only", () => {
    expect(safeMultiSelectValues([1, 9, 2], [{ id: 1 }, { id: 2 }])).toEqual([1, 2]);
  });

  it("keeps period when listed", () => {
    expect(safePeriodValue(3, [{ value: 1 }, { value: 3 }])).toBe(3);
  });

  it("falls back period when not listed", () => {
    expect(safePeriodValue(99, [{ value: 1 }, { value: 2 }])).toBe(1);
  });
});

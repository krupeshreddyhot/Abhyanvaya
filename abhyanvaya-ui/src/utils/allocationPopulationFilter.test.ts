import { describe, expect, it } from "vitest";
import {
  applyPopulationFilter,
  compareStudentNumbers,
  countPopulationFilter,
  countUnassignedMatches,
  DEFAULT_POPULATION_FILTER,
  distinctFacetValues,
  isStudentNumberInRange,
  takePopulationFilter,
  tryParseLastThreeDigitsBound,
  validateLastThreeDigitsRange,
  validateStudentNumberRange,
  type AllocationContextStudent,
} from "./allocationPopulationFilter";

const students: AllocationContextStudent[] = [
  { studentId: 1, studentNumber: "A10", studentName: "Ada", gender: "Female", language: "Telugu" },
  { studentId: 2, studentNumber: "A2", studentName: "Bob", gender: "Male", language: "Hindi" },
  { studentId: 3, studentNumber: "B01", studentName: "Cara", gender: "Female", language: "Telugu" },
  { studentId: 4, studentNumber: "10", studentName: "Dan", gender: "Male", language: null },
];

describe("compareStudentNumbers", () => {
  it("uses ordinal ignore-case, not numeric magnitude", () => {
    // "A10" < "A2" numerically-as-string? Ordinal: "A10" < "A2" because '1' < '2'
    expect(compareStudentNumbers("A10", "A2")).toBeLessThan(0);
    expect(compareStudentNumbers("a2", "A2")).toBe(0);
    expect(compareStudentNumbers("10", "B01")).toBeLessThan(0);
  });
});

describe("validateStudentNumberRange", () => {
  it("requires both ends", () => {
    expect(validateStudentNumberRange("", "Z").ok).toBe(false);
    expect(validateStudentNumberRange("A", "").ok).toBe(false);
  });

  it("rejects From > To under alphanumeric ordinal rules", () => {
    const r = validateStudentNumberRange("B01", "A10");
    expect(r.ok).toBe(false);
  });

  it("accepts From <= To", () => {
    expect(validateStudentNumberRange("A10", "B01").ok).toBe(true);
    expect(validateStudentNumberRange("A2", "A2").ok).toBe(true);
  });
});

describe("isStudentNumberInRange", () => {
  it("includes endpoints", () => {
    expect(isStudentNumberInRange("A10", "A10", "B01")).toBe(true);
    expect(isStudentNumberInRange("B01", "A10", "B01")).toBe(true);
  });

  it("excludes outside", () => {
    expect(isStudentNumberInRange("C99", "A10", "B01")).toBe(false);
  });
});

describe("applyPopulationFilter", () => {
  it("returns the same array reference for All (no clone of thousands of rows)", () => {
    const view = applyPopulationFilter(students, DEFAULT_POPULATION_FILTER);
    expect(view).toHaveLength(4);
    expect(view).toBe(students);
  });

  it("counts and windows matches without materializing the full filtered set", () => {
    const filter = {
      mode: "Gender" as const,
      fromStudentNumber: "",
      toStudentNumber: "",
      facetValue: "Female",
    };
    expect(countPopulationFilter(students, filter)).toBe(2);
    expect(takePopulationFilter(students, filter, 1).map((s) => s.studentId)).toEqual([1]);
    expect(countUnassignedMatches(students, DEFAULT_POPULATION_FILTER)).toBe(4);
  });

  it("filters by student number range", () => {
    const matched = applyPopulationFilter(students, {
      mode: "StudentNumberRange",
      fromStudentNumber: "A10",
      toStudentNumber: "A2",
      facetValue: "",
    });
    expect(matched.map((s) => s.studentNumber)).toEqual(["A10", "A2"]);
  });

  it("filters by gender facet", () => {
    const matched = applyPopulationFilter(students, {
      mode: "Gender",
      fromStudentNumber: "",
      toStudentNumber: "",
      facetValue: "Female",
    });
    expect(matched.map((s) => s.studentId)).toEqual([1, 3]);
  });

  it("filters by last 3 digits range 046–050 without full-number ordinal compare", () => {
    const rollStudents: AllocationContextStudent[] = [
      { studentId: 1, studentNumber: "105325405046" },
      { studentId: 2, studentNumber: "105325405047" },
      { studentId: 3, studentNumber: "105325405050" },
      { studentId: 4, studentNumber: "105325405051" },
      { studentId: 5, studentNumber: "105325405001" },
    ];
    const matched = applyPopulationFilter(rollStudents, {
      mode: "LastThreeDigitsRange",
      fromStudentNumber: "046",
      toStudentNumber: "050",
      facetValue: "",
    });
    expect(matched.map((s) => s.studentId)).toEqual([1, 2, 3]);
  });

  it("normalizes last 3 digits 1–5 to 001–005", () => {
    expect(validateLastThreeDigitsRange("1", "5").ok).toBe(true);
    expect(tryParseLastThreeDigitsBound("46")).toEqual({ ok: true, value: 46, normalized: "046" });
    expect(validateLastThreeDigitsRange("050", "046").ok).toBe(false);
    expect(validateLastThreeDigitsRange("", "050").ok).toBe(false);
    expect(validateLastThreeDigitsRange("12A", "050").ok).toBe(false);
  });

  it("returns empty when facet has no context values", () => {
    const matched = applyPopulationFilter(students, {
      mode: "Hostel",
      fromStudentNumber: "",
      toStudentNumber: "",
      facetValue: "Block-A",
    });
    expect(matched).toEqual([]);
  });
});

describe("distinctFacetValues", () => {
  it("lists unique non-empty values from context only", () => {
    expect(distinctFacetValues(students, "Language")).toEqual(["Hindi", "Telugu"]);
    expect(distinctFacetValues(students, "Hostel")).toEqual([]);
  });
});

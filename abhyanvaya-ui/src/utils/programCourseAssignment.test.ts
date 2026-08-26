import { describe, expect, it } from "vitest";
import {
  countCoursesForProgram,
  coursesAvailableForProgramAssignment,
} from "./programCourseAssignment";

const courses = [
  { id: 1, code: "BCOM", name: "B.Com", programId: 10 },
  { id: 2, code: "BSC", name: "B.Sc", programId: null },
  { id: 3, code: "BBA", name: "BBA", programId: 20 },
];

describe("programCourseAssignment — Prompt 6/7", () => {
  it("counts only Course.ProgramId matches", () => {
    expect(countCoursesForProgram(courses, 10)).toBe(1);
    expect(countCoursesForProgram(courses, 99)).toBe(0);
  });

  it("available list excludes courses already on the Program", () => {
    const avail = coursesAvailableForProgramAssignment(courses, 10);
    expect(avail.map((c) => c.code)).toEqual(["BBA", "BSC"]);
  });

  it("null ProgramId courses are available for assignment", () => {
    const avail = coursesAvailableForProgramAssignment(courses, 10);
    expect(avail.some((c) => c.code === "BSC")).toBe(true);
  });
});

import { describe, expect, it } from "vitest";
import type { ProgramDto } from "../services/programService";
import {
  isAssignableProgram,
  programsForCourseAssignmentSelector,
} from "./courseProgramAssignment";

const base = (over: Partial<ProgramDto> & Pick<ProgramDto, "id" | "programName" | "status" | "isActive">): ProgramDto => ({
  collegeId: 1,
  programCode: "P",
  displayOrder: 0,
  courseCount: 0,
  studentCount: 0,
  facultyCount: 0,
  ...over,
});

describe("courseProgramAssignment — Course Master Program selector", () => {
  it("hides archived programs for new assignment", () => {
    const rows = [
      base({ id: 1, programName: "Commerce", status: "Active", isActive: true }),
      base({ id: 2, programName: "Old", status: "Archived", isActive: false }),
    ];
    expect(programsForCourseAssignmentSelector(rows, null).map((p) => p.id)).toEqual([1]);
    expect(isAssignableProgram(rows[1]!)).toBe(false);
  });

  it("keeps current inactive assignment visible when editing", () => {
    const rows = [
      base({ id: 1, programName: "Commerce", status: "Active", isActive: true }),
      base({ id: 3, programName: "Paused", status: "Inactive", isActive: false }),
    ];
    expect(programsForCourseAssignmentSelector(rows, 3).map((p) => p.id)).toEqual([1, 3]);
  });
});

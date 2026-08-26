import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { shouldConfirmProgramReassignment } from "./programReassignmentConfirmation";

const coursesPage = readFileSync(resolve(__dirname, "../pages/setup/CoursesPage.tsx"), "utf8");
const programsPage = readFileSync(resolve(__dirname, "../pages/setup/ProgramsPage.tsx"), "utf8");

describe("AI29.1D.24A Prompt 3 — Course Master integration", () => {
  it("uses decision helper before persist", () => {
    expect(coursesPage).toContain("shouldConfirmProgramReassignment");
    expect(coursesPage).toContain("persistCourse");
    expect(coursesPage).toContain("cancelProgramChange");
    expect(coursesPage).toContain("setProgramId(initialProgramId)");
  });

  it("does not call assign-course from Course Master", () => {
    expect(coursesPage).not.toContain("assignCourseToProgram");
    expect(coursesPage).toContain("callAssignCourseSeparately");
  });

  it("confirm path calls persistCourse; cancel restores selection (source contract)", () => {
    expect(coursesPage).toContain("void persistCourse()");
    expect(coursesPage).toContain("setProgramId(initialProgramId)");
    expect(coursesPage).toContain("if (saving || confirmProgramChangeOpen) return");
    expect(coursesPage).toContain("cancelProgramChange");
  });

  it("Commerce → Science requires confirmation for existing course", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: 10,
        requestedProgramId: 20,
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(true);
  });
});

describe("AI29.1D.24A Prompt 4 — Program Master integration", () => {
  it("gates doAssign with decision helper and uses existing assign API", () => {
    expect(programsPage).toContain("shouldConfirmProgramReassignment");
    expect(programsPage).toContain("performAssign");
    expect(programsPage).toContain("assignCourseToProgram");
    expect(programsPage).toContain("reassignConfirmOpen");
    expect(programsPage).toContain("buildProgramReassignmentCopy");
    expect(programsPage).toContain("reassignmentCopy.title");
  });

  it("cancel reassign does not call API (source contract)", () => {
    expect(programsPage).toContain("cancelReassign");
    expect(programsPage).toMatch(/const cancelReassign = \(\) => \{[\s\S]*setReassignConfirmOpen\(false\)/);
  });

  it("B.Com Commerce → Science requires confirmation", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: 10, // Commerce
        requestedProgramId: 20, // Science
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(true);
  });

  it("first assign None → Science does not confirm", () => {
    expect(
      shouldConfirmProgramReassignment({
        currentProgramId: null,
        requestedProgramId: 20,
        isExistingCourse: true,
        programsEnabled: true,
      }),
    ).toBe(false);
  });
});

describe("AI29.1D.24A Prompt 5 — UX hardening", () => {
  it("reuses AcademicConfirmDialog and avoids window.confirm for reassignment", () => {
    expect(coursesPage).toContain("AcademicConfirmDialog");
    expect(programsPage).toContain("AcademicConfirmDialog");
    // Reassignment path must not use window.confirm (archive/delete may still).
    const assignSection = programsPage.slice(
      programsPage.indexOf("performAssign"),
      programsPage.indexOf("doUnassign"),
    );
    expect(assignSection).not.toContain("window.confirm");
  });
});

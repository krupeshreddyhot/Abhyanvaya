import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { buildCourseMasterSavePlan } from "../../utils/courseMasterPersistence";

const repoRoot = join(__dirname, "..", "..", "..");
const pagePath = join(repoRoot, "src", "pages", "setup", "CoursesPage.tsx");

describe("AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 2 — Course Department UI", () => {
  const page = readFileSync(pagePath, "utf8");

  it("CoursesPage requires Department selector", () => {
    expect(page).toContain('label="Department"');
    expect(page).toContain("listDepartments");
    expect(page).toContain("departmentId");
  });

  it("Program selector remains EnablePrograms-gated", () => {
    expect(page).toContain("{enablePrograms ? (");
    expect(page).toContain('label="Program"');
  });

  it("save plan always includes departmentId", () => {
    const plan = buildCourseMasterSavePlan({
      editingId: 0,
      code: "BCOM",
      name: "B.Com",
      departmentId: 3,
      programId: 0,
      enablePrograms: false,
    });
    expect(plan.coursePayload.departmentId).toBe(3);
    expect(plan.coursePayload.programId).toBeUndefined();
  });

  it("save plan includes optional program when enabled", () => {
    const plan = buildCourseMasterSavePlan({
      editingId: 1,
      code: "BCOM",
      name: "B.Com",
      departmentId: 3,
      programId: 9,
      enablePrograms: true,
    });
    expect(plan.coursePayload.departmentId).toBe(3);
    expect(plan.coursePayload.programId).toBe(9);
  });
});

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const repoRoot = join(__dirname, "..", "..", "..", "..");
const pagePath = join(repoRoot, "abhyanvaya-ui", "src", "pages", "setup", "ProgramsPage.tsx");
const servicePath = join(repoRoot, "abhyanvaya-ui", "src", "services", "programService.ts");

describe("AI-SCHED-CATALOG/TIMETABLE P1-2 — Program Department UI", () => {
  const page = readFileSync(pagePath, "utf8");
  const service = readFileSync(servicePath, "utf8");

  it("create/edit payload includes departmentId", () => {
    expect(service).toContain("departmentId: number");
    expect(service).toContain("/programs/department-options");
    expect(page).toContain("departmentId");
    expect(page).toContain("listProgramDepartmentOptions");
  });

  it("Department selector is present on Program dialog", () => {
    expect(page).toContain('label="Department"');
    expect(page).toContain("Department is required.");
  });

  it("does not add Program selection to Course screens (P1-3 out of scope)", () => {
    expect(page).not.toMatch(/CoursePage|CoursesPage/);
    // Programs page may mention Course assignment (existing) but must not introduce Course.Department.
    expect(page).not.toContain("course.departmentId");
  });

  it("Programs-disabled messaging keeps Program optional", () => {
    expect(page).toContain("without Program selection");
    expect(page).toContain("disabled={!enablePrograms}");
  });
});

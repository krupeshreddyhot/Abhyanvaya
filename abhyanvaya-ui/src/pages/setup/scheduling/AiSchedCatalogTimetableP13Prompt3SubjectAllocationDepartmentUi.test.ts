import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const pagePath = join(__dirname, "SubjectAllocationPage.tsx");

describe("AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 3 — SA Department UI", () => {
  const page = readFileSync(pagePath, "utf8");

  it("syncs departmentId from selected Course", () => {
    expect(page).toContain("course.departmentId");
    expect(page).toContain('form.setValue("departmentId", course.departmentId)');
  });

  it("filters dialog courses by department", () => {
    expect(page).toContain("dialogCourses");
    expect(page).toContain("c.departmentId === watchedDepartmentId");
  });

  it("does not invent Teaching Group inference", () => {
    expect(page).not.toContain("inferTeachingGroup");
    expect(page).not.toContain("autoCreateTeachingGroup");
  });
});

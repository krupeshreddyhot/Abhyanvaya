import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const dialogPath = join(__dirname, "TimetableEntryDialog.tsx");

describe("AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 4 — TT Entry Department UI", () => {
  const page = readFileSync(dialogPath, "utf8");

  it("labels Department as filter, not ownership authority", () => {
    expect(page).toContain("Department (filter)");
  });

  it("syncs department display from selected SubjectAllocation", () => {
    expect(page).toContain("setDepartmentId(alloc.departmentId)");
  });

  it("does not send departmentId in create/update payload", () => {
    // Create/update use allocationId + day/slot/room — Department is server-derived.
    expect(page).not.toMatch(/createTimetableEntry\([^)]*departmentId/);
    expect(page).not.toMatch(/updateTimetableEntry\([^)]*departmentId/);
  });
});

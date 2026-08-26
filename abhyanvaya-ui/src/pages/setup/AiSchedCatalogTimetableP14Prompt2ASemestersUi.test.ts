import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const pagePath = join(__dirname, "SemestersPage.tsx");

describe("AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 2A — Semesters UI", () => {
  const page = readFileSync(pagePath, "utf8");

  it("requires Group for create/save", () => {
    expect(page).toContain("Group is required");
    expect(page).toContain("disabled={saving || !groupId}");
  });

  it("labels legacy historical rows", () => {
    expect(page).toContain("Legacy / Historical");
  });

  it("does not offer None / whole-course for new writes", () => {
    expect(page).not.toContain("— None —");
    expect(page.toLowerCase()).not.toContain("applies to the whole course");
    expect(page).not.toContain("Legacy / Course-wide");
  });
});

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const root = resolve(__dirname, "../../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");
const repoDocs = (...parts: string[]) =>
  readFileSync(resolve(root, "..", "..", "docs", ...parts), "utf8");

/**
 * AI-SCHED-TG.6 Prompt 4 / Prompt 1 — Discovery guards.
 * Asserts current architecture facts for the upcoming TG selector work.
 * Does not implement the selector.
 */
describe("AI-SCHED-TG.6 Prompt 4 Discovery — timetable entry UX architecture", () => {
  it("registers designer route under scheduling timetables/:id", () => {
    const routes = read("routes", "AppRoutes.tsx");
    expect(routes).toContain('path="setup/scheduling/timetables/:id"');
    expect(routes).toContain("TimetableDesignerPage");
    expect(routes).toContain("SchedulingTimetableView");
    expect(routes).toContain("SchedulingTimetableManage");
  });

  it("entry dialog owns SubjectAllocation cascade, ordinary create/update, and TG selector", () => {
    const dialog = read("pages", "setup", "scheduling", "timetable", "TimetableEntryDialog.tsx");
    expect(dialog).toContain("Subject allocation");
    expect(dialog).toContain("createTimetableEntry");
    expect(dialog).toContain("updateTimetableEntry");
    expect(dialog).toContain("Teaching Group");
    expect(dialog).toContain("applyTeachingGroupSelectionDelta");
    expect(dialog).toContain("reloadCompatibleTeachingGroups");
  });

  it("Prompt 2 client already exposes TG assign/clear and response teachingGroupId", () => {
    const service = read("services", "schedulingService.ts");
    expect(service).toContain("assignTeachingGroupToTimetableEntry");
    expect(service).toContain("clearTeachingGroupFromTimetableEntry");
    expect(service).toMatch(/teachingGroupId\?:\s*number\s*\|\s*null/);

    const createBlock = (() => {
      const start = service.indexOf("export type CreateTimetableEntryRequest");
      const brace = service.indexOf("{", start);
      const end = service.indexOf("};", brace);
      return service.slice(brace, end);
    })();
    expect(createBlock).not.toContain("teachingGroupId");
  });

  it("designer refreshes via getTimetableGrid / local upsert; no TimetableSection writes", () => {
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    expect(designer).toContain("getTimetableGrid");
    expect(designer).toContain("upsertEntryLocal");
    expect(designer).toContain("TimetableEntryDialog");
    expect(designer).toContain("createTimetableEntry");
    expect(designer).toContain("moveTimetableEntry");
    expect(designer).toContain("bulkTimetableEntries");
    expect(designer).not.toContain("setTimetableSections");
    expect(designer).not.toContain("assignTeachingGroupToTimetableEntry");
    expect(designer).not.toContain("/attendance");
  });

  it("grid surfaces Teaching Group state via formatEntryTeachingGroupLine (Prompt 4 / Prompt 4)", () => {
    const utils = read("pages", "setup", "scheduling", "timetable", "timetableUtils.ts");
    expect(utils).toContain("formatEntryCompact");
    expect(utils).toContain("formatEntryTeachingGroupLine");
    expect(utils).toContain("Teaching Group: None");
    const grid = read("pages", "setup", "scheduling", "timetable", "TimetableGrid.tsx");
    expect(grid).toContain("formatEntryTeachingGroupLine");
    expect(grid).not.toContain("assignTeachingGroupToTimetableEntry");
  });

  it("discovery document exists and marks discovery-only", () => {
    const doc = repoDocs("AI_SCHED_TG_6_PROMPT_4_DISCOVERY.md");
    expect(doc).toContain("DISCOVERY ONLY");
    expect(doc).toContain("TimetableEntryDialog");
    expect(doc).toContain("Recommended insertion point");
    expect(doc).toContain("no production behavior changed");
  });
});

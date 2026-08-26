import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const root = resolve(__dirname, "../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");

describe("AI-SCHED-TG.6 Prompt 2 — UI client architecture guards", () => {
  it("extends teachingGroupService without a parallel client", () => {
    const service = read("services", "teachingGroupService.ts");
    expect(service).toContain("resolved-members");
    expect(service).toContain("getResolvedTeachingGroupMembers");
    expect(service).toContain("addTeachingGroupMembers");
    expect(service).toContain("replaceTeachingGroupMemberships");
    expect(service).toContain("removeTeachingGroupMember");
    expect(service).toContain("import api from \"../api/axios\"");
    expect(service).not.toContain("createIfMissing");
    expect(service).not.toContain("autoCreate");
    expect(service).not.toContain("inferTeachingGroup");
    // No client-side resolver implementation (transport-only comments are allowed).
    expect(service).not.toMatch(/function\s+resolveMembership|computeResolved|base\.union|includes\.filter/i);
  });

  it("timetable client has assign/clear TG and response teachingGroupId only", () => {
    const service = read("services", "schedulingService.ts");
    expect(service).toContain("assignTeachingGroupToTimetableEntry");
    expect(service).toContain("clearTeachingGroupFromTimetableEntry");
    expect(service).toContain("/teaching-group");
    expect(service).toMatch(/teachingGroupId\?:\s*number\s*\|\s*null/);

    // Request type property lists must not include teachingGroupId.
    const propLines = (typeName: string) => {
      const start = service.indexOf(`export type ${typeName}`);
      const brace = service.indexOf("{", start);
      const end = service.indexOf("};", brace);
      return service.slice(brace, end);
    };
    expect(propLines("CreateTimetableEntryRequest")).not.toContain("teachingGroupId");
    expect(propLines("UpdateTimetableEntryRequest")).not.toContain("teachingGroupId");
    expect(propLines("UpsertTimetableEntryRequest")).not.toContain("teachingGroupId");
  });

  it("timetable TG selector uses dedicated assign/clear actions (TG.6 Prompt 3+)", () => {
    // Prompt 2 forbade UI wiring; Prompt 3/4 delivered dialog + actions.
    const dialog = read("pages", "setup", "scheduling", "timetable", "TimetableEntryDialog.tsx");
    const actions = read(
      "pages",
      "setup",
      "scheduling",
      "timetable",
      "timetableTeachingGroupAssignmentActions.ts",
    );
    expect(dialog).toContain("applyTeachingGroupSelectionDelta");
    expect(dialog).toContain("Teaching Group");
    expect(actions).toContain("assignTeachingGroupToTimetableEntry");
    expect(actions).toContain("clearTeachingGroupFromTimetableEntry");
    expect(actions).toContain("listCompatibleTeachingGroupsForTimetableEntry");
    // Create payload path must still omit teachingGroupId (wired via dedicated APIs only).
    expect(dialog).toContain("buildPayload");
  });

  it("forbids client TimetableSection writes and Attendance mutation from TG service", () => {
    const service = read("services", "teachingGroupService.ts");
    expect(service).not.toContain("TimetableSection");
    expect(service).not.toContain("/attendance");
    expect(service).not.toContain("StudentSection");
    expect(service).not.toContain("setTimetableSections");
  });
});

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { PermissionKeys } from "../../../auth/permissionKeys";

const root = resolve(__dirname, "../../..");

const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");

describe("AI-SCHED-TG.5 Prompt 3 — Teaching Group UI architecture guards", () => {
  it("registers dedicated Teaching Group permission keys", () => {
    expect(PermissionKeys.SchedulingTeachingGroupView).toBe("Scheduling.TeachingGroup.View");
    expect(PermissionKeys.SchedulingTeachingGroupManage).toBe("Scheduling.TeachingGroup.Manage");
  });

  it("routes Teaching Groups under scheduling catalog path with TG permissions", () => {
    const routes = read("routes", "AppRoutes.tsx");
    expect(routes).toContain("setup/scheduling/teaching-groups");
    expect(routes).toContain("TeachingGroupsPage");
    expect(routes).toContain("SchedulingTeachingGroupView");
    expect(routes).toContain("SchedulingTeachingGroupManage");
    // P1-1: UI view gate aligned with API AddSchedulingViewPolicy (also Scheduling.View/Manage).
    const block = routes.slice(
      routes.indexOf('path="setup/scheduling/teaching-groups"'),
      routes.indexOf('path="setup/scheduling/teaching-groups"') + 900,
    );
    expect(block).toContain("SchedulingView");
    expect(block).toContain("SchedulingManage");
  });

  it("adds Teaching Groups catalog card near Subject Allocation", () => {
    const catalog = read("pages", "setup", "scheduling", "schedulingCatalogConfig.tsx");
    expect(catalog).toContain('key: "teaching-groups"');
    expect(catalog).toContain("/setup/scheduling/teaching-groups");
    expect(catalog).toContain("Teaching Groups");
  });

  it("API client uses Prompt 2 Teaching Group endpoints only", () => {
    const service = read("services", "teachingGroupService.ts");
    expect(service).toContain('/scheduling/teaching-groups');
    expect(service).toContain("/archive");
    expect(service).toContain("/memberships");
    expect(service).toContain("/sections");
    expect(service).not.toMatch(/timetable\/\$\{.*\}\/sections/);
    expect(service).not.toContain("createIfMissing");
    expect(service).not.toContain("autoCreate");
  });

  it("Teaching Groups page does not mutate TimetableSection; membership uses approved service", () => {
    const page = read("pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
    expect(page).toContain("listTeachingGroups");
    expect(page).toContain("createTeachingGroup");
    expect(page).toContain("archiveTeachingGroup");
    expect(page).toContain("addTeachingGroupSection");
    expect(page).toContain("removeTeachingGroupSection");
    expect(page).toContain("getTeachingGroupMemberships");
    expect(page).toContain("TeachingGroupMembershipPanel");
    expect(page).toContain("getResolvedTeachingGroupMembers");
    expect(page).not.toContain("setTimetableSections");
    expect(page).not.toContain("/timetable/");
    expect(page).not.toContain("shouldAutoCreate");
  });

  it("page load path does not call createTeachingGroup without explicit action", () => {
    const page = read("pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
    // createTeachingGroup only appears in submitCreate / imports — not in useEffect bodies as auto-create.
    const effects = page.split("useEffect");
    for (const block of effects.slice(1)) {
      const body = block.slice(0, 800);
      expect(body).not.toContain("createTeachingGroup(");
    }
  });
});

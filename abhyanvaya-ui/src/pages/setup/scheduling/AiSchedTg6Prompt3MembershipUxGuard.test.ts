import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { PermissionKeys } from "../../../auth/permissionKeys";
import {
  isMutableMembershipSource,
  shouldCalculateResolvedMembershipInUi,
} from "./teachingGroupMembershipUi";
import { TeachingGroupMembershipSource } from "../../../services/teachingGroupService";

const root = resolve(__dirname, "../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");

describe("AI-SCHED-TG.6 Prompt 3 — membership management UX guards", () => {
  it("1–2. view/manage permission keys remain registered", () => {
    expect(PermissionKeys.SchedulingTeachingGroupView).toBe("Scheduling.TeachingGroup.View");
    expect(PermissionKeys.SchedulingTeachingGroupManage).toBe("Scheduling.TeachingGroup.Manage");
  });

  it("page reuses Teaching Groups route and membership panel (no duplicate page)", () => {
    const routes = read("routes", "AppRoutes.tsx");
    expect(routes).toContain("setup/scheduling/teaching-groups");
    expect(routes).toContain("TeachingGroupsPage");
    expect(routes).toContain("<TeachingGroupsPage />");
    expect((routes.match(/path="setup\/scheduling\/teaching-groups"/g) ?? []).length).toBe(1);

    const page = read("pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
    expect(page).toContain("TeachingGroupMembershipPanel");
    expect(page).not.toContain("Membership management is not yet available");
  });

  it("1. view-only users cannot mutate — panel gates on canManage", () => {
    const panel = read("pages", "setup", "scheduling", "TeachingGroupMembershipPanel.tsx");
    expect(panel).toContain("canManage");
    expect(panel).toContain("View-only");
    expect(panel).toContain("canMutate");
  });

  it("2–3. manage path exposes search/add for mutable sources", () => {
    const panel = read("pages", "setup", "scheduling", "TeachingGroupMembershipPanel.tsx");
    expect(panel).toContain("Search students");
    expect(panel).toContain("Add selected");
    expect(panel).toContain("Included students");
    expect(isMutableMembershipSource(TeachingGroupMembershipSource.ExplicitStudents)).toBe(true);
  });

  it("4–5. resolved roster from API; no client resolver", () => {
    const page = read("pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
    expect(page).toContain("getResolvedTeachingGroupMembers");
    expect(shouldCalculateResolvedMembershipInUi()).toBe(false);
    const actions = read("pages", "setup", "scheduling", "teachingGroupMembershipActions.ts");
    expect(actions).toContain("getResolvedTeachingGroupMembers");
    expect(actions).not.toMatch(/Base\s*\+|StudentSection|computeResolved/i);
  });

  it("6–8. add/remove use Prompt 2 service methods and reload", () => {
    const actions = read("pages", "setup", "scheduling", "teachingGroupMembershipActions.ts");
    expect(actions).toContain("addTeachingGroupMembers");
    expect(actions).toContain("removeTeachingGroupMember");
    expect(actions).toContain("reloadTeachingGroupMembershipState");
    expect(actions).not.toContain("replaceTeachingGroupMemberships(");
  });

  it("9–11. 409 conflict message and no auto-retry", () => {
    const actions = read("pages", "setup", "scheduling", "teachingGroupMembershipActions.ts");
    expect(actions).toContain("409");
    expect(actions).toContain("MEMBERSHIP_CONFLICT_MESSAGE");
    expect(actions).not.toMatch(/forceOverwrite|silentlyMerge|autoRetry\(/i);
    expect(actions).not.toContain("while (true)");
  });

  it("13–14 / 17–19. empty/loading/section isolation and no TimetableSection writes", () => {
    const panel = read("pages", "setup", "scheduling", "TeachingGroupMembershipPanel.tsx");
    expect(panel).toContain("Loading membership");
    expect(panel).toContain("No resolved members");
    expect(panel).toContain("mutating");
    expect(panel).not.toContain("setTimetableSections");
    expect(panel).not.toContain("/attendance");
    expect(panel).not.toContain("listStudentSections");
    expect(panel).not.toContain("replaceTeachingGroupSections");
    expect(panel).not.toContain("addTeachingGroupSection");

    const page = read("pages", "setup", "scheduling", "TeachingGroupsPage.tsx");
    expect(page).not.toContain("inferTeachingGroup");
    expect(page).not.toContain("createIfMissing");
  });

  it("21–24. Hybrid overlays editable; derived/resolved read-only tables present", () => {
    const panel = read("pages", "setup", "scheduling", "TeachingGroupMembershipPanel.tsx");
    expect(panel).toContain("Excluded students");
    expect(panel).toContain("Derived students");
    expect(panel).toContain("Resolved students (server, read-only)");
    expect(panel).toContain("aria-label=\"Resolved students read-only\"");
  });

  it("timetable designer untouched by membership prompt", () => {
    const dialog = read("pages", "setup", "scheduling", "timetable", "TimetableEntryDialog.tsx");
    expect(dialog).not.toContain("TeachingGroupMembershipPanel");
    expect(dialog).not.toContain("addTeachingGroupMembers");
  });
});

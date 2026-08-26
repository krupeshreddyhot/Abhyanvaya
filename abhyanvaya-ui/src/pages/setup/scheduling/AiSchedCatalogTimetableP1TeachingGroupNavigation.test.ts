import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { schedulingHubGroups } from "./schedulingCatalogConfig";

const root = resolve(__dirname, "../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");

/** Canonical Teaching Groups management route (unchanged). */
export const TEACHING_GROUPS_CANONICAL_PATH = "/setup/scheduling/teaching-groups";

describe("AI-SCHED-CATALOG/TIMETABLE P1-1 — Teaching Groups navigation", () => {
  it("catalog card exists under Faculty & Allocation with canonical Teaching Groups path", () => {
    const faculty = schedulingHubGroups.find((g) => g.id === "faculty-planning");
    expect(faculty).toBeTruthy();
    const card = faculty!.items.find((i) => i.key === "teaching-groups");
    expect(card).toBeTruthy();
    expect(card!.title).toBe("Teaching Groups");
    expect(card!.to).toBe(TEACHING_GROUPS_CANONICAL_PATH);
    expect(card!.to).not.toBe("/dashboard");
    expect(card!.to).not.toBe("/setup/scheduling/dashboard");
  });

  it("AppRoutes registers canonical path to TeachingGroupsPage (not Dashboard)", () => {
    const routes = read("routes", "AppRoutes.tsx");
    expect(routes).toContain(`path="setup/scheduling/teaching-groups"`);
    expect(routes).toContain("<TeachingGroupsPage />");

    const routeBlock = routes.slice(
      routes.indexOf('path="setup/scheduling/teaching-groups"'),
      routes.indexOf('path="setup/scheduling/teaching-groups"') + 900,
    );
    expect(routeBlock).toContain("TeachingGroupsPage");
    expect(routeBlock).not.toContain("SchedulingDashboardPage");
    expect(routeBlock).not.toContain('Navigate to="/dashboard"');
  });

  it("Teaching Groups route guard matches API view policy (TG keys + Scheduling.View/Manage)", () => {
    const routes = read("routes", "AppRoutes.tsx");
    const routeBlock = routes.slice(
      routes.indexOf('path="setup/scheduling/teaching-groups"'),
      routes.indexOf('path="setup/scheduling/teaching-groups"') + 900,
    );
    expect(routeBlock).toContain("SchedulingTeachingGroupView");
    expect(routeBlock).toContain("SchedulingTeachingGroupManage");
    expect(routeBlock).toContain("SchedulingView");
    expect(routeBlock).toContain("SchedulingManage");
    expect(PermissionKeys.SchedulingTeachingGroupView).toBe("Scheduling.TeachingGroup.View");
    expect(PermissionKeys.SchedulingView).toBe("Scheduling.View");
  });

  it("ProtectedRoute still redirects unauthorized users to Dashboard (guard preserved)", () => {
    const guard = read("routes", "ProtectedRoute.tsx");
    expect(guard).toContain('Navigate to="/dashboard"');
    expect(guard).toContain("hasAnyPermission");
  });

  it("SchedulingHub links cards via RouterLink to config.to (no hardcoded Dashboard for modules)", () => {
    const hub = read("pages", "setup", "scheduling", "SchedulingHub.tsx");
    expect(hub).toContain("component={RouterLink}");
    expect(hub).toContain("to={x.to}");
    expect(hub).toContain("schedulingHubGroups");
    // Dashboard is a separate explicit card only.
    expect(hub).toContain("schedulingDashboardLink.to");
  });

  it("does not introduce a second Teaching Groups page or route", () => {
    const routes = read("routes", "AppRoutes.tsx");
    const matches = routes.match(/path="setup\/scheduling\/teaching-groups"/g) ?? [];
    expect(matches.length).toBe(1);
    expect(routes.match(/TeachingGroupsPage/g)?.length ?? 0).toBeGreaterThanOrEqual(1);
  });
});

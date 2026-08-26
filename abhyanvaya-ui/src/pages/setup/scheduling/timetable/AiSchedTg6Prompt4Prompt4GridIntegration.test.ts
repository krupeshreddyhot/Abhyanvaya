import { beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";

vi.mock("../../../../api/axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

import api from "../../../../api/axios";
import { TeachingGroupStatus } from "../../../../services/teachingGroupService";
import type { CompatibleTeachingGroupOptionDto, TimetableEntryDto } from "../../../../services/schedulingService";
import {
  entryTeachingGroupCapacityWarning,
  formatEntryTeachingGroupLine,
  type TeachingGroupGridHint,
} from "./timetableUtils";
import {
  hintFromCompatibleOption,
  mergeTeachingGroupHintsFromOptions,
} from "./timetableTeachingGroupGridHints";
import { TIMETABLE_TG_CONFLICT_MESSAGE } from "./timetableTeachingGroupAssignmentActions";
import {
  shouldFilterTeachingGroupsClientSideForCompatibility,
  shouldInferTeachingGroupFromSubjectAllocation,
} from "./timetableTeachingGroupSelectorContract";
import { shouldAutoCreateTeachingGroupFromSubjectAllocation } from "../teachingGroupUi";

const mockedApi = api as unknown as { get: ReturnType<typeof vi.fn> };

const root = resolve(__dirname, "../../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");

const entry = (overrides: Partial<TimetableEntryDto> = {}): TimetableEntryDto =>
  ({
    id: 1,
    timetableId: 1,
    dayOfWeek: 1,
    timeSlotId: 2,
    subjectAllocationId: 10,
    teachingGroupId: null,
    staffId: 1,
    roomId: 1,
    departmentId: 1,
    courseId: 1,
    groupId: 2,
    semesterId: 3,
    subjectId: 17,
    subjectName: "B.Com Financial Accounting",
    ...overrides,
  }) as TimetableEntryDto;

describe("AI-SCHED-TG.6 Prompt 4 Prompt 4 — grid TG state", () => {
  it("distinguishes None / Assigned / Archived", () => {
    expect(formatEntryTeachingGroupLine(entry())).toBe("Teaching Group: None");

    const active: TeachingGroupGridHint = {
      id: 7,
      name: "Lecture A",
      code: "TG-A",
      status: TeachingGroupStatus.Active,
    };
    expect(formatEntryTeachingGroupLine(entry({ teachingGroupId: 7 }), active)).toBe(
      "Teaching Group: TG-A — Lecture A",
    );

    const archived: TeachingGroupGridHint = {
      id: 7,
      name: "Lecture A",
      code: "TG-A",
      status: TeachingGroupStatus.Archived,
    };
    expect(formatEntryTeachingGroupLine(entry({ teachingGroupId: 7 }), archived)).toContain("Archived");
    expect(formatEntryTeachingGroupLine(entry({ teachingGroupId: 7 }))).toBe("Teaching Group: #7");
  });

  it("capacity warning prefers server soft warnings (no client capacity math)", () => {
    expect(
      entryTeachingGroupCapacityWarning({
        id: 1,
        name: "A",
        status: TeachingGroupStatus.Active,
        resolvedStudentCount: 65,
        maxTeachingCapacity: 60,
      }),
    ).toBeNull();

    expect(
      entryTeachingGroupCapacityWarning(null, [
        {
          code: "TEACHING_GROUP_CAPACITY_EXCEEDED",
          severity: "Error",
          message: "Teaching Group capacity exceeded.",
          title: "Teaching Group capacity exceeded",
          entryId: 1,
          staffId: null,
          roomId: null,
          dayOfWeek: null,
          timeSlotId: null,
          dismissed: false,
        },
      ]),
    ).toBe("Teaching Group capacity exceeded");
  });

  it("merges compatible options into display hints without inventing rows", () => {
    const options: CompatibleTeachingGroupOptionDto[] = [
      {
        id: 3,
        code: "L1",
        name: "Lab 1",
        type: 1,
        status: TeachingGroupStatus.Active,
        resolvedStudentCount: 20,
        maxTeachingCapacity: 30,
        isAssignedToEntry: true,
      },
    ];
    const map = mergeTeachingGroupHintsFromOptions(new Map(), options);
    expect(map.get(3)).toEqual(hintFromCompatibleOption(options[0]));
  });

  it("409 conflict message matches Prompt 4 wording", () => {
    expect(TIMETABLE_TG_CONFLICT_MESSAGE).toContain("changed by another user");
    expect(TIMETABLE_TG_CONFLICT_MESSAGE).toContain("latest Teaching Group assignment has been loaded");
  });
});

describe("AI-SCHED-TG.6 Prompt 4 Prompt 4 — architecture guards", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("forbidden: SA→TG inference, TimetableSection UI writes, Attendance, auto TG create", () => {
    expect(shouldInferTeachingGroupFromSubjectAllocation()).toBe(false);
    expect(shouldFilterTeachingGroupsClientSideForCompatibility()).toBe(false);
    expect(shouldAutoCreateTeachingGroupFromSubjectAllocation()).toBe(false);

    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    const grid = read("pages", "setup", "scheduling", "timetable", "TimetableGrid.tsx");
    const utils = read("pages", "setup", "scheduling", "timetable", "timetableUtils.ts");
    const hints = read("pages", "setup", "scheduling", "timetable", "timetableTeachingGroupGridHints.ts");

    expect(designer).not.toContain("setTimetableSections");
    expect(designer).not.toContain("/attendance");
    expect(designer).not.toContain("createTeachingGroup");
    expect(designer).not.toContain("assignTeachingGroupToTimetableEntry");
    expect(grid).toContain("formatEntryTeachingGroupLine");
    expect(grid).toContain("teachingGroupHints");
    expect(utils).toContain("Teaching Group: None");
    expect(hints).toContain("getTeachingGroup");
    expect(hints).not.toContain("listSubjectAllocations");
    expect(hints).not.toContain("shouldInferTeachingGroupFromSubjectAllocation");
    expect(hints).not.toContain("listTeachingGroups(");
  });

  it("required: compatible query + dedicated assign/clear; Create/Update/Upsert omit TG", () => {
    const dialog = read("pages", "setup", "scheduling", "timetable", "TimetableEntryDialog.tsx");
    const actions = read(
      "pages",
      "setup",
      "scheduling",
      "timetable",
      "timetableTeachingGroupAssignmentActions.ts",
    );
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    const service = read("services", "schedulingService.ts");

    expect(actions).toContain("listCompatibleTeachingGroupsForTimetableEntry");
    expect(actions).toContain("assignTeachingGroupToTimetableEntry");
    expect(actions).toContain("clearTeachingGroupFromTimetableEntry");
    expect(actions).not.toMatch(/while\s*\(true\)|autoRetry|forceOverwrite/i);
    expect(dialog).toContain("onTeachingGroupConflict");
    expect(designer).toContain("onTeachingGroupConflict");
    expect(designer).toContain("refreshGrid");
    expect(designer).toContain("teachingGroupHints");

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

  it("drag/drop and paste create payloads omit teachingGroupId", () => {
    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    expect(designer).toContain("createTimetableEntry");
    expect(designer).toContain("bulkTimetableEntries");
    // No client-side TG assignment on DnD/paste
    expect(designer).not.toMatch(/createTimetableEntry\([^)]*teachingGroupId/s);
    expect(designer).not.toMatch(/bulkTimetableEntries\([\s\S]*teachingGroupId/);
  });

  it("acceptance: grid informational only; dialog remains editor", () => {
    const grid = read("pages", "setup", "scheduling", "timetable", "TimetableGrid.tsx");
    expect(grid).not.toContain("assignTeachingGroupToTimetableEntry");
    expect(grid).not.toContain("listCompatibleTeachingGroups");
    expect(grid).toContain("aria-label");
    expect(grid).toContain("role=\"status\"");

    const dialog = read("pages", "setup", "scheduling", "timetable", "TimetableEntryDialog.tsx");
    expect(dialog).toContain('label="Teaching Group"');
    expect(dialog).toContain("applyTeachingGroupSelectionDelta");
  });
});

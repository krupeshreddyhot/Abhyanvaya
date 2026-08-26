import { beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import axios from "axios";

vi.mock("../../../../api/axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

import api from "../../../../api/axios";
import {
  applyTeachingGroupSelectionDelta,
  reloadCompatibleTeachingGroups,
  TIMETABLE_TG_CONFLICT_MESSAGE,
} from "./timetableTeachingGroupAssignmentActions";
import {
  shouldFilterTeachingGroupsClientSideForCompatibility,
  shouldInferTeachingGroupFromSubjectAllocation,
  shouldSilentlyAssignOrClearTeachingGroup,
} from "./timetableTeachingGroupSelectorContract";
import {
  formatTeachingGroupSelectorOptionLabel,
  isResolvedOverMaxTeachingCapacity,
  shouldAutoCreateTeachingGroupFromSubjectAllocation,
} from "../teachingGroupUi";
import { TeachingGroupStatus, TeachingGroupType } from "../../../../services/teachingGroupService";
import type { TimetableEntryDto } from "../../../../services/schedulingService";

const mockedApi = api as unknown as {
  get: ReturnType<typeof vi.fn>;
  put: ReturnType<typeof vi.fn>;
  delete: ReturnType<typeof vi.fn>;
};

const root = resolve(__dirname, "../../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");

const entry = (overrides: Partial<TimetableEntryDto> = {}): TimetableEntryDto => ({
  id: 55,
  timetableId: 1,
  dayOfWeek: 1,
  timeSlotId: 2,
  timeSlotName: null,
  startTime: null,
  endTime: null,
  subjectAllocationId: 10,
  teachingGroupId: null,
  staffId: 1,
  staffName: null,
  roomId: 1,
  roomName: null,
  departmentId: 1,
  departmentName: null,
  courseId: 1,
  courseName: null,
  groupId: 2,
  groupName: null,
  semesterId: 3,
  semesterName: null,
  subjectId: 17,
  subjectName: null,
  remarks: null,
  ...overrides,
});

const axiosError = (status: number, data?: unknown) => {
  const err = new axios.AxiosError("fail");
  err.response = {
    status,
    data,
    statusText: "",
    headers: {},
    config: { headers: new axios.AxiosHeaders() },
  };
  return err;
};

describe("AI-SCHED-TG.6 Prompt 4 Prompt 3 — TG selector UX", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("architecture guards: no client filter / inference / silent assign", () => {
    expect(shouldFilterTeachingGroupsClientSideForCompatibility()).toBe(false);
    expect(shouldInferTeachingGroupFromSubjectAllocation()).toBe(false);
    expect(shouldSilentlyAssignOrClearTeachingGroup()).toBe(false);
    expect(shouldAutoCreateTeachingGroupFromSubjectAllocation()).toBe(false);
  });

  it("capacity warning helper does not use room capacity", () => {
    expect(isResolvedOverMaxTeachingCapacity(42, 40)).toBe(true);
    expect(isResolvedOverMaxTeachingCapacity(40, 40)).toBe(false);
    expect(isResolvedOverMaxTeachingCapacity(42, null)).toBe(false);
  });

  it("formats selector labels including archived assigned cue", () => {
    const label = formatTeachingGroupSelectorOptionLabel({
      id: 1,
      code: "TG-A",
      name: "Finance Lecture",
      type: TeachingGroupType.Custom,
      status: TeachingGroupStatus.Archived,
      resolvedStudentCount: 42,
      expectedStudentCount: 45,
      maxTeachingCapacity: 60,
      isAssignedToEntry: true,
    });
    expect(label).toContain("TG-A — Finance Lecture");
    expect(label).toContain("Students: 42");
    expect(label).toContain("Archived — currently assigned");
  });

  it("assign uses dedicated PUT; unchanged selection skips mutation", async () => {
    mockedApi.put.mockResolvedValue({ data: entry({ teachingGroupId: 7 }) });
    const changed = await applyTeachingGroupSelectionDelta(entry(), 7, null);
    expect(mockedApi.put).toHaveBeenCalledWith(
      "/scheduling/timetables/entries/55/teaching-group",
      { teachingGroupId: 7 },
    );
    expect(changed.kind).toBe("success");

    mockedApi.put.mockClear();
    const same = await applyTeachingGroupSelectionDelta(entry({ teachingGroupId: 7 }), 7, 7);
    expect(mockedApi.put).not.toHaveBeenCalled();
    expect(same.kind).toBe("unchanged");
  });

  it("clear uses dedicated DELETE", async () => {
    mockedApi.delete.mockResolvedValue({ data: entry({ teachingGroupId: null }) });
    const outcome = await applyTeachingGroupSelectionDelta(entry({ teachingGroupId: 7 }), "", 7);
    expect(mockedApi.delete).toHaveBeenCalledWith(
      "/scheduling/timetables/entries/55/teaching-group",
    );
    expect(outcome.kind).toBe("success");
  });

  it("409 reloads compatible options and does not auto-retry", async () => {
    mockedApi.put.mockRejectedValueOnce(axiosError(409, "conflict"));
    mockedApi.get.mockResolvedValueOnce({
      data: [{ id: 9, name: "Other", type: 7, status: 2, resolvedStudentCount: 1, isAssignedToEntry: true }],
    });
    const outcome = await applyTeachingGroupSelectionDelta(entry(), 7, null);
    expect(mockedApi.put).toHaveBeenCalledTimes(1);
    expect(outcome.kind).toBe("conflict");
    if (outcome.kind === "conflict") {
      expect(outcome.message).toBe(TIMETABLE_TG_CONFLICT_MESSAGE);
      expect(outcome.entry.teachingGroupId).toBe(9);
      expect(outcome.options).toHaveLength(1);
    }
  });

  it("403 surfaces safe permission message", async () => {
    mockedApi.put.mockRejectedValueOnce(axiosError(403));
    const outcome = await applyTeachingGroupSelectionDelta(entry(), 7, null);
    expect(outcome.kind).toBe("error");
    if (outcome.kind === "error") {
      expect(outcome.status).toBe(403);
      expect(outcome.message).toMatch(/not authorized|Manage|permission/i);
    }
  });

  it("reloadCompatibleTeachingGroups uses entry-scoped GET", async () => {
    mockedApi.get.mockResolvedValueOnce({
      data: [
        {
          id: 3,
          name: "A",
          type: 7,
          status: 2,
          resolvedStudentCount: 0,
          isAssignedToEntry: false,
        },
      ],
    });
    const reloaded = await reloadCompatibleTeachingGroups(55, entry({ teachingGroupId: 3 }));
    expect(mockedApi.get).toHaveBeenCalledWith(
      "/scheduling/timetables/entries/55/compatible-teaching-groups",
    );
    expect(reloaded.options[0].id).toBe(3);
  });
});

describe("AI-SCHED-TG.6 Prompt 4 Prompt 3 — architecture guards", () => {
  it("Guard 1–4: dialog + actions use dedicated APIs; Create/Update/Upsert omit TG", () => {
    const dialog = read("pages", "setup", "scheduling", "timetable", "TimetableEntryDialog.tsx");
    expect(dialog).toContain("Teaching Group");
    expect(dialog).toContain("Subject allocation");
    expect(dialog.indexOf("Subject allocation")).toBeLessThan(dialog.indexOf('label="Teaching Group"'));
    expect(dialog).toContain("applyTeachingGroupSelectionDelta");
    expect(dialog).toContain("reloadCompatibleTeachingGroups");
    expect(dialog).toContain("No compatible Teaching Groups are available");
    expect(dialog).toContain("readOnly");
    expect(dialog).not.toContain("setTimetableSections");
    expect(dialog).not.toContain("createTeachingGroup");
    expect(dialog).not.toContain("listTeachingGroups(");

    const actions = read(
      "pages",
      "setup",
      "scheduling",
      "timetable",
      "timetableTeachingGroupAssignmentActions.ts",
    );
    expect(actions).toContain("listCompatibleTeachingGroupsForTimetableEntry");
    expect(actions).toContain("assignTeachingGroupToTimetableEntry");
    expect(actions).toContain("clearTeachingGroupFromTimetableEntry");
    expect(actions).not.toMatch(/while\s*\(true\)|autoRetry|forceOverwrite/i);

    const service = read("services", "schedulingService.ts");
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

  it("Guard 5–10: no TimetableSection writes / auto TG / SA inference / silent clear", () => {
    const dialog = read("pages", "setup", "scheduling", "timetable", "TimetableEntryDialog.tsx");
    expect(dialog).not.toContain("TimetableSection");
    expect(dialog).not.toContain("/attendance");
    expect(dialog).toContain("Archived");
    expect(dialog).toContain("explicitly clear");
    expect(dialog).toContain("createTimetableEntry(timetableId, payload)");
    // create payload built without teachingGroupId
    expect(dialog).toContain("buildPayload");
    const buildStart = dialog.indexOf("const buildPayload");
    const buildEnd = dialog.indexOf("};", dialog.indexOf("return {", buildStart)) + 2;
    const buildBody = dialog.slice(buildStart, buildEnd);
    expect(buildBody).not.toContain("teachingGroupId");

    const designer = read("pages", "setup", "scheduling", "timetable", "TimetableDesignerPage.tsx");
    expect(designer).not.toContain("setTimetableSections");
    expect(designer).not.toContain("assignTeachingGroupToTimetableEntry");
  });
});

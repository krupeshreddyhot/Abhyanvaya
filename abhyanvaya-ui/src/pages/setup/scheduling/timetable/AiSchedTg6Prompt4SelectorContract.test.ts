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
import {
  assignTeachingGroupToTimetableEntry,
  clearTeachingGroupFromTimetableEntry,
  listCompatibleTeachingGroupsForTimetableEntry,
  type CompatibleTeachingGroupOptionDto,
  type CreateTimetableEntryRequest,
  type UpdateTimetableEntryRequest,
  type UpsertTimetableEntryRequest,
} from "../../../../services/schedulingService";
import {
  assignTeachingGroupPath,
  clearTeachingGroupPath,
  compatibleTeachingGroupsPath,
  shouldFilterTeachingGroupsClientSideForCompatibility,
  shouldInferTeachingGroupFromSubjectAllocation,
  shouldSilentlyAssignOrClearTeachingGroup,
} from "./timetableTeachingGroupSelectorContract";

const mockedApi = api as unknown as {
  get: ReturnType<typeof vi.fn>;
  put: ReturnType<typeof vi.fn>;
  delete: ReturnType<typeof vi.fn>;
};

const root = resolve(__dirname, "../../../..");
const read = (...parts: string[]) => readFileSync(resolve(root, ...parts), "utf8");

describe("AI-SCHED-TG.6 Prompt 4 Prompt 2 — Teaching Group selector contract", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedApi.get.mockResolvedValue({ data: [] });
    mockedApi.put.mockResolvedValue({ data: {} });
    mockedApi.delete.mockResolvedValue({ data: {} });
  });

  it("1. selector API is entry-scoped", async () => {
    expect(compatibleTeachingGroupsPath(55)).toBe(
      "/scheduling/timetables/entries/55/compatible-teaching-groups",
    );
    await listCompatibleTeachingGroupsForTimetableEntry(55);
    expect(mockedApi.get).toHaveBeenCalledWith(
      "/scheduling/timetables/entries/55/compatible-teaching-groups",
    );
  });

  it("2. UI does not perform client-side TG compatibility filtering", () => {
    expect(shouldFilterTeachingGroupsClientSideForCompatibility()).toBe(false);
    expect(shouldInferTeachingGroupFromSubjectAllocation()).toBe(false);
    expect(shouldSilentlyAssignOrClearTeachingGroup()).toBe(false);

    const contract = read(
      "pages",
      "setup",
      "scheduling",
      "timetable",
      "timetableTeachingGroupSelectorContract.ts",
    );
    expect(contract).not.toMatch(/filter\(|\.filter\s*\(/);
    expect(contract).not.toContain("listTeachingGroups");
  });

  it("3. Create/Update/Upsert DTOs do not accept TeachingGroupId", () => {
    const create: CreateTimetableEntryRequest = {
      dayOfWeek: 1,
      timeSlotId: 2,
      subjectAllocationId: 3,
    };
    const update: UpdateTimetableEntryRequest = { ...create, id: 9 };
    const upsert: UpsertTimetableEntryRequest = { ...create };
    expect("teachingGroupId" in create).toBe(false);
    expect("teachingGroupId" in update).toBe(false);
    expect("teachingGroupId" in upsert).toBe(false);

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

  it("4. assignment uses dedicated API", async () => {
    expect(assignTeachingGroupPath(12)).toBe("/scheduling/timetables/entries/12/teaching-group");
    await assignTeachingGroupToTimetableEntry(12, { teachingGroupId: 7 });
    expect(mockedApi.put).toHaveBeenCalledWith(
      "/scheduling/timetables/entries/12/teaching-group",
      { teachingGroupId: 7 },
    );
  });

  it("5. clear uses dedicated API", async () => {
    expect(clearTeachingGroupPath(12)).toBe("/scheduling/timetables/entries/12/teaching-group");
    await clearTeachingGroupFromTimetableEntry(12);
    expect(mockedApi.delete).toHaveBeenCalledWith(
      "/scheduling/timetables/entries/12/teaching-group",
    );
  });

  it("6. no TimetableSection API is called by the selector contract", () => {
    const service = read("services", "schedulingService.ts");
    expect(service).toContain("listCompatibleTeachingGroupsForTimetableEntry");
    expect(service).toContain("compatible-teaching-groups");

    const sliceStart = service.indexOf("listCompatibleTeachingGroupsForTimetableEntry");
    const slice = service.slice(sliceStart, sliceStart + 400);
    expect(slice).not.toContain("TimetableSection");
    expect(slice).not.toContain("/attendance");
    expect(slice).not.toContain("StudentSection");

    const contract = read(
      "pages",
      "setup",
      "scheduling",
      "timetable",
      "timetableTeachingGroupSelectorContract.ts",
    );
    expect(contract).not.toContain("setTimetableSections");
    expect(contract).not.toContain("/attendance");
  });

  it("compatible option DTO is selector-shaped (no membership internals)", () => {
    const option: CompatibleTeachingGroupOptionDto = {
      id: 1,
      code: "TG-A",
      name: "Lab A",
      type: 5,
      status: 2,
      resolvedStudentCount: 18,
      expectedStudentCount: 20,
      maxTeachingCapacity: 24,
      isAssignedToEntry: true,
    };
    expect(option.isAssignedToEntry).toBe(true);
    expect(option).not.toHaveProperty("memberships");
    expect(option).not.toHaveProperty("resolvedMembers");
  });

  it("dialog wires selector through assignment actions (Prompt 3)", () => {
    const dialog = read("pages", "setup", "scheduling", "timetable", "TimetableEntryDialog.tsx");
    expect(dialog).toContain("applyTeachingGroupSelectionDelta");
    expect(dialog).toContain("reloadCompatibleTeachingGroups");
  });
});

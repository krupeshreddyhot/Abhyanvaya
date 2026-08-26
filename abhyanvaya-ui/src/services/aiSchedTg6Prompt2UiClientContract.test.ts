import { beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import { getApiErrorMessage, getHttpStatus } from "../utils/apiErrorMessage";

vi.mock("../api/axios", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

import api from "../api/axios";
import {
  addTeachingGroupMembers,
  getResolvedTeachingGroupMembers,
  removeTeachingGroupMember,
  replaceTeachingGroupMemberships,
  type ResolvedTeachingGroupMemberDto,
  type TeachingGroupMembershipMutationResultDto,
} from "./teachingGroupService";
import {
  assignTeachingGroupToTimetableEntry,
  clearTeachingGroupFromTimetableEntry,
  updateTimetableEntry,
  type CreateTimetableEntryRequest,
  type TimetableEntryDto,
  type UpdateTimetableEntryRequest,
  type UpsertTimetableEntryRequest,
} from "./schedulingService";

const mockedApi = api as unknown as {
  get: ReturnType<typeof vi.fn>;
  post: ReturnType<typeof vi.fn>;
  put: ReturnType<typeof vi.fn>;
  delete: ReturnType<typeof vi.fn>;
};

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

describe("AI-SCHED-TG.6 Prompt 2 — UI client contract", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockedApi.get.mockResolvedValue({ data: [] });
    mockedApi.post.mockResolvedValue({ data: {} });
    mockedApi.put.mockResolvedValue({ data: {} });
    mockedApi.delete.mockResolvedValue({ data: {} });
  });

  it("1. resolved-members URL", async () => {
    await getResolvedTeachingGroupMembers(42);
    expect(mockedApi.get).toHaveBeenCalledWith("/scheduling/teaching-groups/42/resolved-members");
  });

  it("2. membership POST URL", async () => {
    await addTeachingGroupMembers(7, { studentIds: [1, 2], effectiveFrom: "2026-01-01" });
    expect(mockedApi.post).toHaveBeenCalledWith("/scheduling/teaching-groups/7/memberships", {
      studentIds: [1, 2],
      effectiveFrom: "2026-01-01",
    });
  });

  it("3. membership PUT URL", async () => {
    await replaceTeachingGroupMemberships(7, {
      includeStudentIds: [1],
      excludeStudentIds: [9],
    });
    expect(mockedApi.put).toHaveBeenCalledWith("/scheduling/teaching-groups/7/memberships", {
      includeStudentIds: [1],
      excludeStudentIds: [9],
    });
  });

  it("4. membership DELETE URL", async () => {
    await removeTeachingGroupMember(7, 99);
    expect(mockedApi.delete).toHaveBeenCalledWith("/scheduling/teaching-groups/7/memberships/99");
  });

  it("5. timetable TG PUT URL", async () => {
    await assignTeachingGroupToTimetableEntry(55, { teachingGroupId: 12 });
    expect(mockedApi.put).toHaveBeenCalledWith(
      "/scheduling/timetables/entries/55/teaching-group",
      { teachingGroupId: 12 },
    );
  });

  it("6. timetable TG DELETE URL", async () => {
    await clearTeachingGroupFromTimetableEntry(55);
    expect(mockedApi.delete).toHaveBeenCalledWith(
      "/scheduling/timetables/entries/55/teaching-group",
    );
  });

  it("7. TeachingGroupId response mapping is typed on TimetableEntryDto", () => {
    const withTg: TimetableEntryDto = {
      id: 1,
      timetableId: 2,
      dayOfWeek: 1,
      timeSlotId: 3,
      timeSlotName: null,
      startTime: null,
      endTime: null,
      subjectAllocationId: 4,
      teachingGroupId: 12,
      staffId: 5,
      staffName: null,
      roomId: 6,
      roomName: null,
      departmentId: 7,
      departmentName: null,
      courseId: 8,
      courseName: null,
      groupId: 9,
      groupName: null,
      semesterId: 10,
      semesterName: null,
      subjectId: 11,
      subjectName: null,
      remarks: null,
    };
    expect(withTg.teachingGroupId).toBe(12);
  });

  it("8. null TeachingGroupId remains valid", () => {
    const legacy: TimetableEntryDto = {
      id: 1,
      timetableId: 2,
      dayOfWeek: 1,
      timeSlotId: 3,
      timeSlotName: null,
      startTime: null,
      endTime: null,
      subjectAllocationId: 4,
      teachingGroupId: null,
      staffId: 5,
      staffName: null,
      roomId: 6,
      roomName: null,
      departmentId: 7,
      departmentName: null,
      courseId: 8,
      courseName: null,
      groupId: 9,
      groupName: null,
      semesterId: 10,
      semesterName: null,
      subjectId: 11,
      subjectName: null,
      remarks: null,
    };
    expect(legacy.teachingGroupId).toBeNull();

    const omitted: TimetableEntryDto = {
      id: 1,
      timetableId: 2,
      dayOfWeek: 1,
      timeSlotId: 3,
      timeSlotName: null,
      startTime: null,
      endTime: null,
      subjectAllocationId: 4,
      staffId: 5,
      staffName: null,
      roomId: 6,
      roomName: null,
      departmentId: 7,
      departmentName: null,
      courseId: 8,
      courseName: null,
      groupId: 9,
      groupName: null,
      semesterId: 10,
      semesterName: null,
      subjectId: 11,
      subjectName: null,
      remarks: null,
    };
    expect(omitted.teachingGroupId).toBeUndefined();
  });

  it("9. ordinary timetable update does not implicitly clear TeachingGroupId", async () => {
    const payload: UpdateTimetableEntryRequest = {
      id: 55,
      dayOfWeek: 1,
      timeSlotId: 3,
      subjectAllocationId: 4,
      roomId: 6,
      remarks: "edit",
    };
    expect("teachingGroupId" in payload).toBe(false);

    const createPayload: CreateTimetableEntryRequest = {
      dayOfWeek: 1,
      timeSlotId: 3,
      subjectAllocationId: 4,
    };
    expect("teachingGroupId" in createPayload).toBe(false);

    const upsert: UpsertTimetableEntryRequest = {
      dayOfWeek: 1,
      timeSlotId: 3,
      subjectAllocationId: 4,
    };
    expect("teachingGroupId" in upsert).toBe(false);

    await updateTimetableEntry(55, payload);
    const sent = mockedApi.put.mock.calls[0][1] as Record<string, unknown>;
    expect(sent).not.toHaveProperty("teachingGroupId");
    expect(mockedApi.put.mock.calls[0][0]).toBe("/scheduling/timetables/entries/55");
  });

  it("10. 409 is passed through existing error handling", () => {
    const conflict = axiosError(409, "Membership was modified by another user.");
    expect(getHttpStatus(conflict)).toBe(409);
    expect(getApiErrorMessage(conflict, "Request failed.")).toBe(
      "Membership was modified by another user.",
    );
  });

  it("resolved-members response is transport-only (preserves order + provenance)", async () => {
    const roster: ResolvedTeachingGroupMemberDto[] = [
      { studentId: 10, provenance: 1 },
      { studentId: 20, provenance: 2 },
    ];
    mockedApi.get.mockResolvedValueOnce({ data: roster });
    const res = await getResolvedTeachingGroupMembers(1);
    expect(res.data).toEqual(roster);
    expect(res.data.map((r) => r.studentId)).toEqual([10, 20]);
  });

  it("mutation result DTO matches backend contract shape", () => {
    const result: TeachingGroupMembershipMutationResultDto = {
      teachingGroupId: 7,
      resolvedStudentCount: 2,
      memberships: [],
      resolvedMembers: [{ studentId: 1, provenance: 1 }],
    };
    expect(result.resolvedStudentCount).toBe(2);
    expect(result.resolvedMembers[0].provenance).toBe(1);
  });
});

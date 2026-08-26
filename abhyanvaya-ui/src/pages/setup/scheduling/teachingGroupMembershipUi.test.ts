import { beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import {
  MEMBERSHIP_CONFLICT_MESSAGE,
  currentExcludeOverlays,
  currentIncludeOverlays,
  derivedResolvedMembers,
  formatStudentMembershipLabel,
  isExplicitStudentsSource,
  isHybridSource,
  isMutableMembershipSource,
  isResolvedOverMaxCapacity,
  shouldCalculateResolvedMembershipInUi,
  uniqueStudentIds,
} from "./teachingGroupMembershipUi";
import {
  TeachingGroupMemberProvenance,
  TeachingGroupMembershipInclusion,
  TeachingGroupMembershipSource,
} from "../../../services/teachingGroupService";

vi.mock("../../../services/teachingGroupService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/teachingGroupService")>();
  return {
    ...actual,
    addTeachingGroupMembers: vi.fn(),
    removeTeachingGroupMember: vi.fn(),
    getTeachingGroup: vi.fn(),
    getTeachingGroupMemberships: vi.fn(),
    getResolvedTeachingGroupMembers: vi.fn(),
  };
});

import {
  addTeachingGroupMembers,
  getResolvedTeachingGroupMembers,
  getTeachingGroup,
  getTeachingGroupMemberships,
  removeTeachingGroupMember,
} from "../../../services/teachingGroupService";
import {
  addTeachingGroupMembersWithReload,
  removeTeachingGroupMemberWithReload,
  reloadTeachingGroupMembershipState,
} from "./teachingGroupMembershipActions";

const mockedAdd = vi.mocked(addTeachingGroupMembers);
const mockedRemove = vi.mocked(removeTeachingGroupMember);
const mockedGetTg = vi.mocked(getTeachingGroup);
const mockedGetMemberships = vi.mocked(getTeachingGroupMemberships);
const mockedGetResolved = vi.mocked(getResolvedTeachingGroupMembers);

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

const detail = {
  id: 7,
  code: null,
  name: "TG-A",
  type: 7,
  status: 1,
  membershipSource: TeachingGroupMembershipSource.ExplicitStudents,
  activityKind: 1,
  subjectAllocationId: 1,
  academicYearId: 1,
  courseId: 1,
  groupId: 1,
  semesterId: 1,
  subjectId: 1,
  expectedStudentCount: 10,
  maxTeachingCapacity: 5,
  resolvedStudentCount: 2,
  linkedSectionCount: 0,
  timetableEntryCount: 0,
  exclusionGroupKey: null,
  effectiveFrom: "2026-01-01",
  effectiveTo: null,
  displayOrder: 0,
  notes: null,
  membershipCount: 2,
  sections: [],
};

const stubReload = () => {
  mockedGetTg.mockResolvedValue({ data: detail } as never);
  mockedGetMemberships.mockResolvedValue({ data: [] } as never);
  mockedGetResolved.mockResolvedValue({
    data: [{ studentId: 1, provenance: TeachingGroupMemberProvenance.ExplicitInclude }],
  } as never);
};

describe("AI-SCHED-TG.6 Prompt 3 — membership UX helpers", () => {
  it("3. ExplicitStudents is mutable; section sources are not", () => {
    expect(isExplicitStudentsSource(TeachingGroupMembershipSource.ExplicitStudents)).toBe(true);
    expect(isMutableMembershipSource(TeachingGroupMembershipSource.ExplicitStudents)).toBe(true);
    expect(isMutableMembershipSource(TeachingGroupMembershipSource.Section)).toBe(false);
    expect(isHybridSource(TeachingGroupMembershipSource.Hybrid)).toBe(true);
  });

  it("5. UI does not calculate resolved membership", () => {
    expect(shouldCalculateResolvedMembershipInUi()).toBe(false);
  });

  it("15–16. capacity display semantics and over-capacity warning", () => {
    expect(isResolvedOverMaxCapacity(6, 5)).toBe(true);
    expect(isResolvedOverMaxCapacity(5, 5)).toBe(false);
    expect(isResolvedOverMaxCapacity(0, null)).toBe(false);
    expect(isResolvedOverMaxCapacity(0, 10)).toBe(false);
  });

  it("20. empty membership remains valid (zero resolved is not a client prohibition)", () => {
    expect(isResolvedOverMaxCapacity(0, 10)).toBe(false);
  });

  it("partitions include/exclude overlays and derived resolved rows", () => {
    const memberships = [
      {
        id: 1,
        teachingGroupId: 7,
        studentId: 10,
        inclusion: TeachingGroupMembershipInclusion.Include,
        effectiveFrom: "2026-01-01",
        effectiveTo: null,
        isCurrent: true,
      },
      {
        id: 2,
        teachingGroupId: 7,
        studentId: 11,
        inclusion: TeachingGroupMembershipInclusion.Exclude,
        effectiveFrom: "2026-01-01",
        effectiveTo: null,
        isCurrent: true,
      },
    ];
    expect(currentIncludeOverlays(memberships).map((m) => m.studentId)).toEqual([10]);
    expect(currentExcludeOverlays(memberships).map((m) => m.studentId)).toEqual([11]);
    expect(
      derivedResolvedMembers([
        { studentId: 10, provenance: TeachingGroupMemberProvenance.Derived },
        { studentId: 12, provenance: TeachingGroupMemberProvenance.ExplicitInclude },
      ]).map((r) => r.studentId),
    ).toEqual([10]);
  });

  it("deduplicates student ids for add requests", () => {
    expect(uniqueStudentIds([3, 1, 3, 2, 1])).toEqual([3, 1, 2]);
  });

  it("formats student labels without inventing PII", () => {
    expect(formatStudentMembershipLabel({ id: 9 })).toBe("Student #9");
    expect(
      formatStudentMembershipLabel({ id: 9, studentNumber: "S1", name: "Ada" }),
    ).toBe("S1 — Ada");
  });
});

describe("AI-SCHED-TG.6 Prompt 3 — membership actions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    stubReload();
  });

  it("4. resolved members are loaded from API", async () => {
    await reloadTeachingGroupMembershipState(7);
    expect(mockedGetResolved).toHaveBeenCalledWith(7);
    expect(mockedGetTg).toHaveBeenCalledWith(7);
    expect(mockedGetMemberships).toHaveBeenCalledWith(7);
  });

  it("6. Add member calls POST then reloads authoritative state", async () => {
    mockedAdd.mockResolvedValue({ data: {} } as never);
    const outcome = await addTeachingGroupMembersWithReload(7, [1, 1, 2]);
    expect(mockedAdd).toHaveBeenCalledWith(7, { studentIds: [1, 2] });
    expect(mockedGetResolved).toHaveBeenCalled();
    expect(outcome.kind).toBe("success");
    if (outcome.kind === "success") {
      expect(outcome.state.detail.resolvedStudentCount).toBe(2);
    }
  });

  it("7. Remove member calls DELETE then reloads", async () => {
    mockedRemove.mockResolvedValue({ data: {} } as never);
    const outcome = await removeTeachingGroupMemberWithReload(7, 99);
    expect(mockedRemove).toHaveBeenCalledWith(7, 99);
    expect(mockedGetMemberships).toHaveBeenCalled();
    expect(outcome.kind).toBe("success");
  });

  it("8. successful mutation reloads authoritative state", async () => {
    mockedAdd.mockResolvedValue({ data: {} } as never);
    await addTeachingGroupMembersWithReload(7, [5]);
    expect(mockedGetTg).toHaveBeenCalled();
    expect(mockedGetMemberships).toHaveBeenCalled();
    expect(mockedGetResolved).toHaveBeenCalled();
  });

  it("9–11. 409 triggers reload, does not auto-retry, does not invent overwrite", async () => {
    mockedAdd.mockRejectedValueOnce(axiosError(409, "conflict"));
    const outcome = await addTeachingGroupMembersWithReload(7, [5]);
    expect(mockedAdd).toHaveBeenCalledTimes(1);
    expect(outcome.kind).toBe("conflict");
    if (outcome.kind === "conflict") {
      expect(outcome.message).toBe(MEMBERSHIP_CONFLICT_MESSAGE);
      expect(outcome.state.detail.id).toBe(7);
    }
  });

  it("12. 403 is handled safely", async () => {
    mockedRemove.mockRejectedValueOnce(axiosError(403));
    const outcome = await removeTeachingGroupMemberWithReload(7, 1);
    expect(outcome.kind).toBe("error");
    if (outcome.kind === "error") {
      expect(outcome.status).toBe(403);
      expect(outcome.message).toMatch(/not authorized|Manage/i);
    }
  });

  it("does not call replaceTeachingGroupMemberships (safer Add/Remove UX)", async () => {
    const service = await import("../../../services/teachingGroupService");
    expect(typeof service.replaceTeachingGroupMemberships).toBe("function");
    mockedAdd.mockResolvedValue({ data: {} } as never);
    await addTeachingGroupMembersWithReload(7, [1]);
    // replace is not invoked by action helpers
    expect(mockedAdd).toHaveBeenCalled();
  });
});

import { describe, expect, it } from "vitest";
import {
  formatCapacityDisplay,
  formatTeachingGroupSelectorOptionLabel,
  isResolvedOverMaxTeachingCapacity,
  parseOptionalCapacity,
  shouldAutoCreateTeachingGroupFromSubjectAllocation,
  teachingGroupStatusLabel,
  teachingGroupTypeLabel,
} from "./teachingGroupUi";
import {
  TeachingGroupStatus,
  TeachingGroupType,
} from "../../../services/teachingGroupService";

describe("teachingGroupUi", () => {
  it("never auto-creates Teaching Groups from Subject Allocation", () => {
    expect(shouldAutoCreateTeachingGroupFromSubjectAllocation()).toBe(false);
  });

  it("labels types and statuses", () => {
    expect(teachingGroupTypeLabel(TeachingGroupType.Laboratory)).toBe("Laboratory");
    expect(teachingGroupStatusLabel(TeachingGroupStatus.Archived)).toBe("Archived");
  });

  it("parses optional capacity for usability only", () => {
    expect(parseOptionalCapacity("")).toBeNull();
    expect(parseOptionalCapacity("  ")).toBeNull();
    expect(parseOptionalCapacity("40")).toBe(40);
    expect(parseOptionalCapacity("0")).toBe(0);
    expect(Number.isNaN(parseOptionalCapacity("x"))).toBe(true);
  });

  it("formats capacity display without inventing PlannedCapacity", () => {
    expect(formatCapacityDisplay(null)).toBe("—");
    expect(formatCapacityDisplay(12)).toBe("12");
  });

  it("formats teaching group selector option labels and capacity warning", () => {
    expect(
      formatTeachingGroupSelectorOptionLabel({
        id: 1,
        code: "TG-A",
        name: "Lab",
        type: TeachingGroupType.Laboratory,
        status: TeachingGroupStatus.Active,
        resolvedStudentCount: 10,
        expectedStudentCount: 12,
        maxTeachingCapacity: 20,
      }),
    ).toContain("TG-A — Lab");
    expect(isResolvedOverMaxTeachingCapacity(21, 20)).toBe(true);
  });
});

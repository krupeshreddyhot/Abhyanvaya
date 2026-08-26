import { describe, expect, it } from "vitest";
import {
  buildFacultySectionAllocationRows,
  formatOperationalClassLabel,
  matchSubjectNamesForFacultySection,
  resolveFacultyAllocationStatus,
} from "./facultySectionAllocationView";

const sections = [
  { id: 11, sectionCode: "A", courseId: 1, groupId: 2, semesterId: 3, academicYearId: 100 },
  { id: 12, sectionCode: "B", courseId: 1, groupId: 2, semesterId: 3, academicYearId: 100 },
  { id: 13, sectionCode: "C", courseId: 1, groupId: 2, semesterId: 3, academicYearId: 100 },
];

const sectionGroupAb = {
  id: 9,
  groupCode: "AB",
  groupName: "Combined AB",
  currentSectionIds: [11, 12],
  academicYearId: 100,
};

describe("Prompt 14 / 15A.8 — Faculty Section Allocation view", () => {
  it("maps Current / Ended / Inactive allocation status", () => {
    expect(resolveFacultyAllocationStatus({ isCurrent: true, effectiveTo: null }, "2026-08-09")).toBe("Current");
    expect(resolveFacultyAllocationStatus({ isCurrent: true, effectiveTo: "2026-01-01" }, "2026-08-09")).toBe("Ended");
    expect(resolveFacultyAllocationStatus({ isCurrent: false, effectiveTo: null }, "2026-08-09")).toBe("Inactive");
  });

  it("matches SubjectAllocation by faculty + section academic scope (no new relationship)", () => {
    const names = matchSubjectNamesForFacultySection({
      facultyId: 42,
      section: sections[0]!,
      allocations: [
        { staffId: 42, subjectId: 7, academicYearId: 100, courseId: 1, groupId: 2, semesterId: 3 },
        { staffId: 99, subjectId: 8, academicYearId: 100, courseId: 1, groupId: 2, semesterId: 3 },
      ],
      subjectNameById: new Map([
        [7, "Physics"],
        [8, "Chemistry"],
      ]),
    });
    expect(names).toEqual(["Physics"]);
  });

  it("one faculty + one section", () => {
    const rows = buildFacultySectionAllocationRows({
      assignments: [
        {
          id: 1,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 11,
          sectionCode: "A",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-06-01",
          effectiveTo: null,
          isCurrent: true,
        },
      ],
      sections,
      subjectAllocations: [
        { staffId: 42, subjectId: 7, academicYearId: 100, courseId: 1, groupId: 2, semesterId: 3 },
      ],
      subjectNameById: new Map([[7, "Physics"]]),
      sectionGroups: [sectionGroupAb],
      todayIso: "2026-08-09",
    });
    expect(rows).toHaveLength(1);
    expect(rows[0]).toMatchObject({
      operationalClassLabel: "A",
      underlyingSectionCodes: ["A"],
      facultyName: "Dr. John Smith",
      subjectLabel: "Physics",
      effectiveFrom: "2026-06-01",
      effectiveTo: null,
      allocationStatus: "Current",
      isCombinedSectionGroup: false,
      assignmentIds: [1],
    });
  });

  it("one faculty + A+B combined operational class retains assignment ids", () => {
    const rows = buildFacultySectionAllocationRows({
      assignments: [
        {
          id: 1,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 11,
          sectionCode: "A",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-06-01",
          isCurrent: true,
        },
        {
          id: 2,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 12,
          sectionCode: "B",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-06-15",
          isCurrent: true,
        },
      ],
      sections,
      subjectAllocations: [],
      subjectNameById: new Map(),
      sectionGroups: [sectionGroupAb],
      todayIso: "2026-08-09",
    });
    expect(rows).toHaveLength(1);
    expect(rows[0]).toMatchObject({
      operationalClassLabel: "Combined · A + B",
      underlyingSectionCodes: ["A", "B"],
      facultyName: "Dr. John Smith",
      isCombinedSectionGroup: true,
      sectionGroupCode: "AB",
      assignmentIds: [1, 2],
      effectiveFrom: "2026-06-01",
      effectiveTo: null,
      allocationStatus: "Current",
    });
    expect(formatOperationalClassLabel(true, "A + B")).toBe("Combined · A + B");
  });

  it("multiple faculty + combined sections → one operational row each", () => {
    const rows = buildFacultySectionAllocationRows({
      assignments: [
        {
          id: 1,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 11,
          sectionCode: "A",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-06-01",
          isCurrent: true,
        },
        {
          id: 2,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 12,
          sectionCode: "B",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-06-01",
          isCurrent: true,
        },
        {
          id: 3,
          facultyId: 99,
          facultyName: "Dr. Ada Lovelace",
          sectionId: 11,
          sectionCode: "A",
          academicYearId: 100,
          role: "Secondary",
          effectiveFrom: "2026-07-01",
          isCurrent: true,
        },
        {
          id: 4,
          facultyId: 99,
          facultyName: "Dr. Ada Lovelace",
          sectionId: 12,
          sectionCode: "B",
          academicYearId: 100,
          role: "Secondary",
          effectiveFrom: "2026-07-01",
          isCurrent: true,
        },
      ],
      sections,
      subjectAllocations: [],
      subjectNameById: new Map(),
      sectionGroups: [sectionGroupAb],
      todayIso: "2026-08-09",
    });
    expect(rows).toHaveLength(2);
    expect(rows.every((r) => r.operationalClassLabel === "Combined · A + B")).toBe(true);
    expect(rows.map((r) => r.facultyId).sort((a, b) => a - b)).toEqual([42, 99]);
    expect(rows.find((r) => r.facultyId === 42)!.assignmentIds).toEqual([1, 2]);
    expect(rows.find((r) => r.facultyId === 99)!.assignmentIds).toEqual([3, 4]);
  });

  it("ended assignment preserves effective From/To", () => {
    const rows = buildFacultySectionAllocationRows({
      assignments: [
        {
          id: 5,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 11,
          sectionCode: "A",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-01-01",
          effectiveTo: "2026-03-01",
          isCurrent: true,
        },
      ],
      sections,
      subjectAllocations: [],
      subjectNameById: new Map(),
      sectionGroups: [],
      todayIso: "2026-08-09",
    });
    expect(rows[0]).toMatchObject({
      allocationStatus: "Ended",
      effectiveFrom: "2026-01-01",
      effectiveTo: "2026-03-01",
      operationalClassLabel: "A",
    });
  });

  it("inactive assignment is not collapsed into combined class", () => {
    const rows = buildFacultySectionAllocationRows({
      assignments: [
        {
          id: 1,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 11,
          sectionCode: "A",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-06-01",
          isCurrent: false,
        },
        {
          id: 2,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 12,
          sectionCode: "B",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-06-01",
          isCurrent: false,
        },
      ],
      sections,
      subjectAllocations: [],
      subjectNameById: new Map(),
      sectionGroups: [sectionGroupAb],
      todayIso: "2026-08-09",
    });
    expect(rows).toHaveLength(2);
    expect(rows.every((r) => r.allocationStatus === "Inactive")).toBe(true);
    expect(rows.every((r) => !r.isCombinedSectionGroup)).toBe(true);
    expect(rows.flatMap((r) => r.assignmentIds).sort((a, b) => a - b)).toEqual([1, 2]);
  });

  it("does not invent a combined model for A+C outside SectionGroup", () => {
    const rows = buildFacultySectionAllocationRows({
      assignments: [
        {
          id: 1,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 11,
          sectionCode: "A",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-06-01",
          isCurrent: true,
        },
        {
          id: 2,
          facultyId: 42,
          facultyName: "Dr. John Smith",
          sectionId: 13,
          sectionCode: "C",
          academicYearId: 100,
          role: "Primary",
          effectiveFrom: "2026-06-01",
          isCurrent: true,
        },
      ],
      sections,
      subjectAllocations: [],
      subjectNameById: new Map(),
      sectionGroups: [sectionGroupAb],
      todayIso: "2026-08-09",
    });
    expect(rows).toHaveLength(2);
    expect(rows.every((r) => !r.isCombinedSectionGroup)).toBe(true);
  });
});

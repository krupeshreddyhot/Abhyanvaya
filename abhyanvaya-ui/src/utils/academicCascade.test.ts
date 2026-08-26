import { describe, expect, it } from "vitest";
import type { AcademicHierarchyNodeDto } from "../services/programService";
import type { SemesterRow } from "../services/setupService";
import { emptyAcademicUiSelection } from "../types/academicUiContext";
import {
  academicCascadePath,
  applyCascadeSelection,
  buildProgramCourseIndex,
  collectHierarchyConsistencyWarnings,
  filterCoursesForProgram,
  filterGroupsForCourse,
  filterSectionsForScope,
  filterSemestersForCourseGroup,
  sanitizeSelectionAgainstOptions,
} from "./academicCascade";

const semesters: SemesterRow[] = [
  { id: 1, number: 1, name: "Sem 1", courseId: 10, courseName: "BSc", groupId: null, groupName: null },
  { id: 2, number: 2, name: "Sem 2", courseId: 10, courseName: "BSc", groupId: 100, groupName: "MPC" },
  { id: 3, number: 1, name: "Other", courseId: 20, courseName: "BA", groupId: null, groupName: null },
];

describe("academicCascade — Program feature mode & ProgramId authority (Prompt 4B)", () => {
  const courses = [
    { id: 10, programId: 1 },
    { id: 11, programId: 1 },
    { id: 20, programId: 2 },
    { id: 30, programId: null },
  ];
  const ready = { hierarchyReady: true, hierarchyFailed: false } as const;
  const failed = { hierarchyReady: false, hierarchyFailed: true } as const;

  it("Programs disabled → all authorized Courses (legacy)", () => {
    expect(filterCoursesForProgram(courses, false, null).map((c) => c.id)).toEqual([10, 11, 20, 30]);
    expect(filterCoursesForProgram(courses, false, 1, undefined, ready).map((c) => c.id)).toEqual([10, 11, 20, 30]);
  });

  it("Programs enabled + no Program → no Courses", () => {
    expect(filterCoursesForProgram(courses, true, null, undefined, ready).map((c) => c.id)).toEqual([]);
  });

  it("Programs enabled but zero Programs configured → still no Course catalog fallback", () => {
    // Feature mode is EnablePrograms, not "programs list non-empty".
    expect(filterCoursesForProgram(courses, true, null, new Map(), ready).map((c) => c.id)).toEqual([]);
  });

  it("matching ProgramId + hierarchy → Course shown", () => {
    const index = new Map<number, Set<number>>([[1, new Set([10, 11])]]);
    expect(filterCoursesForProgram(courses, true, 1, index, ready).map((c) => c.id)).toEqual([10, 11]);
  });

  it("matching ProgramId but missing hierarchy → Course still shown (ProgramId authoritative)", () => {
    const index = new Map<number, Set<number>>([[1, new Set([10])]]);
    expect(filterCoursesForProgram(courses, true, 1, index, ready).map((c) => c.id)).toEqual([10, 11]);
  });

  it("conflicting ProgramId vs hierarchy → Course NOT shown under hierarchy Program", () => {
    // Hierarchy incorrectly lists course 20 under program 1; Course.ProgramId is 2.
    const index = new Map<number, Set<number>>([[1, new Set([10, 20])]]);
    expect(filterCoursesForProgram(courses, true, 1, index, ready).map((c) => c.id)).toEqual([10, 11]);
    expect(filterCoursesForProgram(courses, true, 1, index, ready).some((c) => c.id === 20)).toBe(false);
  });

  it("null ProgramId → Course NOT shown when a Program is selected", () => {
    const index = new Map<number, Set<number>>([[1, new Set([10, 30])]]);
    expect(filterCoursesForProgram(courses, true, 1, index, ready).some((c) => c.id === 30)).toBe(false);
  });

  it("Program selected with zero courses → empty result (no catalog fallback)", () => {
    const index = new Map<number, Set<number>>([[9, new Set()]]);
    expect(filterCoursesForProgram(courses, true, 9, index, ready).map((c) => c.id)).toEqual([]);
  });

  it("hierarchy failure → no unrelated Course fallback", () => {
    const index = new Map<number, Set<number>>([[1, new Set([10, 11])]]);
    expect(filterCoursesForProgram(courses, true, 1, index, failed).map((c) => c.id)).toEqual([]);
  });

  it("records hierarchy consistency warnings without exposing conflicting Courses", () => {
    const index = new Map<number, Set<number>>([
      [1, new Set([10, 20, 30])], // 20 belongs to program 2; 30 has null ProgramId
      [2, new Set([20])],
    ]);
    const warnings = collectHierarchyConsistencyWarnings(courses, index);
    expect(warnings.some((w) => w.courseId === 20 && w.hierarchyProgramId === 1)).toBe(true);
    expect(warnings.some((w) => w.courseId === 30 && w.hierarchyProgramId === 1)).toBe(true);
    expect(warnings.some((w) => w.courseId === 20 && w.hierarchyProgramId === 2)).toBe(false);
    expect(filterCoursesForProgram(courses, true, 1, index, ready).map((c) => c.id)).toEqual([10, 11]);
  });
});

describe("academicCascade — cascading selection", () => {
  it("clears Course and below when Program changes", () => {
    const current = {
      ...emptyAcademicUiSelection(),
      programId: 1,
      courseId: 10,
      groupId: 100,
      semesterId: 2,
      sectionId: 5,
      sectionIds: [5],
      subjectId: 50,
    };
    const next = applyCascadeSelection(current, { programId: 2 });
    expect(next.programId).toBe(2);
    expect(next.courseId).toBeNull();
    expect(next.groupId).toBeNull();
    expect(next.semesterId).toBeNull();
    expect(next.sectionId).toBeNull();
    expect(next.sectionIds).toEqual([]);
    expect(next.subjectId).toBeNull();
  });

  it("clears Group/Semester/Section/Subject when Course changes", () => {
    const current = {
      ...emptyAcademicUiSelection(),
      courseId: 10,
      groupId: 100,
      semesterId: 2,
      sectionId: 5,
      sectionIds: [5],
      subjectId: 50,
    };
    const next = applyCascadeSelection(current, { courseId: 20 });
    expect(next.courseId).toBe(20);
    expect(next.groupId).toBeNull();
    expect(next.semesterId).toBeNull();
    expect(next.sectionId).toBeNull();
    expect(next.subjectId).toBeNull();
  });

  it("does not clear Subject when Section changes (Subject Master = C+G+S)", () => {
    const current = {
      ...emptyAcademicUiSelection(),
      courseId: 10,
      groupId: 100,
      semesterId: 2,
      sectionId: 5,
      sectionIds: [5],
      subjectId: 50,
    };
    const next = applyCascadeSelection(current, { sectionId: 6 });
    expect(next.sectionId).toBe(6);
    expect(next.sectionIds).toEqual([6]);
    expect(next.subjectId).toBe(50);
  });

  it("clears Section when Academic Year changes but keeps curriculum selection", () => {
    const current = {
      ...emptyAcademicUiSelection(),
      academicYearId: 1,
      courseId: 10,
      groupId: 100,
      semesterId: 2,
      sectionId: 5,
      sectionIds: [5],
      subjectId: 50,
    };
    const next = applyCascadeSelection(current, { academicYearId: 2 });
    expect(next.academicYearId).toBe(2);
    expect(next.courseId).toBe(10);
    expect(next.groupId).toBe(100);
    expect(next.semesterId).toBe(2);
    expect(next.subjectId).toBe(50);
    expect(next.sectionId).toBeNull();
    expect(next.sectionIds).toEqual([]);
  });

  it("supports multi-section combined class selection", () => {
    const current = { ...emptyAcademicUiSelection(), subjectId: 50 };
    const next = applyCascadeSelection(current, { sectionIds: [5, 6] });
    expect(next.sectionIds).toEqual([5, 6]);
    expect(next.sectionId).toBeNull();
    expect(next.subjectId).toBe(50);
  });
});

describe("academicCascade — filtered children", () => {
  it("filters groups by course", () => {
    const groups = [
      { id: 100, courseId: 10 },
      { id: 101, courseId: 10 },
      { id: 200, courseId: 20 },
    ];
    expect(filterGroupsForCourse(groups, 10).map((g) => g.id)).toEqual([100, 101]);
    expect(filterGroupsForCourse(groups, null)).toEqual([]);
  });

  it("filters semesters by course/group scope (Group-specific only)", () => {
    expect(filterSemestersForCourseGroup(semesters, 10, 100).map((s) => s.id)).toEqual([2]);
    expect(filterSemestersForCourseGroup(semesters, null, null)).toEqual([]);
  });

  it("excludes historical NULL-group Semesters and does not fall back to another group's semester", () => {
    const rows: SemesterRow[] = [
      { id: 111, number: 3, name: "Semester III", courseId: 10, courseName: "B.Com", groupId: 200, groupName: "General" },
      { id: 112, number: 1, name: "Semester I", courseId: 10, courseName: "B.Com", groupId: null, groupName: null },
      { id: 113, number: 1, name: "Semester I", courseId: 10, courseName: "B.Com", groupId: 100, groupName: "CA" },
    ];
    // Group 100 — Group-specific Sem I only; historical NULL-group and other-group Semesters excluded.
    expect(filterSemestersForCourseGroup(rows, 10, 100).map((s) => s.id)).toEqual([113]);
    expect(filterSemestersForCourseGroup(rows, 10, 999).map((s) => s.id)).toEqual([]);
  });

  it("filters sections by year + C/G/S", () => {
    const sections = [
      { id: 1, academicYearId: 9, courseId: 10, groupId: 100, semesterId: 2 },
      { id: 2, academicYearId: 9, courseId: 10, groupId: 100, semesterId: 1 },
      { id: 3, academicYearId: 8, courseId: 10, groupId: 100, semesterId: 2 },
    ];
    expect(
      filterSectionsForScope(sections, {
        academicYearId: 9,
        courseId: 10,
        groupId: 100,
        semesterId: 2,
      }).map((s) => s.id),
    ).toEqual([1]);
  });
});

describe("academicCascade — hierarchy index & sanitize", () => {
  it("builds program → course index from hierarchy roots", () => {
    const roots: AcademicHierarchyNodeDto[] = [
      {
        kind: "Program",
        id: 1,
        code: "P1",
        name: "Science",
        children: [
          { kind: "Course", id: 10, code: "BSC", name: "BSc", children: [] },
          { kind: "Course", id: 11, code: "BSC2", name: "BSc2", children: [] },
        ],
      },
      {
        kind: "Program",
        id: 2,
        code: "P2",
        name: "Arts",
        children: [{ kind: "Course", id: 20, code: "BA", name: "BA", children: [] }],
      },
    ];
    const index = buildProgramCourseIndex(roots);
    expect([...index.get(1)!]).toEqual([10, 11]);
    expect([...index.get(2)!]).toEqual([20]);
  });

  it("clears programId when Programs unavailable without wiping course", () => {
    const selection = {
      ...emptyAcademicUiSelection(),
      programId: 1,
      courseId: 10,
      groupId: 100,
    };
    const next = sanitizeSelectionAgainstOptions(selection, {
      enablePrograms: false,
      programIds: new Set(),
      courseIds: new Set([10]),
      groupIds: new Set([100]),
      semesterIds: new Set(),
      sectionIds: new Set(),
      subjectIds: new Set(),
    });
    expect(next.programId).toBeNull();
    expect(next.courseId).toBe(10);
    expect(next.groupId).toBe(100);
  });

  it("clears sectionId/sectionIds when section options are empty (no re-inject loop)", () => {
    const selection = {
      ...emptyAcademicUiSelection(),
      courseId: 10,
      groupId: 100,
      semesterId: 2,
      sectionId: 5,
      sectionIds: [5],
    };
    const next = sanitizeSelectionAgainstOptions(selection, {
      enablePrograms: false,
      programIds: new Set(),
      courseIds: new Set([10]),
      groupIds: new Set([100]),
      semesterIds: new Set([2]),
      sectionIds: new Set(),
      subjectIds: new Set(),
    });
    expect(next.sectionId).toBeNull();
    expect(next.sectionIds).toEqual([]);
    // Stable under repeated sanitize — previously oscillated [] ↔ [5].
    const again = sanitizeSelectionAgainstOptions(next, {
      enablePrograms: false,
      programIds: new Set(),
      courseIds: new Set([10]),
      groupIds: new Set([100]),
      semesterIds: new Set([2]),
      sectionIds: new Set(),
      subjectIds: new Set(),
    });
    expect(again.sectionId).toBeNull();
    expect(again.sectionIds).toEqual([]);
  });

  it("cascades clear when selected course drops out of filtered options", () => {
    const selection = {
      ...emptyAcademicUiSelection(),
      programId: 1,
      courseId: 99,
      groupId: 100,
      semesterId: 2,
      subjectId: 50,
    };
    const next = sanitizeSelectionAgainstOptions(selection, {
      enablePrograms: true,
      programIds: new Set([1]),
      courseIds: new Set([10]),
      groupIds: new Set([100]),
      semesterIds: new Set([2]),
      sectionIds: new Set(),
      subjectIds: new Set([50]),
    });
    expect(next.courseId).toBeNull();
    expect(next.groupId).toBeNull();
    expect(next.semesterId).toBeNull();
    expect(next.subjectId).toBeNull();
  });

  it("documents cascade paths for enabled/disabled Programs", () => {
    expect(academicCascadePath(true)).toContain("Program → Course");
    expect(academicCascadePath(false)).toMatch(/^Course → Group/);
    expect(academicCascadePath(true)).toContain("Subject via Course + Group + Semester");
  });
});

/**
 * AI29.1D Prompt 20 — UI companion for mandatory regression cases 1–15 (+ allocation catalog 16–26).
 * Production cascade / attendance helpers are authoritative; this file only composes them.
 */
import { describe, expect, it } from "vitest";
import type { SemesterRow } from "../services/setupService";
import { emptyAcademicUiSelection } from "../types/academicUiContext";
import {
  applyCascadeSelection,
  filterCoursesForProgram,
  filterGroupsForCourse,
  filterSectionsForScope,
  filterSemestersForCourseGroup,
} from "./academicCascade";
import {
  buildAttendanceWritePayload,
  buildStudentsForMarkingParams,
  resolveAttendanceMarkingMode,
} from "./attendanceMarkingScope";
import { GROUPING_STRATEGY_OPTIONS } from "./allocationStrategyCatalog";
import { POPULATION_FILTER_MODES } from "./allocationPopulationFilter";

const semesters: SemesterRow[] = [
  { id: 1, number: 1, name: "Sem 1", courseId: 10, courseName: "BSc", groupId: null, groupName: null },
  { id: 2, number: 2, name: "Sem 2", courseId: 10, courseName: "BSc", groupId: 100, groupName: "MPC" },
  { id: 3, number: 1, name: "Other", courseId: 20, courseName: "BA", groupId: null, groupName: null },
];

const courses = [
  { id: 10, programId: 1 },
  { id: 11, programId: 1 },
  { id: 20, programId: 2 },
];
const ready = { hierarchyReady: true, hierarchyFailed: false } as const;

describe("AI29.1D Prompt 20 — Academic Hierarchy (1–8)", () => {
  it("Case 1: Program enabled — Course list requires Program", () => {
    const index = new Map<number, Set<number>>([[1, new Set([10, 11])]]);
    expect(filterCoursesForProgram(courses, true, null, index, ready)).toEqual([]);
    expect(filterCoursesForProgram(courses, true, 1, index, ready).map((c) => c.id)).toEqual([10, 11]);
  });

  it("Case 2: Program disabled — all authorized Courses", () => {
    expect(filterCoursesForProgram(courses, false, null).map((c) => c.id)).toEqual([10, 11, 20]);
  });

  it("Case 3: Course filtering by ProgramId authority", () => {
    const index = new Map<number, Set<number>>([[1, new Set([10, 20])]]);
    expect(filterCoursesForProgram(courses, true, 1, index, ready).map((c) => c.id)).toEqual([10, 11]);
  });

  it("Case 4: Group filtering by Course", () => {
    const groups = [
      { id: 100, courseId: 10 },
      { id: 200, courseId: 20 },
    ];
    expect(filterGroupsForCourse(groups, 10).map((g) => g.id)).toEqual([100]);
  });

  it("Case 5: Semester filtering by Course/Group", () => {
    expect(filterSemestersForCourseGroup(semesters, 10, 100).map((s) => s.id)).toEqual([1, 2]);
  });

  it("Case 6: Section filtering by Year + C/G/S", () => {
    const sections = [
      { id: 1, academicYearId: 9, courseId: 10, groupId: 100, semesterId: 2 },
      { id: 2, academicYearId: 9, courseId: 10, groupId: 100, semesterId: 1 },
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

  it("Case 7: Subject stays when Section omitted / Subject not cleared by Year-only curriculum keep", () => {
    const current = {
      ...emptyAcademicUiSelection(),
      academicYearId: 1,
      courseId: 10,
      groupId: 100,
      semesterId: 2,
      subjectId: 50,
      sectionId: 5,
      sectionIds: [5],
    };
    const yearChange = applyCascadeSelection(current, { academicYearId: 2 });
    expect(yearChange.subjectId).toBe(50);
    expect(yearChange.sectionIds).toEqual([]);
  });

  it("Case 8: Section does not alter Subject Master", () => {
    const current = {
      ...emptyAcademicUiSelection(),
      courseId: 10,
      groupId: 100,
      semesterId: 2,
      subjectId: 50,
      sectionId: 5,
      sectionIds: [5],
    };
    const next = applyCascadeSelection(current, { sectionIds: [6, 7] });
    expect(next.sectionIds).toEqual([6, 7]);
    expect(next.subjectId).toBe(50);
  });
});

describe("AI29.1D Prompt 20 — Attendance (9–15)", () => {
  it("Case 9: Faculty with timetable", () => {
    expect(
      resolveAttendanceMarkingMode({
        mode: "Timetable",
        hasTimetable: true,
        courseId: 1,
        groupId: 2,
        semesterId: 3,
        subjectId: 4,
      }),
    ).toBe("Timetable");
  });

  it("Case 10: Faculty without timetable", () => {
    expect(resolveAttendanceMarkingMode({ mode: "Legacy", hasTimetable: false })).toBe("Manual");
  });

  it("Case 11: Manual Course → Group → Semester → Subject → Period payload shape", () => {
    const params = buildStudentsForMarkingParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      selectedSectionIds: [],
    });
    expect(params.courseId).toBe(1);
    expect(params.groupId).toBe(2);
    expect(params.semesterId).toBe(3);
    expect(params.subjectId).toBe(4);
    expect(params.sectionId).toBeUndefined();
  });

  it("Case 12: Manual attendance with Section", () => {
    const payload = buildAttendanceWritePayload({
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      students: [{ studentNumber: "A1", status: 1 }],
      getStatus: (s) => s.status,
      selectedSectionIds: [10],
      operation: "mark",
    });
    expect(payload.sectionId).toBe(10);
    expect(payload.sectionIds).toEqual([10]);
  });

  it("Case 13: Manual attendance without Section", () => {
    const payload = buildAttendanceWritePayload({
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      students: [{ studentNumber: "A1", status: 1 }],
      getStatus: (s) => s.status,
      selectedSectionIds: [],
      operation: "mark",
    });
    expect(payload.sectionId).toBeUndefined();
    expect(payload.sectionIds).toBeUndefined();
  });

  it("Case 14: Combined Section attendance", () => {
    const params = buildStudentsForMarkingParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      selectedSectionIds: [11, 12],
    });
    expect(params.sectionIds).toEqual([11, 12]);
  });

  it("Case 15: Timetable Section attendance write scope", () => {
    const payload = buildAttendanceWritePayload({
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      students: [{ studentNumber: "A1", status: 1 }],
      getStatus: (s) => s.status,
      selectedSectionIds: [10],
      operation: "edit",
    });
    expect(payload.sectionIds).toEqual([10]);
  });
});

describe("AI29.1D Prompt 20 — Allocation catalog compatibility (16–26)", () => {
  it("exposes mandatory grouping modes including LastThreeDigits and StudentNumberRange", () => {
    const codes = GROUPING_STRATEGY_OPTIONS.map((o) => o.code);
    for (const code of [
      "StudentNumberRange",
      "LastThreeDigits",
      "Alphabetical",
      "Gender",
      "Merit",
      "Scholarship",
      "MinorSubject",
      "Language",
      "Transport",
      "Hostel",
      "ElectiveCombination",
    ]) {
      expect(codes).toContain(code);
    }
  });

  it("exposes population filter modes for facet strategies", () => {
    for (const mode of [
      "StudentNumberRange",
      "LastThreeDigitsRange",
      "Gender",
      "Merit",
      "ScholarshipCategory",
      "MinorSubject",
      "Language",
      "TransportRoute",
      "Hostel",
      "ElectiveCombination",
    ]) {
      expect(POPULATION_FILTER_MODES).toContain(mode);
    }
  });
});

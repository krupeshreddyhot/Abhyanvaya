import { describe, expect, it } from "vitest";
import {
  buildAttendancePopulationParams,
  describeAttendancePopulation,
  subjectMasterScopeOf,
} from "./attendanceSectionBehavior";

describe("Prompt 12 — Attendance Section behavior", () => {
  it("1. Subject Master remains Course + Group + Semester (no Section)", () => {
    expect(subjectMasterScopeOf({ courseId: 1, groupId: 2, semesterId: 3 })).toEqual({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
    });
  });

  it("2–4. Section A selected → population filter is Section A only", () => {
    const params = buildAttendancePopulationParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      selectedSectionIds: [11],
    });
    expect(params.sectionId).toBe(11);
    expect(params.sectionIds).toEqual([11]);
    expect(describeAttendancePopulation([11], ["A"])).toContain("Section A");
  });

  it("5. Section B selected → population filter is Section B only", () => {
    const params = buildAttendancePopulationParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      selectedSectionIds: [12],
    });
    expect(params.sectionIds).toEqual([12]);
    expect(describeAttendancePopulation([12], ["B"])).toContain("Section B");
  });

  it("6. No Section → omit filters (legacy Course + Group + Semester + Subject + Period)", () => {
    const params = buildAttendancePopulationParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      selectedSectionIds: [],
    });
    expect(params.sectionId).toBeUndefined();
    expect(params.sectionIds).toBeUndefined();
    expect(describeAttendancePopulation([])).toContain("no Section filter");
  });

  it("7. Combined timetable sections A+B from session contract (no SectionGroup logic in UI)", () => {
    const params = buildAttendancePopulationParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      preferTimetableSections: true,
      timetableSectionIds: [11, 12],
      selectedSectionIds: [99], // ignored when preferring timetable contract
    });
    expect(params.sectionIds).toEqual([11, 12]);
    expect(describeAttendancePopulation([11, 12], ["A", "B"])).toContain("A + B");
  });
});

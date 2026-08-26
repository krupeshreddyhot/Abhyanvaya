import { describe, expect, it } from "vitest";
import {
  buildAttendanceSaveScope,
  buildAttendanceWritePayload,
  buildSectionListParams,
  buildStudentsForMarkingParams,
  hasTimetableAcademicDrift,
  manualScopeHint,
  MULTIPLE_CURRENT_ACADEMIC_YEARS_MESSAGE,
  NO_CURRENT_ACADEMIC_YEAR_MESSAGE,
  normalizeSectionIds,
  resolveAttendanceMarkingMode,
  resolveAuthoritativeAcademicYear,
  rosterStudentNumbersForSave,
  snapshotFromTimetableResolution,
  studentRowsOmitSectionFields,
  timetableScopeHint,
} from "./attendanceMarkingScope";

describe("attendanceMarkingScope — Prompt 11 regression scenarios", () => {
  it("1. Faculty with timetable → Timetable mode", () => {
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

  it("2. Faculty without timetable → Manual mode", () => {
    expect(
      resolveAttendanceMarkingMode({
        mode: "Legacy",
        hasTimetable: false,
        message: "No published timetable entry",
      }),
    ).toBe("Manual");
    expect(resolveAttendanceMarkingMode(null)).toBe("Manual");
  });

  it("3. Faculty with timetable and Section → sectionIds passed to roster API", () => {
    const params = buildStudentsForMarkingParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-08T00:00:00.000Z",
      selectedSectionIds: [10],
    });
    expect(params.sectionId).toBe(10);
    expect(params.sectionIds).toEqual([10]);
    expect(timetableScopeHint({ subjectName: "Math", sectionCodes: ["A"], roomName: "R1" })).toContain("Section A");
  });

  it("4. Faculty without timetable and no Section → omit section filters (legacy)", () => {
    const params = buildStudentsForMarkingParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-08T00:00:00.000Z",
      selectedSectionIds: [],
    });
    expect(params.sectionId).toBeUndefined();
    expect(params.sectionIds).toBeUndefined();
    expect(manualScopeHint(false)).toContain("Section is optional");
    expect(manualScopeHint(false).toLowerCase()).not.toContain("unavailable");
  });

  it("5. Faculty manually selecting Section → single section filter", () => {
    const params = buildStudentsForMarkingParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-08T00:00:00.000Z",
      selectedSectionIds: [22],
    });
    expect(params.sectionIds).toEqual([22]);
    expect(manualScopeHint(true)).toContain("restricted");
  });

  it("6. Combined Section attendance → multiple sectionIds (A + B)", () => {
    const ids = normalizeSectionIds([12, 11, 12]);
    expect(ids).toEqual([11, 12]);
    const params = buildStudentsForMarkingParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-08T00:00:00.000Z",
      selectedSectionIds: ids,
    });
    expect(params.sectionId).toBeUndefined();
    expect(params.sectionIds).toEqual([11, 12]);
    expect(timetableScopeHint({ sectionCodes: ["A", "B"], subjectName: "Physics" })).toContain("A + B");
  });
});

describe("attendanceMarkingScope — Prompt 11A hardening", () => {
  it("scopes Section list to authoritative academic year + C/G/S", () => {
    expect(
      buildSectionListParams({ academicYearId: 9, courseId: 1, groupId: 2, semesterId: 3 }),
    ).toEqual({ academicYearId: 9, courseId: 1, groupId: 2, semesterId: 3 });
    expect(buildSectionListParams({ academicYearId: null, courseId: 1, groupId: 2, semesterId: 3 })).toBeNull();
  });

  it("picks IsCurrent academic year without inventing a second resolver", () => {
    expect(
      resolveAuthoritativeAcademicYear([
        { id: 1, isCurrent: false },
        { id: 2, isCurrent: true },
      ]),
    ).toEqual({ status: "ExactlyOne", academicYearId: 2, message: null });
  });

  it("11B — does not guess first year when no IsCurrent", () => {
    expect(resolveAuthoritativeAcademicYear([{ id: 1, isCurrent: false }, { id: 2, isCurrent: false }])).toEqual({
      status: "None",
      academicYearId: null,
      message: NO_CURRENT_ACADEMIC_YEAR_MESSAGE,
    });
  });

  it("11B — multiple IsCurrent years fail closed", () => {
    const authority = resolveAuthoritativeAcademicYear([
      { id: 1, isCurrent: true },
      { id: 2, isCurrent: true },
    ]);
    expect(authority.status).toBe("Multiple");
    expect(authority.academicYearId).toBeNull();
    expect(authority.message).toBe(MULTIPLE_CURRENT_ACADEMIC_YEARS_MESSAGE);
  });

  it("11B — no Section filter params when AY invalid (legacy cascade unaffected)", () => {
    const params = buildStudentsForMarkingParams({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      date: "2026-08-08T00:00:00.000Z",
      selectedSectionIds: [],
    });
    expect(params.sectionIds).toBeUndefined();
    expect(params.courseId).toBe(1);
  });

  it("timetable academic drift clears timetable ownership (Manual + no stale Section/Room)", () => {
    const snap = snapshotFromTimetableResolution({
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      periodNumber: 1,
      sectionIds: [10, 11],
      sectionCodes: ["A", "B"],
      roomName: "Lab-1",
    });
    expect(
      hasTimetableAcademicDrift(snap, {
        courseId: 1,
        groupId: 2,
        semesterId: 3,
        subjectId: 4,
        periodNumber: 1,
      }),
    ).toBe(false);
    expect(
      hasTimetableAcademicDrift(snap, {
        courseId: 99,
        groupId: 2,
        semesterId: 3,
        subjectId: 4,
        periodNumber: 1,
      }),
    ).toBe(true);
  });

  it("save roster numbers follow Section A / A+B / no-section filters", () => {
    expect(rosterStudentNumbersForSave([{ studentNumber: "A1" }, { studentNumber: "A2" }])).toEqual(["A1", "A2"]);
    expect(
      rosterStudentNumbersForSave([
        { studentNumber: "A1" },
        { studentNumber: "B1" },
      ]),
    ).toEqual(["A1", "B1"]);
    expect(rosterStudentNumbersForSave([])).toEqual([]);
  });
});

describe("attendanceMarkingScope — AI29.1D.15A Prompt 2 save scope contract", () => {
  it("omitted Section → no section fields on save payload", () => {
    expect(buildAttendanceSaveScope([])).toEqual({});
    expect(buildAttendanceSaveScope(null)).toEqual({});
    expect(buildAttendanceSaveScope(undefined)).toEqual({});
  });

  it("one Section → sectionId + sectionIds", () => {
    expect(buildAttendanceSaveScope([22])).toEqual({ sectionId: 22, sectionIds: [22] });
  });

  it("multiple Sections → sectionIds only (combined)", () => {
    expect(buildAttendanceSaveScope([12, 11])).toEqual({ sectionIds: [11, 12] });
  });

  it("empty array / duplicates normalize like roster filters", () => {
    expect(buildAttendanceSaveScope([])).toEqual({});
    expect(buildAttendanceSaveScope([11, 11, 0, -3, 12])).toEqual({ sectionIds: [11, 12] });
  });
});

describe("attendanceMarkingScope — AI29.1D.15A UI write payload (server authoritative)", () => {
  const roster = [
    { studentNumber: "A-001", sectionId: 11, sectionCode: "A" },
    { studentNumber: "B-001", sectionId: 12, sectionCode: "B" },
  ];

  it("manual no Section — omits section scope; student rows have no section fields", () => {
    const payload = buildAttendanceWritePayload({
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      students: roster,
      getStatus: () => 1,
      selectedSectionIds: [],
      operation: "mark",
    });
    expect(payload.sectionId).toBeUndefined();
    expect(payload.sectionIds).toBeUndefined();
    expect(payload.students).toHaveLength(2);
    expect(studentRowsOmitSectionFields(payload.students)).toBe(true);
    expect(manualScopeHint(false)).toContain("Course → Group → Semester → Subject → Period");
  });

  it("manual Section A — request-level section scope only", () => {
    const payload = buildAttendanceWritePayload({
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      students: [roster[0]!],
      getStatus: () => 1,
      selectedSectionIds: [11],
      operation: "mark",
    });
    expect(payload).toMatchObject({ sectionId: 11, sectionIds: [11] });
    expect(payload.students[0]).toEqual({ studentNumber: "A-001", status: 1 });
    expect(studentRowsOmitSectionFields(payload.students)).toBe(true);
  });

  it("manual A+B — combined sectionIds; does not filter students client-side", () => {
    const payload = buildAttendanceWritePayload({
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      students: roster,
      getStatus: () => 0,
      selectedSectionIds: [11, 12],
      operation: "mark",
    });
    expect(payload.sectionId).toBeUndefined();
    expect(payload.sectionIds).toEqual([11, 12]);
    expect(payload.students.map((s) => s.studentNumber)).toEqual(["A-001", "B-001"]);
  });

  it("timetable Section A — uses prefilled selectedSectionIds (no React resolver)", () => {
    const snap = snapshotFromTimetableResolution({
      mode: "Timetable",
      hasTimetable: true,
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      periodNumber: 1,
      sectionIds: [11],
      sectionCodes: ["A"],
    });
    expect(resolveAttendanceMarkingMode({ mode: "Timetable", hasTimetable: true })).toBe("Timetable");
    const payload = buildAttendanceWritePayload({
      subjectId: snap.subjectId,
      date: "2026-08-09T00:00:00.000Z",
      students: [roster[0]!],
      getStatus: () => 1,
      selectedSectionIds: snap.sectionIds,
      operation: "mark",
    });
    expect(payload.sectionIds).toEqual([11]);
    expect(studentRowsOmitSectionFields(payload.students)).toBe(true);
  });

  it("timetable A+B — combined from session snapshot sectionIds only", () => {
    const snap = snapshotFromTimetableResolution({
      mode: "Timetable",
      hasTimetable: true,
      courseId: 1,
      groupId: 2,
      semesterId: 3,
      subjectId: 4,
      periodNumber: 2,
      sectionIds: [12, 11],
      sectionCodes: ["A", "B"],
    });
    const payload = buildAttendanceWritePayload({
      subjectId: snap.subjectId,
      date: "2026-08-09T00:00:00.000Z",
      students: roster,
      getStatus: () => 1,
      selectedSectionIds: snap.sectionIds,
      operation: "mark",
    });
    expect(payload.sectionIds).toEqual([11, 12]);
    expect(payload.students).toHaveLength(2);
  });

  it("edit operations use the same payload contract as mark", () => {
    const markPayload = buildAttendanceWritePayload({
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      students: roster,
      getStatus: () => 1,
      selectedSectionIds: [11, 12],
      operation: "mark",
    });
    const editPayload = buildAttendanceWritePayload({
      subjectId: 4,
      date: "2026-08-09T00:00:00.000Z",
      students: roster,
      getStatus: () => 0,
      selectedSectionIds: [11, 12],
      operation: "edit",
    });
    expect(Object.keys(markPayload).sort()).toEqual(Object.keys(editPayload).sort());
    expect(editPayload.students.every((s) => s.status === 0)).toBe(true);
    expect(studentRowsOmitSectionFields(editPayload.students)).toBe(true);
  });
});

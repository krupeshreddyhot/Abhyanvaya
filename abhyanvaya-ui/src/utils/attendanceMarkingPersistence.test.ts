import { afterEach, describe, expect, it } from "vitest";
import {
  attendanceSelectionStorageKey,
  clearAttendanceMarkingPersistence,
  getCoursesCache,
  getSubjectsCache,
  readPersistedSelection,
  setCoursesCache,
  writePersistedSelection,
} from "./attendanceMarkingPersistence";

describe("attendanceMarkingPersistence — per-user isolation", () => {
  afterEach(() => {
    clearAttendanceMarkingPersistence();
  });

  it("does not restore another user's selection", () => {
    writePersistedSelection(10, 1, {
      courseId: 2,
      groupId: 30,
      semesterId: 111,
      subjectId: 99,
      periodNumber: 1,
      attendanceMethod: "manual",
      date: "2024-08-09",
    });

    expect(readPersistedSelection(10, 1).subjectId).toBe(99);
    expect(readPersistedSelection(20, 1)).toEqual({});
    expect(sessionStorage.getItem(attendanceSelectionStorageKey(10, 1))).toBeTruthy();
  });

  it("clearAttendanceMarkingPersistence removes selections and module caches", () => {
    writePersistedSelection(10, 1, {
      courseId: 2,
      groupId: 30,
      semesterId: 111,
      subjectId: 99,
      periodNumber: 1,
      attendanceMethod: "manual",
      date: "2024-08-09",
    });
    sessionStorage.setItem("attendanceMarking.selection.v2", JSON.stringify({ courseId: 1 }));
    setCoursesCache([{ id: 2, code: "BCOM", name: "B.Com" }]);
    getSubjectsCache().set("2:30:111", [{ id: 99, code: "SA", name: "Security Analysis", isElective: false }]);

    clearAttendanceMarkingPersistence();

    expect(readPersistedSelection(10, 1)).toEqual({});
    expect(sessionStorage.getItem("attendanceMarking.selection.v2")).toBeNull();
    expect(getCoursesCache()).toBeNull();
    expect(getSubjectsCache().size).toBe(0);
  });
});

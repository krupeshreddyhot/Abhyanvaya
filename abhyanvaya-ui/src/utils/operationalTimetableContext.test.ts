import { describe, expect, it } from "vitest";
import {
  buildOperationalTimetableContextView,
  isBlockingTimetableUnavailableMessage,
  sectionOrGroupLabel,
} from "./operationalTimetableContext";

describe("Prompt 15 — Operational timetable context", () => {
  it("exposes Timetable-derived Program…Date fields from session resolution", () => {
    const view = buildOperationalTimetableContextView({
      resolution: {
        mode: "Timetable",
        hasTimetable: true,
        subjectName: "Physics",
        roomName: "Lab-1",
        periodNumber: 2,
        sectionCodes: ["A", "B"],
      },
      labels: {
        programName: "B.Sc",
        courseName: "Science",
        groupName: "G1",
        semesterName: "Sem 1",
        subjectName: "Physics",
        periodLabel: "Period 2",
        roomName: "Lab-1",
        dateLabel: "2026-08-09",
        sectionLabel: "SectionGroup · A + B",
      },
    });
    expect(view.source).toBe("TimetableDerived");
    expect(view.mode).toBe("Timetable");
    expect(view.fields.map((f) => f.label)).toEqual([
      "Program",
      "Course",
      "Group",
      "Semester",
      "Section / SectionGroup",
      "Subject",
      "Period",
      "Room",
      "Date",
    ]);
    expect(view.fields.every((f) => f.fromTimetable)).toBe(true);
    expect(view.fields.find((f) => f.key === "section")?.value).toContain("A + B");
  });

  it("falls back to Manual selection without blocking attendance", () => {
    const view = buildOperationalTimetableContextView({
      resolution: { mode: "Legacy", hasTimetable: false, message: "No published entry" },
      labels: {
        courseName: "Science",
        groupName: "G1",
        semesterName: "Sem 1",
        subjectName: "Physics",
        periodLabel: "Period 1",
        dateLabel: "2026-08-09",
      },
    });
    expect(view.source).toBe("ManualSelection");
    expect(view.banner.toLowerCase()).not.toContain("unavailable");
    expect(view.banner).toContain("Manual selection");
    expect(view.banner).toContain("not required");
  });

  it("labels combined sections as SectionGroup · A + B", () => {
    expect(sectionOrGroupLabel(["A"])).toBe("Section A");
    expect(sectionOrGroupLabel(["A", "B"])).toBe("SectionGroup · A + B");
  });

  it("detects misleading blocked-timetable messages", () => {
    expect(
      isBlockingTimetableUnavailableMessage("Attendance unavailable because timetable is not assigned."),
    ).toBe(true);
    expect(isBlockingTimetableUnavailableMessage("Manual selection context — timetable not required.")).toBe(
      false,
    );
  });
});

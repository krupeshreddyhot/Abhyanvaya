/**
 * AI29.1D Prompt 15 — Operational timetable context presentation.
 * Maps AttendanceSessionResolver /attendance-resolution/current only.
 * Does not resolve timetables in React.
 */

import {
  resolveAttendanceMarkingMode,
  type AttendanceMarkingScopeMode,
  type AttendanceResolutionLike,
} from "./attendanceMarkingScope";

export type OperationalContextSource = "TimetableDerived" | "ManualSelection";

export type OperationalContextField = {
  key: string;
  label: string;
  value: string | null;
  /** True when value came from timetable/session resolution. */
  fromTimetable: boolean;
};

export type OperationalTimetableContextView = {
  source: OperationalContextSource;
  mode: AttendanceMarkingScopeMode;
  /** Banner copy — never blocks attendance when timetable is missing. */
  banner: string;
  fields: OperationalContextField[];
};

export type OperationalContextLabels = {
  programName?: string | null;
  courseName?: string | null;
  groupName?: string | null;
  semesterName?: string | null;
  subjectName?: string | null;
  sectionLabel?: string | null;
  periodLabel?: string | null;
  roomName?: string | null;
  dateLabel?: string | null;
};

const field = (
  key: string,
  label: string,
  value: string | null | undefined,
  fromTimetable: boolean,
): OperationalContextField => ({
  key,
  label,
  value: value && String(value).trim() ? String(value).trim() : null,
  fromTimetable,
});

export function sectionOrGroupLabel(sectionCodes: readonly string[] | null | undefined): string | null {
  const codes = (sectionCodes ?? []).map((c) => String(c).trim()).filter(Boolean);
  if (codes.length === 0) return null;
  if (codes.length === 1) return `Section ${codes[0]}`;
  return `SectionGroup · ${codes.join(" + ")}`;
}

/**
 * Build operational context view from session resolution + current UI labels.
 * Timetable mode → TimetableDerived fields; otherwise ManualSelection with graceful workflow copy.
 */
export function buildOperationalTimetableContextView(input: {
  resolution: AttendanceResolutionLike | null | undefined;
  labels: OperationalContextLabels;
  /** True after user drifted off timetable-prefilled academic fields. */
  driftedToManual?: boolean;
}): OperationalTimetableContextView {
  const mode = input.driftedToManual
    ? "Manual"
    : resolveAttendanceMarkingMode(input.resolution);
  const isTimetable = mode === "Timetable";
  const labels = input.labels;

  if (isTimetable) {
    const sectionLabel =
      labels.sectionLabel ??
      sectionOrGroupLabel(input.resolution?.sectionCodes) ??
      (input.resolution?.sectionIds?.length
        ? `${input.resolution.sectionIds.length} section(s)`
        : null);

    return {
      source: "TimetableDerived",
      mode: "Timetable",
      banner:
        "Timetable-derived context from the attendance session API. You can still adjust filters; changing Course / Group / Semester / Subject / Period switches to Manual selection.",
      fields: [
        field("program", "Program", labels.programName, true),
        field("course", "Course", labels.courseName, true),
        field("group", "Group", labels.groupName, true),
        field("semester", "Semester", labels.semesterName, true),
        field("section", "Section / SectionGroup", sectionLabel, true),
        field("subject", "Subject", labels.subjectName ?? input.resolution?.subjectName, true),
        field(
          "period",
          "Period",
          labels.periodLabel ??
            (input.resolution?.periodNumber != null ? `Period ${input.resolution.periodNumber}` : null),
          true,
        ),
        field("room", "Room", labels.roomName ?? input.resolution?.roomName, true),
        field("date", "Date", labels.dateLabel, true),
      ],
    };
  }

  return {
    source: "ManualSelection",
    mode: "Manual",
    banner:
      "Manual selection context — Course → Group → Semester → Subject → Period. Section is optional. Timetable assignment is not required.",
    fields: [
      field("program", "Program", labels.programName, false),
      field("course", "Course", labels.courseName, false),
      field("group", "Group", labels.groupName, false),
      field("semester", "Semester", labels.semesterName, false),
      field("section", "Section / SectionGroup", labels.sectionLabel, false),
      field("subject", "Subject", labels.subjectName, false),
      field("period", "Period", labels.periodLabel, false),
      field("room", "Room", labels.roomName, false),
      field("date", "Date", labels.dateLabel, false),
    ],
  };
}

/** Reject misleading blocked-attendance copy when timetable is missing. */
export function isBlockingTimetableUnavailableMessage(message: string | null | undefined): boolean {
  if (!message) return false;
  const m = message.toLowerCase();
  return (
    m.includes("attendance unavailable") &&
    (m.includes("timetable") || m.includes("not assigned"))
  );
}

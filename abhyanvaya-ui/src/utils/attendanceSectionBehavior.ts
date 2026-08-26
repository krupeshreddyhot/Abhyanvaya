/**
 * AI29.1D Prompt 12 — Attendance Section behavior (presentation contract).
 * Subject Master stays Course + Group + Semester. Section only scopes the student population.
 * Combined / SectionGroup classes are resolved by AttendanceSessionResolver (sectionIds) — not reimplemented here.
 */

import { buildStudentsForMarkingParams, normalizeSectionIds } from "./attendanceMarkingScope";

export type SubjectMasterScope = {
  courseId: number;
  groupId: number;
  semesterId: number;
};

export type AttendancePopulationScope = SubjectMasterScope & {
  subjectId: number;
  /** Empty = legacy full Course/Group/Semester cohort. */
  sectionIds: number[];
};

/** Subject Master identity — never includes Section. */
export function subjectMasterScopeOf(input: SubjectMasterScope): SubjectMasterScope {
  return {
    courseId: input.courseId,
    groupId: input.groupId,
    semesterId: input.semesterId,
  };
}

/**
 * Apply Section population filter for roster API.
 * Section A → [A]; Section B → [B]; none → omit filters; A+B from timetable contract → both ids.
 */
export function buildAttendancePopulationParams(input: {
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  date: string;
  selectedSectionIds?: readonly number[] | null;
  /** Resolver-owned combined section ids (TimetableSections / SectionGroup expansion on server). */
  timetableSectionIds?: readonly number[] | null;
  preferTimetableSections?: boolean;
}) {
  const fromTimetable = normalizeSectionIds(input.timetableSectionIds);
  const fromManual = normalizeSectionIds(input.selectedSectionIds);
  const sectionIds =
    input.preferTimetableSections && fromTimetable.length > 0 ? fromTimetable : fromManual;

  return buildStudentsForMarkingParams({
    courseId: input.courseId,
    groupId: input.groupId,
    semesterId: input.semesterId,
    subjectId: input.subjectId,
    date: input.date,
    selectedSectionIds: sectionIds,
  });
}

export function describeAttendancePopulation(sectionIds: readonly number[], sectionCodes?: readonly string[]): string {
  const ids = normalizeSectionIds(sectionIds);
  if (ids.length === 0) {
    return "Full Course / Group / Semester cohort (no Section filter).";
  }
  const codes = (sectionCodes ?? []).filter(Boolean);
  if (codes.length > 1) {
    return `Combined attendance population: sections ${codes.join(" + ")}.`;
  }
  if (codes.length === 1) {
    return `Attendance population: Section ${codes[0]}.`;
  }
  return ids.length > 1
    ? `Combined attendance population: ${ids.length} sections.`
    : `Attendance population: section #${ids[0]}.`;
}

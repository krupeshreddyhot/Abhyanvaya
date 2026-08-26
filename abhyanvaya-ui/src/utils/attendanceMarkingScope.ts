/**
 * AI29.1D Prompt 11 / 11A — Attendance marking scope helpers.
 * Does not implement eligibility or resolution logic; maps resolver + UI selection into roster filters.
 */

export type AttendanceMarkingScopeMode = "Timetable" | "Manual";

export type AttendanceResolutionLike = {
  mode?: string | null;
  hasTimetable?: boolean;
  message?: string | null;
  courseId?: number | null;
  groupId?: number | null;
  semesterId?: number | null;
  subjectId?: number | null;
  periodNumber?: number | null;
  roomId?: number | null;
  subjectName?: string | null;
  roomName?: string | null;
  sectionIds?: number[] | null;
  sectionCodes?: string[] | null;
};

export type AttendanceTimetableSnapshot = {
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  periodNumber: number;
  sectionIds: number[];
  sectionCodes: string[];
  roomName: string | null;
};

export type AttendanceRosterFilter = {
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  date: string;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
  /** Omitted/empty = legacy full cohort (no section filter). */
  sectionId?: number;
  sectionIds?: number[];
};

export type AcademicYearLike = {
  id: number;
  isCurrent?: boolean | null;
};

export type AcademicYearAuthorityStatus = "ExactlyOne" | "None" | "Multiple";

export type AcademicYearAuthority =
  | { status: "ExactlyOne"; academicYearId: number; message: null }
  | { status: "None"; academicYearId: null; message: string }
  | { status: "Multiple"; academicYearId: null; message: string };

export const NO_CURRENT_ACADEMIC_YEAR_MESSAGE = "Current academic year is not configured.";
export const MULTIPLE_CURRENT_ACADEMIC_YEARS_MESSAGE =
  "Multiple current academic years are configured. Section selection is disabled until exactly one current academic year is set.";

/** Resolve UI mode from AttendanceSessionResolver response (additive, no redesign). */
export function resolveAttendanceMarkingMode(resolution: AttendanceResolutionLike | null | undefined): AttendanceMarkingScopeMode {
  if (resolution?.hasTimetable && String(resolution.mode).toLowerCase() === "timetable") {
    return "Timetable";
  }
  return "Manual";
}

export function normalizeSectionIds(ids: readonly number[] | null | undefined): number[] {
  return [...new Set((ids ?? []).filter((id) => Number.isFinite(id) && id > 0))].sort((a, b) => a - b);
}

/**
 * Prompt 11B — fail-closed Academic Year authority for Section options.
 * Exactly one IsCurrent → use it. None / Multiple → do not guess.
 */
export function resolveAuthoritativeAcademicYear(
  years: readonly AcademicYearLike[] | null | undefined,
): AcademicYearAuthority {
  const currentIds = [...new Set((years ?? []).filter((y) => y.isCurrent && y.id > 0).map((y) => y.id))].sort(
    (a, b) => a - b,
  );
  if (currentIds.length === 1) {
    return { status: "ExactlyOne", academicYearId: currentIds[0]!, message: null };
  }
  if (currentIds.length === 0) {
    return { status: "None", academicYearId: null, message: NO_CURRENT_ACADEMIC_YEAR_MESSAGE };
  }
  return { status: "Multiple", academicYearId: null, message: MULTIPLE_CURRENT_ACADEMIC_YEARS_MESSAGE };
}

/** @deprecated Prompt 11B — use resolveAuthoritativeAcademicYear (fail-closed). */
export function pickAuthoritativeAcademicYearId(years: readonly AcademicYearLike[] | null | undefined): number | null {
  const authority = resolveAuthoritativeAcademicYear(years);
  return authority.status === "ExactlyOne" ? authority.academicYearId : null;
}

/** Section list params: Academic Year → Course → Group → Semester → Section. */
export function buildSectionListParams(input: {
  academicYearId: number | null | undefined;
  courseId: number;
  groupId: number;
  semesterId: number;
}): { academicYearId: number; courseId: number; groupId: number; semesterId: number } | null {
  const academicYearId = input.academicYearId ?? 0;
  if (academicYearId <= 0 || input.courseId <= 0 || input.groupId <= 0 || input.semesterId <= 0) {
    return null;
  }
  return {
    academicYearId,
    courseId: input.courseId,
    groupId: input.groupId,
    semesterId: input.semesterId,
  };
}

export function snapshotFromTimetableResolution(resolution: AttendanceResolutionLike): AttendanceTimetableSnapshot {
  return {
    courseId: resolution.courseId ?? 0,
    groupId: resolution.groupId ?? 0,
    semesterId: resolution.semesterId ?? 0,
    subjectId: resolution.subjectId ?? 0,
    periodNumber: resolution.periodNumber ?? 0,
    sectionIds: normalizeSectionIds(resolution.sectionIds),
    sectionCodes: (resolution.sectionCodes ?? []).filter(Boolean),
    roomName: resolution.roomName ?? null,
  };
}

/**
 * True when the user changed a timetable-resolved academic field.
 * Preferred 11A behavior: switch to Manual and clear resolver-owned Section/Room.
 */
export function hasTimetableAcademicDrift(
  snapshot: AttendanceTimetableSnapshot | null | undefined,
  current: {
    courseId: number;
    groupId: number;
    semesterId: number;
    subjectId: number;
    periodNumber: number;
  },
): boolean {
  if (!snapshot) return false;
  return (
    current.courseId !== snapshot.courseId ||
    current.groupId !== snapshot.groupId ||
    current.semesterId !== snapshot.semesterId ||
    current.subjectId !== snapshot.subjectId ||
    current.periodNumber !== snapshot.periodNumber
  );
}

/**
 * Build students-for-marking params.
 * Section filters are only attached when at least one section id is selected.
 * Manual without section → omit filters (existing behavior).
 */
export function buildStudentsForMarkingParams(input: {
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  date: string;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
  selectedSectionIds?: readonly number[] | null;
}): AttendanceRosterFilter {
  const sectionIds = normalizeSectionIds(input.selectedSectionIds);
  const params: AttendanceRosterFilter = {
    courseId: input.courseId,
    groupId: input.groupId,
    semesterId: input.semesterId,
    subjectId: input.subjectId,
    date: input.date,
    search: input.search,
    pageNumber: input.pageNumber,
    pageSize: input.pageSize,
  };
  if (sectionIds.length === 1) {
    params.sectionId = sectionIds[0];
    params.sectionIds = sectionIds;
  } else if (sectionIds.length > 1) {
    params.sectionIds = sectionIds;
  }
  return params;
}

/** Save payload student numbers = roster loaded for the current section filter. */
export function rosterStudentNumbersForSave(students: readonly { studentNumber: string }[]): string[] {
  return students.map((s) => s.studentNumber);
}

/**
 * AI29.1D.15A Prompt 2 — optional Section scope for mark/edit (request-level only).
 * Mirrors roster filter attachment: omit when empty; single includes sectionId + sectionIds.
 */
export function buildAttendanceSaveScope(selectedSectionIds?: readonly number[] | null): {
  sectionId?: number;
  sectionIds?: number[];
} {
  const sectionIds = normalizeSectionIds(selectedSectionIds);
  if (sectionIds.length === 0) return {};
  if (sectionIds.length === 1) {
    return { sectionId: sectionIds[0], sectionIds };
  }
  return { sectionIds };
}

export type AttendanceWriteStudentRow = {
  studentNumber: string;
  status: number;
};

export type AttendanceWritePayload = {
  subjectId: number;
  date: string;
  students: AttendanceWriteStudentRow[];
  sectionId?: number;
  sectionIds?: number[];
};

/**
 * AI29.1D.15A — mark/edit payload builder.
 * - Sends the user's selected scope only (optional sectionId/sectionIds at request level).
 * - Student rows are { studentNumber, status } only — never sectionId/sectionIds on each student.
 * - Does NOT derive eligibility or filter students as a security mechanism (server is authoritative).
 * - Manual mode works with Course→Group→Semester→Subject→Period; Program/Section/Timetable optional.
 * - Timetable mode uses session-prefilled selectedSectionIds only — no React timetable resolver.
 */
export function buildAttendanceWritePayload<T extends { studentNumber: string }>(input: {
  subjectId: number;
  date: string;
  /** Roster returned by students-for-marking for the selected scope — send as-is (no client eligibility filter). */
  students: readonly T[];
  getStatus: (student: T) => number;
  selectedSectionIds?: readonly number[] | null;
  /** mark | edit — same payload shape; server chooses endpoint. */
  operation: "mark" | "edit";
}): AttendanceWritePayload {
  void input.operation; // same contract for mark and edit
  const students: AttendanceWriteStudentRow[] = input.students.map((s) => ({
    studentNumber: s.studentNumber,
    status: input.getStatus(s),
  }));

  return {
    subjectId: input.subjectId,
    date: input.date,
    students,
    ...buildAttendanceSaveScope(input.selectedSectionIds),
  };
}

/** True when a write payload never attaches section fields onto student rows. */
export function studentRowsOmitSectionFields(
  students: readonly Record<string, unknown>[],
): boolean {
  return students.every(
    (s) =>
      !("sectionId" in s) &&
      !("sectionIds" in s) &&
      !("sectionCode" in s) &&
      typeof s.studentNumber === "string" &&
      typeof s.status === "number",
  );
}

export function timetableScopeHint(resolution: AttendanceResolutionLike): string {
  const subject = resolution.subjectName ?? "class";
  const room = resolution.roomName ? ` @ ${resolution.roomName}` : "";
  const codes = (resolution.sectionCodes ?? []).filter(Boolean);
  const sectionPart =
    codes.length > 1
      ? ` · Combined sections: ${codes.join(" + ")}`
      : codes.length === 1
        ? ` · Section ${codes[0]}`
        : resolution.sectionIds?.length
          ? ` · ${resolution.sectionIds.length} section(s)`
          : "";
  return `Timetable mode: ${subject}${room}${sectionPart}. Changing Course / Group / Semester / Subject / Period exits timetable context and clears Section / Room.`;
}

export function manualScopeHint(hasSectionFilter: boolean): string {
  return hasSectionFilter
    ? "Manually selected context with optional Section — roster restricted to that section. Timetable is not required."
    : "Manually selected context — Course → Group → Semester → Subject → Period. Section is optional. Timetable is not required.";
}

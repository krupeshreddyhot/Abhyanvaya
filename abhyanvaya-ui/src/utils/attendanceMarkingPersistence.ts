import type { AttendanceMethodMode } from "../types/attendanceContext";
import type { CourseDto, GroupDto, SubjectDto } from "../services/attendanceService";
import type { SemesterRow } from "../services/setupService";
import type { SectionDto } from "../services/sectionService";

/** Legacy unscoped keys — cleared on logout for older sessions. */
const LEGACY_SELECTION_KEYS = [
  "attendanceMarking.selection.v1",
  "attendanceMarking.selection.v2",
] as const;

const SELECTION_KEY_PREFIX = "attendanceMarking.selection.v3:";

export type PersistedAttendanceSelection = {
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  periodNumber: number;
  attendanceMethod: AttendanceMethodMode;
  date: string;
  /** Optional; empty = legacy full cohort. */
  selectedSectionIds?: number[];
  /** Owner stamp — ignore payload if it does not match the signed-in user. */
  userId?: number;
  tenantId?: number;
};

/** Module-level lookup caches — must be cleared on logout / user switch. */
let coursesCache: CourseDto[] | null = null;
let semestersCache: SemesterRow[] | null = null;
const groupsCache = new Map<number, GroupDto[]>();
const subjectsCache = new Map<string, SubjectDto[]>();
const sectionsCache = new Map<string, SectionDto[]>();

export const isScopedSemesterCache = (rows: SemesterRow[] | null): rows is SemesterRow[] =>
  rows != null && (rows.length === 0 || typeof rows[0]?.courseId === "number");

export const getCoursesCache = () => coursesCache;
export const setCoursesCache = (rows: CourseDto[] | null) => {
  coursesCache = rows;
};
export const getSemestersCache = () => semestersCache;
export const setSemestersCache = (rows: SemesterRow[] | null) => {
  semestersCache = rows;
};
export const getGroupsCache = () => groupsCache;
export const getSubjectsCache = () => subjectsCache;
export const getSectionsCache = () => sectionsCache;

export const subjectsCacheKey = (courseId: number, groupId: number, semesterId: number) =>
  `${courseId}:${groupId}:${semesterId}`;

export const sectionsCacheKey = (
  academicYearId: number,
  courseId: number,
  groupId: number,
  semesterId: number,
) => `${academicYearId}:${courseId}:${groupId}:${semesterId}`;

export const attendanceSelectionStorageKey = (userId: number, tenantId: number) =>
  `${SELECTION_KEY_PREFIX}${tenantId}:${userId}`;

export const readPersistedSelection = (
  userId: number,
  tenantId: number,
): Partial<PersistedAttendanceSelection> => {
  if (!userId || !tenantId) return {};
  try {
    const raw = sessionStorage.getItem(attendanceSelectionStorageKey(userId, tenantId));
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Partial<PersistedAttendanceSelection>;
    if (!parsed || typeof parsed !== "object") return {};
    // Reject cross-user payloads (defense in depth).
    if (parsed.userId != null && Number(parsed.userId) !== userId) return {};
    if (parsed.tenantId != null && Number(parsed.tenantId) !== tenantId) return {};
    return parsed;
  } catch {
    return {};
  }
};

export const writePersistedSelection = (
  userId: number,
  tenantId: number,
  selection: PersistedAttendanceSelection,
) => {
  if (!userId || !tenantId) return;
  try {
    sessionStorage.setItem(
      attendanceSelectionStorageKey(userId, tenantId),
      JSON.stringify({ ...selection, userId, tenantId }),
    );
  } catch {
    // Ignore storage errors (e.g. private browsing quota).
  }
};

/** Clears attendance selection + in-memory catalogs for all users in this tab. */
export const clearAttendanceMarkingPersistence = () => {
  try {
    for (const key of LEGACY_SELECTION_KEYS) {
      sessionStorage.removeItem(key);
    }
    const toRemove: string[] = [];
    for (let i = 0; i < sessionStorage.length; i++) {
      const key = sessionStorage.key(i);
      if (key?.startsWith(SELECTION_KEY_PREFIX)) toRemove.push(key);
    }
    for (const key of toRemove) sessionStorage.removeItem(key);
  } catch {
    // ignore
  }

  coursesCache = null;
  semestersCache = null;
  groupsCache.clear();
  subjectsCache.clear();
  sectionsCache.clear();
};

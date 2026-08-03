/** Extract API error message from axios failures. */
export const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

/** C# DayOfWeek: Sunday = 0 … Saturday = 6 */
export const DAY_LABELS: Record<number, string> = {
  0: "Sunday",
  1: "Monday",
  2: "Tuesday",
  3: "Wednesday",
  4: "Thursday",
  5: "Friday",
  6: "Saturday",
};

/** Mon–Sun display order for working-day toggles */
export const WEEKDAY_ORDER: readonly number[] = [1, 2, 3, 4, 5, 6, 0];

export const formatTimeSpan = (value: string): string => {
  const parts = value.split(":");
  if (parts.length >= 2) return `${parts[0]}:${parts[1]}`;
  return value;
};

export const toTimeSpan = (hhmm: string): string => {
  const trimmed = hhmm.trim();
  if (!trimmed) return "00:00:00";
  return trimmed.length === 5 ? `${trimmed}:00` : trimmed;
};

type SemesterLike = { id: number; name: string; courseId: number; groupId: number | null };
type SubjectSemesterLink = { courseId: number; groupId: number; semesterId: number };

/**
 * Semesters available for a course/group selection.
 * Includes: course-wide (groupId null), group-specific, and any semester used by
 * subjects for that course/group. Falls back to all course semesters when none match
 * (common when semesters were created under a different group but reused).
 */
export const resolveSemestersForCourseGroup = <T extends SemesterLike>(
  semesters: T[],
  courseId: number | "" | null | undefined,
  groupId: number | "" | null | undefined,
  options?: {
    subjects?: SubjectSemesterLink[];
    selectedSemesterId?: number | "" | null;
  },
): T[] => {
  if (!courseId) return semesters;

  const subjects = options?.subjects ?? [];
  const fromSubjects = new Set(
    subjects
      .filter((s) => s.courseId === courseId && (!groupId || s.groupId === groupId))
      .map((s) => s.semesterId),
  );

  const matched = semesters.filter((s) => {
    if (s.courseId !== courseId) return false;
    if (fromSubjects.has(s.id)) return true;
    if (!groupId) return true;
    return s.groupId == null || s.groupId === groupId;
  });

  const result = matched.length > 0 ? matched : semesters.filter((s) => s.courseId === courseId);

  const selectedId = options?.selectedSemesterId;
  if (selectedId && !result.some((s) => s.id === selectedId)) {
    const current = semesters.find((s) => s.id === selectedId);
    if (current) return [current, ...result];
  }

  return result;
};

/** Parse "HH:mm" or "HH:mm:ss" to minutes from midnight; null if invalid. */
export const timeToMinutes = (value: string): number | null => {
  const parts = value.trim().split(":");
  if (parts.length < 2) return null;
  const h = Number(parts[0]);
  const m = Number(parts[1]);
  if (!Number.isFinite(h) || !Number.isFinite(m)) return null;
  return h * 60 + m;
};

/** Duration in minutes between start and end; null if invalid or end <= start. */
export const minutesBetween = (startTime: string, endTime: string): number | null => {
  const start = timeToMinutes(startTime);
  const end = timeToMinutes(endTime);
  if (start == null || end == null || end <= start) return null;
  return end - start;
};

/** MUI Select may type `target.value` as number when MenuItem values are numeric. */
export const parseOptionalSelectNumber = (value: number | string): number | "" =>
  value === "" ? "" : Number(value);

/** Bit flag for C# DayOfWeek (Sunday = 0 … Saturday = 6). */
export const dayFlag = (dayOfWeek: number): number => 1 << dayOfWeek;

export const isDayFlagSet = (flags: number, dayOfWeek: number): boolean =>
  (flags & dayFlag(dayOfWeek)) !== 0;

export const toggleDayFlag = (flags: number, dayOfWeek: number, checked: boolean): number =>
  checked ? flags | dayFlag(dayOfWeek) : flags & ~dayFlag(dayOfWeek);

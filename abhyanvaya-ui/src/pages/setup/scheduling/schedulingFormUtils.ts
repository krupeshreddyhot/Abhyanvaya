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

/** MUI Select may type `target.value` as number when MenuItem values are numeric. */
export const parseOptionalSelectNumber = (value: number | string): number | "" =>
  value === "" ? "" : Number(value);

/** Bit flag for C# DayOfWeek (Sunday = 0 … Saturday = 6). */
export const dayFlag = (dayOfWeek: number): number => 1 << dayOfWeek;

export const isDayFlagSet = (flags: number, dayOfWeek: number): boolean =>
  (flags & dayFlag(dayOfWeek)) !== 0;

export const toggleDayFlag = (flags: number, dayOfWeek: number, checked: boolean): number =>
  checked ? flags | dayFlag(dayOfWeek) : flags & ~dayFlag(dayOfWeek);

import { DAY_LABELS, WEEKDAY_ORDER } from "./schedulingFormUtils";

export type AvailabilityEntry = {
  id: number;
  startDate: string;
  endDate: string;
  availabilityType: number;
  label?: string;
};

export const parseDateOnly = (value: string): Date => {
  const [y, m, d] = value.split("-").map(Number);
  return new Date(y, m - 1, d);
};

export const formatDateOnly = (date: Date): string => {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
};

export const addDays = (date: Date, days: number): Date => {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
};

export const daysBetween = (start: Date, end: Date): number =>
  Math.round((end.getTime() - start.getTime()) / (24 * 60 * 60 * 1000));

export const getMondayOfWeek = (anchor: Date): Date => {
  const day = anchor.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  return addDays(anchor, diff);
};

export const entryOverlapsDay = (entry: AvailabilityEntry, day: Date): boolean => {
  const start = parseDateOnly(entry.startDate);
  const end = parseDateOnly(entry.endDate);
  const dayStart = new Date(day.getFullYear(), day.getMonth(), day.getDate());
  return dayStart >= start && dayStart <= end;
};

export const weekDaysFromMonday = (weekStart: Date): Date[] =>
  WEEKDAY_ORDER.slice(0, 7).map((dow) => {
    const monday = getMondayOfWeek(weekStart);
    const mondayDow = monday.getDay();
    const offset = dow >= mondayDow ? dow - mondayDow : dow + 7 - mondayDow;
    return addDays(monday, offset);
  });

export const monthGridDays = (year: number, month: number): (Date | null)[] => {
  const first = new Date(year, month, 1);
  const last = new Date(year, month + 1, 0);
  const startPad = (first.getDay() + 6) % 7;
  const cells: (Date | null)[] = Array.from({ length: startPad }, () => null);
  for (let d = 1; d <= last.getDate(); d += 1) {
    cells.push(new Date(year, month, d));
  }
  while (cells.length % 7 !== 0) cells.push(null);
  return cells;
};

export const dayLabel = (date: Date): string => DAY_LABELS[date.getDay()] ?? "";

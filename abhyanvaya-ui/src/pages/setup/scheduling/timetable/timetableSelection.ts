/** Cell coordinate in the weekly timetable grid. */
export type CellCoord = {
  dayOfWeek: number;
  timeSlotId: number;
};

export type CellSelection = Set<string>;

export const cellKey = (dayOfWeek: number, timeSlotId: number): string =>
  `${dayOfWeek}-${timeSlotId}`;

export const parseCellKey = (key: string): CellCoord => {
  const [day, slot] = key.split("-");
  return { dayOfWeek: Number(day), timeSlotId: Number(slot) };
};

/** Mon–Sat default; Sunday included when present in `days` or entries. */
export const DEFAULT_WEEK_DAYS: readonly number[] = [1, 2, 3, 4, 5, 6];

export const resolveWeekDays = (entries: { dayOfWeek: number }[], extraDays?: number[]): number[] => {
  const set = new Set<number>(DEFAULT_WEEK_DAYS);
  for (const d of extraDays ?? []) set.add(d);
  for (const e of entries) set.add(e.dayOfWeek);
  return [...set].sort((a, b) => {
    const order = (d: number) => (d === 0 ? 7 : d);
    return order(a) - order(b);
  });
};

/** Rectangle selection between anchor and current cell (inclusive). */
export const cellsInRect = (
  anchor: CellCoord,
  current: CellCoord,
  days: number[],
  timeSlotIds: number[],
): string[] => {
  const dayIdxA = days.indexOf(anchor.dayOfWeek);
  const dayIdxB = days.indexOf(current.dayOfWeek);
  const slotIdxA = timeSlotIds.indexOf(anchor.timeSlotId);
  const slotIdxB = timeSlotIds.indexOf(current.timeSlotId);
  if (dayIdxA < 0 || dayIdxB < 0 || slotIdxA < 0 || slotIdxB < 0) return [];

  const dayFrom = Math.min(dayIdxA, dayIdxB);
  const dayTo = Math.max(dayIdxA, dayIdxB);
  const slotFrom = Math.min(slotIdxA, slotIdxB);
  const slotTo = Math.max(slotIdxA, slotIdxB);

  const keys: string[] = [];
  for (let d = dayFrom; d <= dayTo; d++) {
    for (let s = slotFrom; s <= slotTo; s++) {
      keys.push(cellKey(days[d], timeSlotIds[s]));
    }
  }
  return keys;
};

export const toggleCellInSelection = (selection: CellSelection, key: string): CellSelection => {
  const next = new Set(selection);
  if (next.has(key)) next.delete(key);
  else next.add(key);
  return next;
};

export const mergeSelection = (selection: CellSelection, keys: string[]): CellSelection => {
  const next = new Set(selection);
  for (const k of keys) next.add(k);
  return next;
};

export const clearSelection = (): CellSelection => new Set();

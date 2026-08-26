/**
 * Prevent MUI Select out-of-range crashes when persisted IDs load before option lists.
 * Faculty attendance cold-start often has courseId/groupId set while courses=[] briefly.
 */

export function safeSelectValue(
  value: number,
  options: ReadonlyArray<{ id: number }>,
  emptyValue = 0,
): number {
  if (!Number.isFinite(value) || value === emptyValue) return emptyValue;
  return options.some((o) => o.id === value) ? value : emptyValue;
}

export function safeMultiSelectValues(
  values: readonly number[],
  options: ReadonlyArray<{ id: number }>,
): number[] {
  if (!values?.length) return [];
  const allowed = new Set(options.map((o) => o.id));
  return values.filter((id) => Number.isFinite(id) && id > 0 && allowed.has(id));
}

export function safePeriodValue(
  periodNumber: number,
  allowedPeriods: ReadonlyArray<{ value: number }>,
  fallback = 1,
): number {
  if (!Number.isFinite(periodNumber) || periodNumber <= 0) return fallback;
  return allowedPeriods.some((p) => p.value === periodNumber) ? periodNumber : fallback;
}

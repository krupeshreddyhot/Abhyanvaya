import type { CompatibleTeachingGroupOptionDto, TimetableEntryDto } from "../../../../services/schedulingService";
import { getTeachingGroup, type TeachingGroupSummaryDto } from "../../../../services/teachingGroupService";
import type { TeachingGroupGridHint } from "./timetableUtils";

/** Map compatible-TG API rows into display-only grid hints (not a compatibility filter). */
export const hintFromCompatibleOption = (
  option: CompatibleTeachingGroupOptionDto,
): TeachingGroupGridHint => ({
  id: option.id,
  name: option.name,
  code: option.code,
  status: option.status,
  resolvedStudentCount: option.resolvedStudentCount,
  expectedStudentCount: option.expectedStudentCount,
  maxTeachingCapacity: option.maxTeachingCapacity,
});

export const hintFromTeachingGroupSummary = (
  tg: TeachingGroupSummaryDto,
): TeachingGroupGridHint => ({
  id: tg.id,
  name: tg.name,
  code: tg.code,
  status: tg.status,
  resolvedStudentCount: tg.resolvedStudentCount,
  expectedStudentCount: tg.expectedStudentCount,
  maxTeachingCapacity: tg.maxTeachingCapacity,
});

export const mergeTeachingGroupHintsFromOptions = (
  previous: Map<number, TeachingGroupGridHint>,
  options: CompatibleTeachingGroupOptionDto[],
): Map<number, TeachingGroupGridHint> => {
  const next = new Map(previous);
  for (const option of options) {
    next.set(option.id, hintFromCompatibleOption(option));
  }
  return next;
};

/**
 * Load display metadata for assigned TeachingGroupIds missing from the hint map.
 * Uses GET Teaching Group by id — never SubjectAllocation inference or compatibility rules.
 */
export const enrichMissingTeachingGroupHints = async (
  entries: TimetableEntryDto[],
  previous: Map<number, TeachingGroupGridHint>,
): Promise<Map<number, TeachingGroupGridHint>> => {
  const needed = [
    ...new Set(
      entries
        .map((e) => e.teachingGroupId)
        .filter((id): id is number => typeof id === "number" && id > 0),
    ),
  ].filter((id) => !previous.has(id));

  if (needed.length === 0) return previous;

  const next = new Map(previous);
  await Promise.all(
    needed.map(async (id) => {
      try {
        const res = await getTeachingGroup(id);
        next.set(id, hintFromTeachingGroupSummary(res.data));
      } catch {
        // Keep id-only display via formatEntryTeachingGroupLine fallback.
      }
    }),
  );
  return next;
};

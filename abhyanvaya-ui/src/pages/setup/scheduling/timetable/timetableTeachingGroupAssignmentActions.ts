import { getApiErrorMessage, getHttpStatus } from "../../../../utils/apiErrorMessage";
import {
  assignTeachingGroupToTimetableEntry,
  clearTeachingGroupFromTimetableEntry,
  listCompatibleTeachingGroupsForTimetableEntry,
  type CompatibleTeachingGroupOptionDto,
  type TimetableEntryDto,
} from "../../../../services/schedulingService";

export const TIMETABLE_TG_CONFLICT_MESSAGE =
  "This timetable entry was changed by another user. The latest Teaching Group assignment has been loaded. Please review your selection and try again.";
export type TeachingGroupAssignmentOutcome =
  | { kind: "unchanged"; entry: TimetableEntryDto }
  | { kind: "success"; entry: TimetableEntryDto }
  | {
      kind: "conflict";
      entry: TimetableEntryDto;
      options: CompatibleTeachingGroupOptionDto[];
      message: string;
    }
  | { kind: "error"; message: string; status?: number };

/** Reload compatible options; derive current assignment from server `isAssignedToEntry`. */
export const reloadCompatibleTeachingGroups = async (
  entryId: number,
  entry: TimetableEntryDto,
): Promise<{ options: CompatibleTeachingGroupOptionDto[]; entry: TimetableEntryDto }> => {
  const res = await listCompatibleTeachingGroupsForTimetableEntry(entryId);
  const options = res.data;
  const assignedId = options.find((o) => o.isAssignedToEntry)?.id ?? null;
  return {
    options,
    entry: { ...entry, teachingGroupId: assignedId },
  };
};

/**
 * Apply TG selection delta only when it differs from the baseline loaded from the server.
 * Never sends teachingGroupId on create/update payloads.
 */
export const applyTeachingGroupSelectionDelta = async (
  entry: TimetableEntryDto,
  selectedTeachingGroupId: number | "",
  baselineTeachingGroupId: number | null,
): Promise<TeachingGroupAssignmentOutcome> => {
  const selected =
    selectedTeachingGroupId === "" ? null : selectedTeachingGroupId;
  const baseline = baselineTeachingGroupId;

  if (selected === baseline) {
    return { kind: "unchanged", entry };
  }

  try {
    if (selected == null) {
      const res = await clearTeachingGroupFromTimetableEntry(entry.id);
      return { kind: "success", entry: res.data };
    }
    const res = await assignTeachingGroupToTimetableEntry(entry.id, {
      teachingGroupId: selected,
    });
    return { kind: "success", entry: res.data };
  } catch (error) {
    const status = getHttpStatus(error);
    if (status === 409) {
      try {
        const reloaded = await reloadCompatibleTeachingGroups(entry.id, entry);
        return {
          kind: "conflict",
          entry: reloaded.entry,
          options: reloaded.options,
          message: TIMETABLE_TG_CONFLICT_MESSAGE,
        };
      } catch {
        return { kind: "error", status: 409, message: TIMETABLE_TG_CONFLICT_MESSAGE };
      }
    }
    return {
      kind: "error",
      status,
      message: getApiErrorMessage(error, "Teaching Group assignment failed.", {
        forbiddenFallback:
          "You are not authorized to change the Teaching Group on this timetable entry.",
      }),
    };
  }
};

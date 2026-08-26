import type { PublishReadinessFindingDto } from "../../../services/schedulingService";
import { DAY_LABELS } from "./schedulingFormUtils";

/** Presentation-only label hints for known codes; unknown codes use generic fallback. */
export const publishFindingCodeLabel = (code: string): string => {
  switch (code) {
    case "ROOM_CAPACITY":
      return "Room capacity";
    case "TEACHING_GROUP_CAPACITY_EXCEEDED":
      return "Teaching Group capacity";
    case "LIFECYCLE_FROZEN":
      return "Frozen";
    case "LIFECYCLE_NOT_ELIGIBLE":
      return "Not eligible";
    case "LIFECYCLE_ARCHIVED":
      return "Archived";
    case "LIFECYCLE_PUBLISHED_SCOPE_CONFLICT":
      return "Published scope conflict";
    default:
      return "Publish blocker";
  }
};

export const formatFindingContextLine = (f: PublishReadinessFindingDto): string | null => {
  const parts: string[] = [];
  if (f.timetableEntryId != null) parts.push(`Entry #${f.timetableEntryId}`);
  if (f.dayOfWeek != null) parts.push(DAY_LABELS[f.dayOfWeek] ?? `Day ${f.dayOfWeek}`);
  if (f.timeSlotId != null) parts.push(`Slot #${f.timeSlotId}`);
  if (f.roomId != null) parts.push(`Room #${f.roomId}`);
  if (f.teachingGroupCode || f.teachingGroupName || f.teachingGroupId != null) {
    const tg =
      f.teachingGroupCode ||
      f.teachingGroupName ||
      (f.teachingGroupId != null ? `TG #${f.teachingGroupId}` : null);
    if (tg) {
      parts.push(f.teachingGroupStatus ? `${tg} (${f.teachingGroupStatus})` : tg);
    }
  }
  return parts.length > 0 ? parts.join(" · ") : null;
};

export const formatFindingMetricsLine = (f: PublishReadinessFindingDto): string | null => {
  const parts: string[] = [];
  if (f.placementSize != null) parts.push(`Placement: ${f.placementSize}`);
  if (f.effectiveRoomCapacity != null) parts.push(`Effective room: ${f.effectiveRoomCapacity}`);
  if (f.resolvedStudentCount != null) parts.push(`Resolved students: ${f.resolvedStudentCount}`);
  if (f.maxTeachingCapacity != null) parts.push(`TG max: ${f.maxTeachingCapacity}`);
  return parts.length > 0 ? parts.join(". ") + "." : null;
};

export const publishFindingSeverityChipColor = (
  severity: string | undefined,
): "default" | "info" | "warning" | "error" => {
  switch (severity) {
    case "Critical":
    case "Error":
      return "error";
    case "Information":
      return "info";
    case "Warning":
      return "warning";
    default:
      return "default";
  }
};

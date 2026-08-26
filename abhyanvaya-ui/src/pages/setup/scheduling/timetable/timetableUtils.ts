import {
  SlotKind,
  TimetableStatus,
  type SoftWarningDto,
  type TimeSlotDto,
  type TimetableEntryDto,
} from "../../../../services/schedulingService";
import { TeachingGroupStatus } from "../../../../services/teachingGroupService";
import { formatTimeSpan } from "../schedulingFormUtils";

export const TIMETABLE_STATUS_LABELS: Record<number, string> = {
  [TimetableStatus.Draft]: "Draft",
  [TimetableStatus.Locked]: "Locked",
  [TimetableStatus.Published]: "Published",
  [TimetableStatus.Archived]: "Archived",
};

export const TIMETABLE_STATUS_COLORS: Record<number, "default" | "warning" | "success" | "info"> = {
  [TimetableStatus.Draft]: "warning",
  [TimetableStatus.Locked]: "success",
  [TimetableStatus.Published]: "info",
  [TimetableStatus.Archived]: "default",
};

/** Display-only hint for grid TG state (not a compatibility resolver). */
export type TeachingGroupGridHint = {
  id: number;
  name: string;
  code?: string | null;
  status: number;
  resolvedStudentCount?: number;
  expectedStudentCount?: number | null;
  maxTeachingCapacity?: number | null;
};

export const periodTimeSlots = (slots: TimeSlotDto[]): TimeSlotDto[] =>
  slots
    .filter((s) => s.slotKind === SlotKind.Period)
    .sort((a, b) => (a.periodNumber ?? 0) - (b.periodNumber ?? 0) || a.startTime.localeCompare(b.startTime));

export const formatSlotLabel = (slot: TimeSlotDto): string => {
  const period = slot.periodNumber != null ? `P${slot.periodNumber}` : slot.name;
  return `${period} (${formatTimeSpan(slot.startTime)}–${formatTimeSpan(slot.endTime)})`;
};

export const formatEntryCompact = (entry: TimetableEntryDto, mode: "academic" | "faculty" | "room" = "academic"): string => {
  const subject = entry.subjectName ?? "Subject";
  if (mode === "faculty") return `${subject} · ${entry.roomName ?? "—"}`;
  if (mode === "room") return `${subject} · ${entry.staffName ?? "—"}`;
  return `${subject} · ${entry.staffName ?? "—"} · ${entry.roomName ?? "—"}`;
};

/**
 * Informational grid line for Teaching Group state.
 * Uses TeachingGroupId + optional display hint — never infers from SubjectAllocation.
 */
export const formatEntryTeachingGroupLine = (
  entry: TimetableEntryDto,
  hint?: TeachingGroupGridHint | null,
): string => {
  if (entry.teachingGroupId == null) return "Teaching Group: None";
  if (!hint) return `Teaching Group: #${entry.teachingGroupId}`;
  const label = hint.code?.trim() ? `${hint.code} — ${hint.name}` : hint.name;
  const archived = hint.status === TeachingGroupStatus.Archived ? " · Archived" : "";
  return `Teaching Group: ${label}${archived}`;
};

/**
 * Server soft-warning capacity feedback for a timetable entry (AI-SCHED-CAP Prompt 4).
 * UI must not recalculate PlacementSize / EffectiveRoomCapacity / TG capacity.
 */
export const entryCapacityFeedbackFromSoftWarnings = (
  entryId: number,
  warnings: SoftWarningDto[] | undefined | null,
): SoftWarningDto[] => {
  if (!warnings?.length) return [];
  return warnings.filter(
    (w) =>
      !w.dismissed &&
      w.entryId === entryId &&
      (w.code === "ROOM_CAPACITY" || w.code === "TEACHING_GROUP_CAPACITY_EXCEEDED"),
  );
};

/**
 * Prefer server soft-warning title/message for grid caption.
 * Falls back to null — does not recalculate capacity on the client.
 */
export const entryTeachingGroupCapacityWarning = (
  hint?: TeachingGroupGridHint | null,
  softWarningsForEntry?: SoftWarningDto[] | null,
): string | null => {
  const fromServer = softWarningsForEntry?.find((w) => w.code === "TEACHING_GROUP_CAPACITY_EXCEEDED");
  if (fromServer) return fromServer.title ?? fromServer.message;
  // Display-only archived label remains via formatEntryTeachingGroupLine; no client capacity math.
  void hint;
  return null;
};

export const downloadBlob = (blob: Blob, filename: string): void => {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
};

export const printTimetable = (): void => {
  window.print();
};

/** Styles applied to printable timetable containers. */
export const timetablePrintSx = {
  "@media print": {
    "& .no-print": { display: "none !important" },
    "& .timetable-grid-wrap": {
      overflow: "visible !important",
      boxShadow: "none !important",
      border: "none !important",
    },
    "& .timetable-grid-table": {
      fontSize: "10px !important",
    },
  },
} as const;

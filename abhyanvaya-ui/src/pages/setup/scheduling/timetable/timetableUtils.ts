import { SlotKind, TimetableStatus, type TimeSlotDto, type TimetableEntryDto } from "../../../../services/schedulingService";
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

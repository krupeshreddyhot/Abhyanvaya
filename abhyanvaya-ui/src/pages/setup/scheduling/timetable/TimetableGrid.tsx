import { useMemo } from "react";
import {
  Box,
  Chip,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from "@mui/material";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { alpha, useTheme } from "@mui/material/styles";
import type { SoftWarningDto, TimeSlotDto, TimetableEntryDto } from "../../../../services/schedulingService";
import { DAY_LABELS } from "../schedulingFormUtils";
import {
  cellKey,
  type CellCoord,
  type CellSelection,
  resolveWeekDays,
} from "./timetableSelection";
import {
  entryCapacityFeedbackFromSoftWarnings,
  formatEntryCompact,
  formatEntryTeachingGroupLine,
  formatSlotLabel,
  periodTimeSlots,
  timetablePrintSx,
  type TeachingGroupGridHint,
} from "./timetableUtils";

export type TimetableViewMode = "academic" | "faculty" | "room";

export type TimetableGridProps = {
  timeSlots: TimeSlotDto[];
  entries: TimetableEntryDto[];
  days?: number[];
  readOnly?: boolean;
  viewMode?: TimetableViewMode;
  selectedCells?: CellSelection;
  onCellMouseDown?: (coord: CellCoord, event: React.MouseEvent) => void;
  onCellMouseEnter?: (coord: CellCoord, event: React.MouseEvent) => void;
  onCellClick?: (coord: CellCoord, event: React.MouseEvent) => void;
  onCellContextMenu?: (coord: CellCoord, event: React.MouseEvent) => void;
  onEntryClick?: (entry: TimetableEntryDto, event: React.MouseEvent) => void;
  onEntryContextMenu?: (entry: TimetableEntryDto, event: React.MouseEvent) => void;
  onDropOnCell?: (coord: CellCoord, data: DataTransfer) => void;
  onEntryDragStart?: (entry: TimetableEntryDto, event: React.DragEvent) => void;
  /** Display-only Teaching Group labels keyed by TeachingGroupId. */
  teachingGroupHints?: Map<number, TeachingGroupGridHint>;
  /** Server soft warnings — capacity captions must come from here (Prompt 4). */
  softWarnings?: SoftWarningDto[];
  cellWarningCounts?: Map<string, number>;
  className?: string;
  maxHeight?: number | string;
};

const TimetableGrid = ({
  timeSlots,
  entries,
  days: daysProp,
  readOnly = false,
  viewMode = "academic",
  selectedCells,
  onCellMouseDown,
  onCellMouseEnter,
  onCellClick,
  onCellContextMenu,
  onEntryClick,
  onEntryContextMenu,
  onDropOnCell,
  onEntryDragStart,
  teachingGroupHints,
  softWarnings,
  cellWarningCounts,
  className,
  maxHeight = "calc(100vh - 280px)",
}: TimetableGridProps) => {
  const theme = useTheme();
  const periodSlots = useMemo(() => periodTimeSlots(timeSlots), [timeSlots]);
  const days = useMemo(() => daysProp ?? resolveWeekDays(entries), [daysProp, entries]);

  const entryMap = useMemo(() => {
    const map = new Map<string, TimetableEntryDto[]>();
    for (const e of entries) {
      const key = cellKey(e.dayOfWeek, e.timeSlotId);
      const list = map.get(key) ?? [];
      list.push(e);
      map.set(key, list);
    }
    return map;
  }, [entries]);

  const stickyBg = theme.palette.background.paper;
  const selectedBg = alpha(theme.palette.primary.main, 0.14);

  return (
    <Paper
      variant="outlined"
      className={`timetable-grid-wrap ${className ?? ""}`}
      sx={{ ...timetablePrintSx, overflow: "hidden" }}
    >
      <TableContainer sx={{ maxHeight, overflow: "auto" }}>
        <Table stickyHeader size="small" className="timetable-grid-table" sx={{ minWidth: 720 }}>
          <TableHead>
            <TableRow>
              <TableCell
                sx={{
                  position: "sticky",
                  left: 0,
                  zIndex: 3,
                  bgcolor: stickyBg,
                  fontWeight: 600,
                  minWidth: 120,
                }}
              >
                Time
              </TableCell>
              {days.map((d) => (
                <TableCell key={d} align="center" sx={{ fontWeight: 600, minWidth: 110 }}>
                  {DAY_LABELS[d] ?? `Day ${d}`}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {periodSlots.map((slot) => (
              <TableRow key={slot.id} hover>
                <TableCell
                  sx={{
                    position: "sticky",
                    left: 0,
                    zIndex: 2,
                    bgcolor: stickyBg,
                    whiteSpace: "nowrap",
                    fontWeight: 500,
                    fontSize: "0.75rem",
                  }}
                >
                  {formatSlotLabel(slot)}
                </TableCell>
                {days.map((day) => {
                  const key = cellKey(day, slot.id);
                  const cellEntries = entryMap.get(key) ?? [];
                  const selected = selectedCells?.has(key) ?? false;
                  const warningCount = cellWarningCounts?.get(key) ?? 0;
                  const coord: CellCoord = { dayOfWeek: day, timeSlotId: slot.id };

                  return (
                    <TableCell
                      key={key}
                      align="left"
                      onMouseDown={(e) => onCellMouseDown?.(coord, e)}
                      onMouseEnter={(e) => onCellMouseEnter?.(coord, e)}
                      onClick={(e) => onCellClick?.(coord, e)}
                      onContextMenu={(e) => {
                        e.preventDefault();
                        onCellContextMenu?.(coord, e);
                      }}
                      onDragOver={(e) => {
                        if (!readOnly) e.preventDefault();
                      }}
                      onDrop={(e) => {
                        e.preventDefault();
                        if (!readOnly) onDropOnCell?.(coord, e.dataTransfer);
                      }}
                      sx={{
                        verticalAlign: "top",
                        p: 0.5,
                        minHeight: 52,
                        cursor: readOnly ? "default" : "cell",
                        bgcolor: selected ? selectedBg : undefined,
                        border: selected ? `1px solid ${theme.palette.primary.main}` : undefined,
                        userSelect: "none",
                        touchAction: readOnly ? "auto" : "none",
                        position: "relative",
                      }}
                    >
                      {warningCount > 0 && (
                        <Tooltip title={`${warningCount} soft warning${warningCount > 1 ? "s" : ""}`}>
                          <Chip
                            icon={<WarningAmberIcon />}
                            label={warningCount}
                            size="small"
                            color="warning"
                            sx={{
                              position: "absolute",
                              top: 2,
                              right: 2,
                              height: 18,
                              "& .MuiChip-label": { px: 0.5, fontSize: "0.65rem" },
                              "& .MuiChip-icon": { fontSize: "0.75rem", ml: 0.25 },
                            }}
                          />
                        </Tooltip>
                      )}
                      {cellEntries.length === 0 ? (
                        <Typography variant="caption" color="text.disabled" sx={{ px: 0.5 }}>
                          —
                        </Typography>
                      ) : (
                        cellEntries.map((entry) => (
                          <Box
                            key={entry.id}
                            draggable={!readOnly}
                            onDragStart={(e) => onEntryDragStart?.(entry, e)}
                            onClick={(e) => {
                              e.stopPropagation();
                              onEntryClick?.(entry, e);
                            }}
                            onContextMenu={(e) => {
                              e.preventDefault();
                              e.stopPropagation();
                              onEntryContextMenu?.(entry, e);
                            }}
                            sx={{
                              px: 0.5,
                              py: 0.25,
                              mb: 0.25,
                              borderRadius: 0.5,
                              bgcolor: alpha(theme.palette.primary.main, 0.08),
                              fontSize: "0.7rem",
                              lineHeight: 1.3,
                              "&:hover": readOnly ? undefined : { bgcolor: alpha(theme.palette.primary.main, 0.16) },
                            }}
                          >
                            <Typography component="div" variant="caption" sx={{ display: "block", fontWeight: 600 }}>
                              {formatEntryCompact(entry, viewMode)}
                            </Typography>
                            <Typography
                              component="div"
                              variant="caption"
                              color="text.secondary"
                              sx={{ display: "block" }}
                              aria-label={formatEntryTeachingGroupLine(
                                entry,
                                entry.teachingGroupId != null
                                  ? teachingGroupHints?.get(entry.teachingGroupId)
                                  : null,
                              )}
                            >
                              {formatEntryTeachingGroupLine(
                                entry,
                                entry.teachingGroupId != null
                                  ? teachingGroupHints?.get(entry.teachingGroupId)
                                  : null,
                              )}
                            </Typography>
                            {(() => {
                              const feedback = entryCapacityFeedbackFromSoftWarnings(
                                entry.id,
                                softWarnings,
                              );
                              if (feedback.length === 0) return null;
                              return feedback.map((w) => (
                                <Typography
                                  key={w.code}
                                  component="div"
                                  variant="caption"
                                  color="warning.main"
                                  sx={{ display: "block" }}
                                  role="status"
                                >
                                  ⚠ {w.title ?? w.message}
                                </Typography>
                              ));
                            })()}
                          </Box>
                        ))
                      )}
                    </TableCell>
                  );
                })}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
};

export default TimetableGrid;

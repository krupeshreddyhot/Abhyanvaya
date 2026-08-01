import {
  Box,
  Chip,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import {
  addDays,
  dayLabel,
  entryOverlapsDay,
  formatDateOnly,
  getMondayOfWeek,
  monthGridDays,
  type AvailabilityEntry,
} from "./availabilityDateUtils";

export type AvailabilityViewMode = "weekly" | "monthly" | "timeline";

type Props = {
  entries: AvailabilityEntry[];
  viewMode: AvailabilityViewMode;
  weekAnchor: Date;
  monthAnchor: Date;
  onWeekAnchorChange: (d: Date) => void;
  onMonthAnchorChange: (d: Date) => void;
  typeLabels: Record<number, string>;
  typeColors: Record<number, string>;
  canManage: boolean;
  onEntryClick: (entry: AvailabilityEntry) => void;
  onEntryMove?: (entry: AvailabilityEntry, targetDay: Date) => void;
};

const AvailabilityViews = ({
  entries,
  viewMode,
  weekAnchor,
  monthAnchor,
  onWeekAnchorChange,
  onMonthAnchorChange,
  typeLabels,
  typeColors,
  canManage,
  onEntryClick,
  onEntryMove,
}: Props) => {
  if (viewMode === "timeline") {
    const sorted = [...entries].sort((a, b) => a.startDate.localeCompare(b.startDate));
    return (
      <Stack spacing={1}>
        {sorted.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No entries to display.
          </Typography>
        ) : (
          sorted.map((e) => (
            <Paper
              key={e.id}
              variant="outlined"
              sx={{
                p: 1.5,
                borderLeft: 4,
                borderLeftColor: typeColors[e.availabilityType] ?? "#1976d2",
                cursor: "pointer",
              }}
              onClick={() => onEntryClick(e)}
            >
              <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }} useFlexGap>
                <Chip
                  size="small"
                  label={typeLabels[e.availabilityType] ?? e.availabilityType}
                  sx={{ bgcolor: typeColors[e.availabilityType], color: "#fff" }}
                />
                <Typography variant="body2">
                  {e.startDate} → {e.endDate}
                </Typography>
                {e.label && (
                  <Typography variant="body2" color="text.secondary">
                    {e.label}
                  </Typography>
                )}
              </Stack>
            </Paper>
          ))
        )}
      </Stack>
    );
  }

  if (viewMode === "monthly") {
    const year = monthAnchor.getFullYear();
    const month = monthAnchor.getMonth();
    const grid = monthGridDays(year, month);
    const monthLabel = monthAnchor.toLocaleString(undefined, { month: "long", year: "numeric" });

    return (
      <Stack spacing={1}>
        <Stack direction="row" sx={{ alignItems: "center" }} spacing={1}>
          <IconButton size="small" onClick={() => onMonthAnchorChange(new Date(year, month - 1, 1))}>
            <ChevronLeftIcon />
          </IconButton>
          <Typography variant="subtitle1" sx={{ flexGrow: 1, textAlign: "center" }}>
            {monthLabel}
          </Typography>
          <IconButton size="small" onClick={() => onMonthAnchorChange(new Date(year, month + 1, 1))}>
            <ChevronRightIcon />
          </IconButton>
        </Stack>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: "repeat(7, 1fr)",
            gap: 0.5,
          }}
        >
          {["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => (
            <Typography key={d} variant="caption" sx={{ fontWeight: 600, textAlign: "center" }}>
              {d}
            </Typography>
          ))}
          {grid.map((day, i) => (
            <Paper
              key={i}
              variant="outlined"
              sx={{
                minHeight: 72,
                p: 0.5,
                bgcolor: day ? "background.paper" : "action.hover",
                opacity: day ? 1 : 0.4,
              }}
            >
              {day && (
                <>
                  <Typography variant="caption" sx={{ display: "block" }}>
                    {day.getDate()}
                  </Typography>
                  <Stack spacing={0.25}>
                    {entries
                      .filter((e) => entryOverlapsDay(e, day))
                      .slice(0, 3)
                      .map((e) => (
                        <Chip
                          key={e.id}
                          size="small"
                          label={typeLabels[e.availabilityType]?.slice(0, 8) ?? "?"}
                          onClick={() => onEntryClick(e)}
                          sx={{
                            height: 18,
                            fontSize: 10,
                            bgcolor: typeColors[e.availabilityType],
                            color: "#fff",
                            maxWidth: "100%",
                          }}
                        />
                      ))}
                  </Stack>
                </>
              )}
            </Paper>
          ))}
        </Box>
      </Stack>
    );
  }

  const monday = getMondayOfWeek(weekAnchor);
  const weekDays = Array.from({ length: 7 }, (_, i) => addDays(monday, i));
  const weekEnd = addDays(monday, 6);
  const weekLabel = `${formatDateOnly(monday)} — ${formatDateOnly(weekEnd)}`;

  const handleDrop = (entry: AvailabilityEntry, targetDay: Date) => {
    if (!canManage || !onEntryMove) return;
    onEntryMove(entry, targetDay);
  };

  return (
    <Stack spacing={1}>
      <Stack direction="row" sx={{ alignItems: "center" }} spacing={1}>
        <IconButton size="small" onClick={() => onWeekAnchorChange(addDays(weekAnchor, -7))}>
          <ChevronLeftIcon />
        </IconButton>
        <Typography variant="subtitle1" sx={{ flexGrow: 1, textAlign: "center" }}>
          Week of {weekLabel}
        </Typography>
        <IconButton size="small" onClick={() => onWeekAnchorChange(addDays(weekAnchor, 7))}>
          <ChevronRightIcon />
        </IconButton>
      </Stack>
      <Table size="small">
        <TableHead>
          <TableRow>
            {weekDays.map((d) => (
              <TableCell key={formatDateOnly(d)} align="center" sx={{ minWidth: 100 }}>
                {dayLabel(d)}
                <Typography variant="caption" sx={{ display: "block" }}>
                  {d.getDate()}
                </Typography>
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          <TableRow>
            {weekDays.map((d) => (
              <TableCell
                key={formatDateOnly(d)}
                sx={{ verticalAlign: "top", minHeight: 120 }}
                onDragOver={(e) => canManage && e.preventDefault()}
                onDrop={(e) => {
                  e.preventDefault();
                  const raw = e.dataTransfer.getData("application/x-availability-id");
                  const id = Number(raw);
                  const entry = entries.find((x) => x.id === id);
                  if (entry) handleDrop(entry, d);
                }}
              >
                <Stack spacing={0.5}>
                  {entries
                    .filter((entry) => entryOverlapsDay(entry, d))
                    .map((entry) => (
                      <Chip
                        key={entry.id}
                        size="small"
                        draggable={canManage}
                        onDragStart={(e) => {
                          e.dataTransfer.setData("application/x-availability-id", String(entry.id));
                        }}
                        label={typeLabels[entry.availabilityType] ?? "Entry"}
                        onClick={() => onEntryClick(entry)}
                        sx={{
                          bgcolor: typeColors[entry.availabilityType],
                          color: "#fff",
                          cursor: canManage ? "grab" : "pointer",
                          width: "100%",
                        }}
                      />
                    ))}
                </Stack>
              </TableCell>
            ))}
          </TableRow>
        </TableBody>
      </Table>
    </Stack>
  );
};

export default AvailabilityViews;

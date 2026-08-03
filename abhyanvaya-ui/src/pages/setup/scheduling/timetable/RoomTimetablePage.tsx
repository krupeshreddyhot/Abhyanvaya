import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import DownloadIcon from "@mui/icons-material/Download";
import PrintIcon from "@mui/icons-material/Print";
import {
  exportTimetableExcel,
  getTimetableGrid,
  getTimetableRoomProjection,
  listTimetables,
  searchRooms,
  type RoomDto,
  type TimetableDto,
  type TimetableEntryDto,
  type TimeSlotDto,
} from "../../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import TimetableGrid from "./TimetableGrid";
import { downloadBlob, printTimetable, timetablePrintSx } from "./timetableUtils";

const RoomTimetablePage = () => {
  const [timetables, setTimetables] = useState<TimetableDto[]>([]);
  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [timetableId, setTimetableId] = useState<number | "">("");
  const [roomId, setRoomId] = useState<number | "">("");
  const [view, setView] = useState<"week" | "day">("week");
  const [dayFilter, setDayFilter] = useState<number | "">(1);

  const [timeSlots, setTimeSlots] = useState<TimeSlotDto[]>([]);
  const [entries, setEntries] = useState<TimetableEntryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const [tt, roomRes] = await Promise.all([
          listTimetables(),
          searchRooms({ page: 1, pageSize: 500, isActive: true }),
        ]);
        setTimetables(tt.data);
        setRooms(roomRes.data.items);
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const loadProjection = useCallback(async () => {
    if (timetableId === "" || roomId === "") return;
    setLoading(true);
    setError(null);
    try {
      const [gridRes, projRes] = await Promise.all([
        getTimetableGrid(Number(timetableId)),
        getTimetableRoomProjection(Number(timetableId), Number(roomId)),
      ]);
      setTimeSlots(gridRes.data.timeSlots);
      setEntries(projRes.data.entries);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [timetableId, roomId]);

  useEffect(() => {
    void loadProjection();
  }, [loadProjection]);

  const occupancy = entries.length;
  const displayedEntries = useMemo(() => {
    if (view === "day" && dayFilter !== "") {
      return entries.filter((e) => e.dayOfWeek === dayFilter);
    }
    return entries;
  }, [entries, view, dayFilter]);

  const days = useMemo(() => {
    if (view === "day" && dayFilter !== "") return [Number(dayFilter)];
    return undefined;
  }, [view, dayFilter]);

  const handleExport = async () => {
    if (timetableId === "" || roomId === "") return;
    const res = await exportTimetableExcel(Number(timetableId), {
      view: "room",
      roomId: Number(roomId),
    });
    downloadBlob(res.data, `room-timetable-${roomId}.xlsx`);
  };

  const roomLabel = rooms.find((r) => r.id === roomId);

  return (
    <Stack spacing={2} sx={timetablePrintSx}>
      <Box className="no-print" sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Room timetable
        </Typography>
        {roomId !== "" && <Chip label={`${occupancy} scheduled periods`} color="primary" variant="outlined" />}
        <Button startIcon={<PrintIcon />} onClick={printTimetable}>
          Print
        </Button>
        <Button startIcon={<DownloadIcon />} onClick={() => void handleExport()} disabled={timetableId === "" || roomId === ""}>
          Excel
        </Button>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} className="no-print" sx={{ flexWrap: "wrap" }}>
        <FormControl sx={{ minWidth: 220 }}>
          <InputLabel>Timetable</InputLabel>
          <Select label="Timetable" value={timetableId} onChange={(e) => setTimetableId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">Select</MenuItem>
            {timetables.map((t) => (
              <MenuItem key={t.id} value={t.id}>
                {t.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl sx={{ minWidth: 220 }}>
          <InputLabel>Room</InputLabel>
          <Select label="Room" value={roomId} onChange={(e) => setRoomId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">Select</MenuItem>
            {rooms.map((r) => (
              <MenuItem key={r.id} value={r.id}>
                {r.code} — {r.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <ToggleButtonGroup exclusive size="small" value={view} onChange={(_, v) => v && setView(v)}>
          <ToggleButton value="week">Week</ToggleButton>
          <ToggleButton value="day">Day</ToggleButton>
        </ToggleButtonGroup>
        {view === "day" && (
          <FormControl sx={{ minWidth: 120 }}>
            <InputLabel>Day</InputLabel>
            <Select label="Day" value={dayFilter} onChange={(e) => setDayFilter(parseOptionalSelectNumber(e.target.value))}>
              {[1, 2, 3, 4, 5, 6, 0].map((d) => (
                <MenuItem key={d} value={d}>
                  {["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"][d === 0 ? 0 : d]}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}
      </Stack>

      {roomLabel && (
        <Typography variant="subtitle2" color="text.secondary">
          {roomLabel.code} — {roomLabel.name} · capacity {roomLabel.capacity}
        </Typography>
      )}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
          <CircularProgress />
        </Box>
      ) : timetableId && roomId ? (
        <TimetableGrid timeSlots={timeSlots} entries={displayedEntries} days={days} readOnly viewMode="room" />
      ) : (
        <Alert severity="info">Select a timetable and room.</Alert>
      )}
    </Stack>
  );
};

export default RoomTimetablePage;

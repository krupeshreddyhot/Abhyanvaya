import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
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
  getTimetableFacultyProjection,
  getTimetableGrid,
  listTimetables,
  type TimetableDto,
  type TimetableEntryDto,
  type TimeSlotDto,
} from "../../../../services/schedulingService";
import { listStaff } from "../../../../services/setupService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import TimetableGrid from "./TimetableGrid";
import { downloadBlob, printTimetable, timetablePrintSx } from "./timetableUtils";

const FacultyTimetablePage = () => {
  const [timetables, setTimetables] = useState<TimetableDto[]>([]);
  const [staff, setStaff] = useState<{ id: number; label: string }[]>([]);
  const [timetableId, setTimetableId] = useState<number | "">("");
  const [staffId, setStaffId] = useState<number | "">("");
  const [view, setView] = useState<"week" | "day">("week");
  const [dayFilter, setDayFilter] = useState<number | "">(1);

  const [timeSlots, setTimeSlots] = useState<TimeSlotDto[]>([]);
  const [entries, setEntries] = useState<TimetableEntryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const [tt, st] = await Promise.all([
          listTimetables(),
          listStaff({ page: 1, pageSize: 500 }),
        ]);
        setTimetables(tt.data);
        setStaff(st.data.items.map((s) => ({ id: s.id, label: `${s.firstName} ${s.lastName}` })));
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const loadProjection = useCallback(async () => {
    if (timetableId === "" || staffId === "") return;
    setLoading(true);
    setError(null);
    try {
      const [gridRes, projRes] = await Promise.all([
        getTimetableGrid(Number(timetableId)),
        getTimetableFacultyProjection(Number(timetableId), Number(staffId)),
      ]);
      setTimeSlots(gridRes.data.timeSlots);
      setEntries(projRes.data.entries);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [timetableId, staffId]);

  useEffect(() => {
    void loadProjection();
  }, [loadProjection]);

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
    if (timetableId === "" || staffId === "") return;
    const res = await exportTimetableExcel(Number(timetableId), {
      view: "faculty",
      staffId: Number(staffId),
    });
    downloadBlob(res.data, `faculty-timetable-${staffId}.xlsx`);
  };

  const staffLabel = staff.find((s) => s.id === staffId)?.label ?? "";

  return (
    <Stack spacing={2} sx={timetablePrintSx}>
      <Box className="no-print" sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Faculty timetable
        </Typography>
        <Button startIcon={<PrintIcon />} onClick={printTimetable}>
          Print
        </Button>
        <Button startIcon={<DownloadIcon />} onClick={() => void handleExport()} disabled={timetableId === "" || staffId === ""}>
          Excel
        </Button>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} className="no-print">
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
          <InputLabel>Faculty</InputLabel>
          <Select label="Faculty" value={staffId} onChange={(e) => setStaffId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">Select</MenuItem>
            {staff.map((s) => (
              <MenuItem key={s.id} value={s.id}>
                {s.label}
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

      <Typography variant="subtitle1" className="print-only" sx={{ display: "none", "@media print": { display: "block" } }}>
        Faculty: {staffLabel}
      </Typography>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
          <CircularProgress />
        </Box>
      ) : timetableId && staffId ? (
        <TimetableGrid timeSlots={timeSlots} entries={displayedEntries} days={days} readOnly viewMode="faculty" />
      ) : (
        <Alert severity="info">Select a timetable and faculty member.</Alert>
      )}
    </Stack>
  );
};

export default FacultyTimetablePage;

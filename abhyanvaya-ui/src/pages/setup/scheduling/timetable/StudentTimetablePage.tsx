import { useCallback, useEffect, useState } from "react";
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
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import DownloadIcon from "@mui/icons-material/Download";
import PrintIcon from "@mui/icons-material/Print";
import {
  exportTimetableExcel,
  getTimetableGrid,
  getTimetableStudentProjection,
  listTimetables,
  type TimetableDto,
  type TimetableEntryDto,
  type TimeSlotDto,
} from "../../../../services/schedulingService";
import { listMasterCourses, listMasterGroups, listSemesters } from "../../../../services/setupService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import TimetableGrid from "./TimetableGrid";
import { downloadBlob, printTimetable, timetablePrintSx } from "./timetableUtils";

const StudentTimetablePage = () => {
  const [timetables, setTimetables] = useState<TimetableDto[]>([]);
  const [courses, setCourses] = useState<{ id: number; name: string }[]>([]);
  const [groups, setGroups] = useState<{ id: number; name: string; courseId: number }[]>([]);
  const [semesters, setSemesters] = useState<{ id: number; name: string; courseId: number; groupId: number | null }[]>([]);

  const [timetableId, setTimetableId] = useState<number | "">("");
  const [courseId, setCourseId] = useState<number | "">("");
  const [groupId, setGroupId] = useState<number | "">("");
  const [semesterId, setSemesterId] = useState<number | "">("");

  const [timeSlots, setTimeSlots] = useState<TimeSlotDto[]>([]);
  const [entries, setEntries] = useState<TimetableEntryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const [tt, c, g, sem] = await Promise.all([
          listTimetables(),
          listMasterCourses(),
          listMasterGroups(),
          listSemesters(),
        ]);
        setTimetables(tt.data);
        setCourses(c.data.map((x) => ({ id: x.id, name: x.name })));
        setGroups(g.data.map((x) => ({ id: x.id, name: x.name, courseId: x.courseId })));
        setSemesters(sem.data.map((x) => ({ id: x.id, name: x.name, courseId: x.courseId, groupId: x.groupId })));
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const loadProjection = useCallback(async () => {
    if (timetableId === "" || courseId === "" || groupId === "" || semesterId === "") return;
    setLoading(true);
    setError(null);
    try {
      const [gridRes, projRes] = await Promise.all([
        getTimetableGrid(Number(timetableId)),
        getTimetableStudentProjection(Number(timetableId), {
          courseId: Number(courseId),
          groupId: Number(groupId),
          semesterId: Number(semesterId),
        }),
      ]);
      setTimeSlots(gridRes.data.timeSlots);
      setEntries(projRes.data.entries);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [timetableId, courseId, groupId, semesterId]);

  useEffect(() => {
    void loadProjection();
  }, [loadProjection]);

  const handleExport = async () => {
    if (timetableId === "" || courseId === "" || groupId === "" || semesterId === "") return;
    const res = await exportTimetableExcel(Number(timetableId), {
      view: "student",
      courseId: Number(courseId),
      groupId: Number(groupId),
      semesterId: Number(semesterId),
    });
    downloadBlob(res.data, `student-timetable-${courseId}-${groupId}-${semesterId}.xlsx`);
  };

  return (
    <Stack spacing={2} sx={timetablePrintSx}>
      <Box className="no-print" sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Student timetable
        </Typography>
        <Button startIcon={<PrintIcon />} onClick={printTimetable}>
          Print
        </Button>
        <Button
          startIcon={<DownloadIcon />}
          onClick={() => void handleExport()}
          disabled={timetableId === "" || courseId === "" || groupId === "" || semesterId === ""}
        >
          Excel
        </Button>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={2} className="no-print" sx={{ flexWrap: "wrap" }}>
        <FormControl sx={{ minWidth: 200 }}>
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
        <FormControl sx={{ minWidth: 160 }}>
          <InputLabel>Course</InputLabel>
          <Select label="Course" value={courseId} onChange={(e) => setCourseId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">Select</MenuItem>
            {courses.map((c) => (
              <MenuItem key={c.id} value={c.id}>
                {c.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl sx={{ minWidth: 160 }}>
          <InputLabel>Group</InputLabel>
          <Select label="Group" value={groupId} onChange={(e) => setGroupId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">Select</MenuItem>
            {groups
              .filter((g) => courseId === "" || g.courseId === courseId)
              .map((g) => (
                <MenuItem key={g.id} value={g.id}>
                  {g.name}
                </MenuItem>
              ))}
          </Select>
        </FormControl>
        <FormControl sx={{ minWidth: 160 }}>
          <InputLabel>Semester</InputLabel>
          <Select label="Semester" value={semesterId} onChange={(e) => setSemesterId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">Select</MenuItem>
            {semesters
              .filter(
                (s) =>
                  (courseId === "" || s.courseId === courseId) &&
                  (groupId === "" || s.groupId === groupId),
              )
              .map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name}
                </MenuItem>
              ))}
          </Select>
        </FormControl>
      </Stack>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
          <CircularProgress />
        </Box>
      ) : timetableId && courseId && groupId && semesterId ? (
        <TimetableGrid timeSlots={timeSlots} entries={entries} readOnly viewMode="academic" />
      ) : (
        <Alert severity="info">Select timetable, course, group, and semester.</Alert>
      )}
    </Stack>
  );
};

export default StudentTimetablePage;

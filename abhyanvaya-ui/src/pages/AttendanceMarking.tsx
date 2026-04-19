import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  CircularProgress,
  FormControlLabel,
  MenuItem,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from "@mui/material";
import useMediaQuery from "@mui/material/useMediaQuery";
import { useTheme } from "@mui/material/styles";
import {
  editAttendance,
  getCourses,
  getGroups,
  getSemesters,
  getStudentsForMarking,
  getSubjects,
  markAttendance,
  type AttendanceStudentDto,
  type CourseDto,
  type GroupDto,
  type SemesterDto,
  type SubjectDto,
} from "../services/attendanceService";

const AttendanceMarking = () => {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const [courses, setCourses] = useState<CourseDto[]>([]);
  const [groups, setGroups] = useState<GroupDto[]>([]);
  const [semesters, setSemesters] = useState<SemesterDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);

  const [courseId, setCourseId] = useState(0);
  const [groupId, setGroupId] = useState(0);
  const [semesterId, setSemesterId] = useState(0);
  const [subjectId, setSubjectId] = useState(0);
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");

  const [students, setStudents] = useState<AttendanceStudentDto[]>([]);
  const [isLocked, setIsLocked] = useState(false);
  const [alreadyMarked, setAlreadyMarked] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  /** Total students in class for selected filters (ignores search) — used for save eligibility */
  const [fullClassTotal, setFullClassTotal] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const pageSize = 50;
  /** Full roster pages use API max page size for fewer round trips */
  const rosterPageSize = 200;
  const [statusMap, setStatusMap] = useState<Record<string, number>>({});

  const [loadingMeta, setLoadingMeta] = useState(true);
  const [loadingStudents, setLoadingStudents] = useState(false);
  const [bulkUpdating, setBulkUpdating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<"card" | "table">(isMobile ? "card" : "table");

  useEffect(() => {
    setViewMode(isMobile ? "card" : "table");
  }, [isMobile]);

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  useEffect(() => {
    const loadMeta = async () => {
      setLoadingMeta(true);
      setError(null);
      try {
        const [cRes, sRes] = await Promise.all([getCourses(), getSemesters()]);
        setCourses(cRes.data);
        setSemesters(sRes.data);
      } catch {
        setError("Failed to load course/semester data.");
      } finally {
        setLoadingMeta(false);
      }
    };
    void loadMeta();
  }, []);

  useEffect(() => {
    if (!courseId) {
      setGroups([]);
      setGroupId(0);
      return;
    }
    const loadGroups = async () => {
      try {
        const res = await getGroups(courseId);
        setGroups(res.data);
      } catch {
        setGroups([]);
      }
    };
    void loadGroups();
  }, [courseId]);

  useEffect(() => {
    if (!courseId || !groupId || !semesterId) {
      setSubjects([]);
      setSubjectId(0);
      return;
    }
    const loadSubjects = async () => {
      try {
        const res = await getSubjects(courseId, groupId, semesterId);
        setSubjects(res.data);
      } catch {
        setSubjects([]);
      }
    };
    void loadSubjects();
  }, [courseId, groupId, semesterId]);

  const canLoadStudents = courseId > 0 && groupId > 0 && semesterId > 0 && subjectId > 0 && !!date;

  const loadStudents = async (targetPage = 1, append = false) => {
    if (!canLoadStudents) return;
    setLoadingStudents(true);
    setError(null);
    setMessage(null);
    try {
      const res = await getStudentsForMarking({
        courseId,
        groupId,
        semesterId,
        subjectId,
        date,
        search: search || undefined,
        pageNumber: targetPage,
        pageSize,
      });
      setStudents((prev) => (append ? [...prev, ...res.data.students] : res.data.students));
      setIsLocked(res.data.isLocked);
      setAlreadyMarked(res.data.alreadyMarked);
      setTotalCount(res.data.totalCount);
      if (!search) {
        setFullClassTotal(res.data.totalCount);
      }
      setPageNumber(res.data.pageNumber);
      setStatusMap((prev) => {
        const next = append ? { ...prev } : {};
        for (const s of res.data.students) {
          if (!(s.studentNumber in next)) {
            next[s.studentNumber] = s.status;
          }
        }
        return next;
      });
    } catch {
      setError("Failed to load students for attendance.");
    } finally {
      setLoadingStudents(false);
    }
  };

  useEffect(() => {
    if (!canLoadStudents) {
      setStudents([]);
      setStatusMap({});
      setAlreadyMarked(false);
      setIsLocked(false);
      setTotalCount(0);
      setFullClassTotal(0);
      setPageNumber(1);
      return;
    }
    void loadStudents(1, false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [courseId, groupId, semesterId, subjectId, date, search]);

  const hasMore = students.length < totalCount;

  const handleLoadMore = async () => {
    if (loadingStudents || !hasMore) return;
    await loadStudents(pageNumber + 1, true);
  };

  const hasRoster = fullClassTotal > 0;

  const canSave = useMemo(
    () =>
      canLoadStudents &&
      hasRoster &&
      !loadingStudents &&
      !bulkUpdating &&
      !saving &&
      !isLocked,
    [canLoadStudents, hasRoster, loadingStudents, bulkUpdating, saving, isLocked]
  );

  /** Full class list for this course/group/semester/subject/date — ignores search (search is view-only). */
  const fetchFullRoster = async () => {
    const all: AttendanceStudentDto[] = [];
    let page = 1;
    let total = 0;

    do {
      const res = await getStudentsForMarking({
        courseId,
        groupId,
        semesterId,
        subjectId,
        date,
        pageNumber: page,
        pageSize: rosterPageSize,
      });
      all.push(...res.data.students);
      total = res.data.totalCount;
      page += 1;
    } while (all.length < total);

    return all;
  };

  const resolveStatus = (s: AttendanceStudentDto, map: Record<string, number>) =>
    Object.prototype.hasOwnProperty.call(map, s.studentNumber) ? map[s.studentNumber] : s.status;

  /** Present first within the current page/list, then name — keeps toggled rows sorted too */
  const sortedStudents = useMemo(() => {
    const map = statusMap;
    const rank = (s: AttendanceStudentDto) =>
      Object.prototype.hasOwnProperty.call(map, s.studentNumber) ? map[s.studentNumber] : s.status;
    return [...students].sort((a, b) => {
      const ra = rank(a);
      const rb = rank(b);
      if (ra !== rb) return rb - ra;
      return a.name.localeCompare(b.name, undefined, { sensitivity: "base" });
    });
  }, [students, statusMap]);

  const setAllStatuses = async (status: number) => {
    if (!canLoadStudents || isLocked || loadingStudents) return;
    setBulkUpdating(true);
    setError(null);
    try {
      const all = await fetchFullRoster();
      setStatusMap((prev) => {
        const next = { ...prev };
        for (const st of all) next[st.studentNumber] = status;
        return next;
      });
    } catch {
      setError("Could not load the full class list for bulk update.");
    } finally {
      setBulkUpdating(false);
    }
  };

  const handleSave = async () => {
    if (!canSave) return;
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const allStudents = await fetchFullRoster();
      const map = statusMap;
      const payload = {
        subjectId,
        date: new Date(`${date}T00:00:00`).toISOString(),
        students: allStudents.map((s) => ({
          studentNumber: s.studentNumber,
          status: resolveStatus(s, map),
        })),
      };

      if (alreadyMarked) {
        await editAttendance(payload);
        setMessage("Attendance updated successfully.");
      } else {
        await markAttendance(payload);
        setMessage("Attendance marked successfully.");
      }

      await loadStudents(1, false);
    } catch {
      setError("Failed to save attendance.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={2} sx={{ pb: isMobile ? 22 : 10 }}>
      <Typography variant={isMobile ? "h5" : "h4"}>Mark Attendance</Typography>

      {loadingMeta && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
          <CircularProgress size={22} />
          <Typography variant="body2" color="text.secondary">
            Loading courses and semesters…
          </Typography>
        </Box>
      )}

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}
      {isLocked && <Alert severity="warning">Attendance is locked for this date and subject.</Alert>}
      {!isLocked && canLoadStudents && (
        <Alert severity="info" sx={{ py: 0.75 }}>
          Search only filters the list. Save sends every student in this class for the selected subject and date
          (present and absent).
        </Alert>
      )}

      <Card>
        <CardContent>
          <Stack spacing={2}>
            <TextField
              select
              label="Course"
              value={courseId}
              onChange={(e) => {
                setCourseId(Number(e.target.value));
                setGroupId(0);
                setSubjectId(0);
              }}
              fullWidth
              disabled={loadingMeta}
            >
              <MenuItem value={0}>Select course</MenuItem>
              {courses.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Group"
              value={groupId}
              onChange={(e) => {
                setGroupId(Number(e.target.value));
                setSubjectId(0);
              }}
              fullWidth
              disabled={loadingMeta || !courseId}
            >
              <MenuItem value={0}>Select group</MenuItem>
              {groups.map((g) => (
                <MenuItem key={g.id} value={g.id}>
                  {g.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Semester"
              value={semesterId}
              onChange={(e) => {
                setSemesterId(Number(e.target.value));
                setSubjectId(0);
              }}
              fullWidth
              disabled={loadingMeta}
            >
              <MenuItem value={0}>Select semester</MenuItem>
              {semesters.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Subject"
              value={subjectId}
              onChange={(e) => setSubjectId(Number(e.target.value))}
              fullWidth
              disabled={loadingMeta || !courseId || !groupId || !semesterId}
            >
              <MenuItem value={0}>Select subject</MenuItem>
              {subjects.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              label="Attendance Date"
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
            />

            <TextField
              label="Search (student no / name / mobile)"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              fullWidth
            />
          </Stack>
        </CardContent>
      </Card>

      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 1.5,
          rowGap: 1,
        }}
      >
        <Typography variant="body2" color="text.secondary" sx={{ flex: "1 1 auto", minWidth: 0 }}>
          {bulkUpdating
            ? "Applying to full class..."
            : loadingStudents
              ? "Loading students..."
              : `Showing: ${students.length} / ${totalCount}${search ? " (filtered)" : ""}`}
        </Typography>
        <Box sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 1, justifyContent: "flex-end" }}>
          {!isMobile && (
            <ToggleButtonGroup
              size="small"
              exclusive
              value={viewMode}
              onChange={(_, value: "card" | "table" | null) => {
                if (value) setViewMode(value);
              }}
            >
              <ToggleButton value="table">Table</ToggleButton>
              <ToggleButton value="card">Cards</ToggleButton>
            </ToggleButtonGroup>
          )}
          {!isMobile && (
            <>
              <Button
                variant="outlined"
                onClick={() => void setAllStatuses(1)}
                disabled={!canSave || bulkUpdating}
              >
                All present
              </Button>
              <Button
                variant="outlined"
                onClick={() => void setAllStatuses(0)}
                disabled={!canSave || bulkUpdating}
              >
                All absent
              </Button>
              <Button variant="contained" onClick={() => void handleSave()} disabled={!canSave || bulkUpdating}>
                {saving ? "Saving..." : alreadyMarked ? "Update attendance" : "Save attendance"}
              </Button>
            </>
          )}
        </Box>
      </Box>

      {!isMobile && viewMode === "table" ? (
        <TableContainer
          component={Paper}
          variant="outlined"
          sx={{ maxHeight: "62vh", overflow: "auto" }}
        >
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                <TableCell
                  sx={{
                    position: "sticky",
                    left: 0,
                    zIndex: 4,
                    backgroundColor: "background.paper",
                    width: 72,
                    minWidth: 72,
                  }}
                >
                  Sl.No
                </TableCell>
                <TableCell
                  sx={{
                    position: "sticky",
                    left: 72,
                    zIndex: 3,
                    backgroundColor: "background.paper",
                    minWidth: 140,
                  }}
                >
                  Student No
                </TableCell>
                <TableCell>Name</TableCell>
                <TableCell>Batch</TableCell>
                <TableCell>Mobile</TableCell>
                <TableCell>Email</TableCell>
                <TableCell align="center" sx={{ whiteSpace: "nowrap" }}>
                  Present
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {sortedStudents.map((s, idx) => (
                <TableRow key={s.studentNumber} hover>
                  <TableCell
                    sx={{
                      position: "sticky",
                      left: 0,
                      zIndex: 3,
                      backgroundColor: "background.paper",
                      width: 72,
                      minWidth: 72,
                    }}
                  >
                    {(pageNumber - 1) * pageSize + idx + 1}
                  </TableCell>
                  <TableCell
                    sx={{
                      position: "sticky",
                      left: 72,
                      zIndex: 2,
                      backgroundColor: "background.paper",
                      minWidth: 140,
                    }}
                  >
                    {s.studentNumber}
                  </TableCell>
                  <TableCell>{s.name}</TableCell>
                  <TableCell>{s.batch ?? "-"}</TableCell>
                  <TableCell>{s.mobile || "-"}</TableCell>
                  <TableCell>{s.email || "-"}</TableCell>
                  <TableCell align="center">
                    <Checkbox
                      checked={resolveStatus(s, statusMap) === 1}
                      onChange={(e) =>
                        setStatusMap((prev) => ({
                          ...prev,
                          [s.studentNumber]: e.target.checked ? 1 : 0,
                        }))
                      }
                      disabled={isLocked}
                      slotProps={{ input: { "aria-label": `Present for ${s.studentNumber}` } }}
                      size="small"
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      ) : (
        sortedStudents.map((s, idx) => (
          <Card key={s.studentNumber}>
            <CardContent sx={{ pb: "16px !important" }}>
              <Stack spacing={1}>
                <Box
                  sx={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    gap: 1,
                    flexWrap: "wrap",
                  }}
                >
                  <Typography variant="subtitle2" sx={{ flex: "1 1 auto", minWidth: 0 }} noWrap>
                    Sl.No: {(pageNumber - 1) * pageSize + idx + 1}
                  </Typography>
                  <FormControlLabel
                    sx={{ mr: 0, ml: 0 }}
                    control={
                      <Checkbox
                        checked={resolveStatus(s, statusMap) === 1}
                        onChange={(e) =>
                          setStatusMap((prev) => ({
                            ...prev,
                            [s.studentNumber]: e.target.checked ? 1 : 0,
                          }))
                        }
                        disabled={isLocked}
                        size="small"
                      />
                    }
                    label="Present"
                  />
                </Box>

                <Typography variant="body2">
                  <strong>Student No:</strong> {s.studentNumber}
                </Typography>
                <Typography variant="body2">
                  <strong>Name:</strong> {s.name}
                </Typography>
                <Typography variant="body2">
                  <strong>Batch:</strong> {s.batch ?? "-"}
                </Typography>
                <Typography variant="body2">
                  <strong>Mobile:</strong> {s.mobile || "-"}
                </Typography>
                {!isMobile && (
                  <Typography variant="body2">
                    <strong>Email:</strong> {s.email || "-"}
                  </Typography>
                )}
              </Stack>
            </CardContent>
          </Card>
        ))
      )}

      {hasMore && (
        <Button variant="outlined" onClick={handleLoadMore} disabled={loadingStudents}>
          {loadingStudents ? "Loading..." : "Load more"}
        </Button>
      )}

      {isMobile && (
        <Box
          sx={{
            position: "fixed",
            left: 8,
            right: 8,
            zIndex: 1202,
            bottom: `max(52px, calc(8px + env(safe-area-inset-bottom, 0px)))`,
            backgroundColor: "background.paper",
            border: 1,
            borderColor: "divider",
            borderRadius: 2,
            p: 1,
            boxShadow: 3,
          }}
        >
          <Box sx={{ display: "flex", gap: 1, mb: 1 }}>
            <Button
              fullWidth
              size="small"
              variant="outlined"
              onClick={() => void setAllStatuses(1)}
              disabled={!canSave || bulkUpdating}
            >
              All present
            </Button>
            <Button
              fullWidth
              size="small"
              variant="outlined"
              onClick={() => void setAllStatuses(0)}
              disabled={!canSave || bulkUpdating}
            >
              All absent
            </Button>
          </Box>
          <Button
            fullWidth
            variant="contained"
            onClick={() => void handleSave()}
            disabled={!canSave || bulkUpdating}
          >
            {saving ? "Saving..." : alreadyMarked ? "Update attendance" : "Save attendance"}
          </Button>
        </Box>
      )}
    </Stack>
  );
};

export default AttendanceMarking;


import { Fragment, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  MenuItem,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
} from "@mui/material";
import {
  getCourses,
  getGroups,
  getSemesters,
  getSubjects,
  type CourseDto,
  type GroupDto,
  type SemesterDto,
  type SubjectDto,
} from "../services/attendanceService";
import {
  getMonthlyStudentReport,
  getStudentReport,
  getSubjectReport,
  type StudentAttendanceRow,
  type SubjectAttendanceRow,
} from "../services/reportsService";

const MONTHS = [
  { value: 1, label: "January" },
  { value: 2, label: "February" },
  { value: 3, label: "March" },
  { value: 4, label: "April" },
  { value: 5, label: "May" },
  { value: 6, label: "June" },
  { value: 7, label: "July" },
  { value: 8, label: "August" },
  { value: 9, label: "September" },
  { value: 10, label: "October" },
  { value: 11, label: "November" },
  { value: 12, label: "December" },
];

const pct = (n: number) => (Number.isFinite(n) ? n.toFixed(2) : "0.00");

/** Full-width table with explicit column shares so the first column does not swallow the row. */
const reportTableSx = {
  tableLayout: "fixed",
  width: "100%",
} as const;

const metricHeadSx = { whiteSpace: "nowrap" as const };
const metricCellSx = { whiteSpace: "nowrap" as const };

/** First column padding; width comes from `<colgroup>` + `table-layout: fixed`. */
const reportFirstColSx = { pr: 2, wordBreak: "break-word" as const };

const ReportColGroup4 = () => (
  <colgroup>
    <col style={{ width: "42%" }} />
    <col style={{ width: "16%" }} />
    <col style={{ width: "22%" }} />
    <col style={{ width: "20%" }} />
  </colgroup>
);

/** Row-level label from aggregate present/total counts (not a single session flag). */
const attendanceOutcome = (present: number, total: number): string => {
  if (total <= 0) return "—";
  if (present >= total) return "Present";
  return "Absent";
};

/** CSS Grid avoids full-width `<Table>` column stretching in the By student tab. */
const studentReportGridSx = {
  display: "grid",
  gridTemplateColumns: "minmax(0, 1fr) auto auto auto",
  columnGap: 2,
  rowGap: 1.5,
  alignItems: "center",
  width: "100%",
  minWidth: 0,
} as const;

const StudentReportGrid = ({
  rows,
  emptyMessage,
  getRowKey,
}: {
  rows: StudentAttendanceRow[];
  emptyMessage: string;
  getRowKey: (r: StudentAttendanceRow, index: number) => string;
}) => {
  if (rows.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
        {emptyMessage}
      </Typography>
    );
  }

  return (
    <Box sx={{ mt: 2, ...studentReportGridSx }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 600, minWidth: 0 }}>
        Subject
      </Typography>
      <Typography variant="subtitle2" sx={{ fontWeight: 600, textAlign: "right", whiteSpace: "nowrap" }}>
        Sessions
      </Typography>
      <Typography variant="subtitle2" sx={{ fontWeight: 600, whiteSpace: "nowrap" }}>
        Outcome
      </Typography>
      <Typography variant="subtitle2" sx={{ fontWeight: 600, textAlign: "right", whiteSpace: "nowrap" }}>
        %
      </Typography>
      {rows.map((r, i) => (
        <Fragment key={getRowKey(r, i)}>
          <Typography variant="body2" sx={{ wordBreak: "break-word", minWidth: 0 }}>
            {r.subject}
          </Typography>
          <Typography variant="body2" sx={{ textAlign: "right", whiteSpace: "nowrap" }}>
            {r.total}
          </Typography>
          <Typography variant="body2" sx={{ whiteSpace: "nowrap" }}>
            {attendanceOutcome(r.present, r.total)}
          </Typography>
          <Typography variant="body2" sx={{ textAlign: "right", whiteSpace: "nowrap" }}>
            {pct(r.percentage)}
          </Typography>
        </Fragment>
      ))}
    </Box>
  );
};

const ReportsPage = () => {
  const [tab, setTab] = useState(0);

  const [courses, setCourses] = useState<CourseDto[]>([]);
  const [groups, setGroups] = useState<GroupDto[]>([]);
  const [semesters, setSemesters] = useState<SemesterDto[]>([]);
  const [subjects, setSubjects] = useState<SubjectDto[]>([]);
  const [metaLoading, setMetaLoading] = useState(true);

  const [courseId, setCourseId] = useState(0);
  const [groupId, setGroupId] = useState(0);
  const [semesterId, setSemesterId] = useState(0);
  const [subjectId, setSubjectId] = useState(0);

  const [studentNumber, setStudentNumber] = useState("");
  const [studentRows, setStudentRows] = useState<StudentAttendanceRow[] | null>(null);
  const [studentLoading, setStudentLoading] = useState(false);
  const [studentError, setStudentError] = useState<string | null>(null);

  const [month, setMonth] = useState(() => new Date().getMonth() + 1);
  const [year, setYear] = useState(() => new Date().getFullYear());
  const [monthlyRows, setMonthlyRows] = useState<StudentAttendanceRow[] | null>(null);
  const [monthlyLoading, setMonthlyLoading] = useState(false);
  const [monthlyError, setMonthlyError] = useState<string | null>(null);

  const [subjectRows, setSubjectRows] = useState<SubjectAttendanceRow[] | null>(null);
  const [subjectLoading, setSubjectLoading] = useState(false);
  const [subjectError, setSubjectError] = useState<string | null>(null);

  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      setMetaLoading(true);
      setError(null);
      try {
        const [cRes, sRes] = await Promise.all([getCourses(), getSemesters()]);
        setCourses(cRes.data);
        setSemesters(sRes.data);
      } catch {
        setError("Failed to load courses and semesters.");
      } finally {
        setMetaLoading(false);
      }
    };
    void load();
  }, []);

  useEffect(() => {
    if (!courseId) {
      setGroups([]);
      setGroupId(0);
      return;
    }
    void (async () => {
      try {
        const res = await getGroups(courseId);
        setGroups(res.data);
      } catch {
        setGroups([]);
      }
    })();
  }, [courseId]);

  useEffect(() => {
    if (!courseId || !groupId || !semesterId) {
      setSubjects([]);
      setSubjectId(0);
      return;
    }
    void (async () => {
      try {
        const res = await getSubjects(courseId, groupId, semesterId);
        setSubjects(res.data);
      } catch {
        setSubjects([]);
      }
    })();
  }, [courseId, groupId, semesterId]);

  const yearOptions = (() => {
    const y = new Date().getFullYear();
    return [y - 2, y - 1, y, y + 1];
  })();

  const runStudentReport = async () => {
    const sn = studentNumber.trim();
    if (!sn) {
      setStudentError("Enter a student number.");
      return;
    }
    setStudentLoading(true);
    setStudentError(null);
    setStudentRows(null);
    try {
      const res = await getStudentReport(sn);
      setStudentRows(res.data);
    } catch (e) {
      const status = (e as { response?: { status?: number } }).response?.status;
      if (status === 404) setStudentError("Student not found.");
      else setStudentError("Could not load the report.");
    } finally {
      setStudentLoading(false);
    }
  };

  const runMonthlyReport = async () => {
    const sn = studentNumber.trim();
    if (!sn) {
      setMonthlyError("Enter a student number.");
      return;
    }
    setMonthlyLoading(true);
    setMonthlyError(null);
    setMonthlyRows(null);
    try {
      const res = await getMonthlyStudentReport(sn, month, year);
      setMonthlyRows(res.data);
    } catch (e) {
      const status = (e as { response?: { status?: number } }).response?.status;
      if (status === 404) setMonthlyError("Student not found.");
      else setMonthlyError("Could not load the monthly report.");
    } finally {
      setMonthlyLoading(false);
    }
  };

  const runSubjectReport = async () => {
    if (!subjectId) {
      setSubjectError("Select course, group, semester and subject.");
      return;
    }
    setSubjectLoading(true);
    setSubjectError(null);
    setSubjectRows(null);
    try {
      const res = await getSubjectReport(subjectId);
      setSubjectRows(res.data);
    } catch {
      setSubjectError("Could not load the subject report.");
    } finally {
      setSubjectLoading(false);
    }
  };

  return (
    <Stack spacing={3}>
      <Typography variant="h4">Reports</Typography>
      <Typography variant="body2" color="text.secondary">
        View attendance summaries by student or by subject. Data is scoped to your institution.
      </Typography>

      {error && <Alert severity="error">{error}</Alert>}

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ borderBottom: 1, borderColor: "divider" }}>
        <Tab label="By student" />
        <Tab label="By subject" />
      </Tabs>

      {tab === 0 && (
        <Stack spacing={3}>
          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Student — all subjects
              </Typography>
              <Stack
                sx={{
                  flexDirection: { xs: "column", sm: "row" },
                  alignItems: { xs: "stretch", sm: "center" },
                  gap: 2,
                  flexWrap: "wrap",
                }}
              >
                <TextField
                  label="Student number"
                  value={studentNumber}
                  onChange={(e) => setStudentNumber(e.target.value)}
                  size="small"
                  sx={{ minWidth: { xs: "100%", sm: 240 }, flex: { sm: "1 1 auto" }, maxWidth: { sm: 420 } }}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") void runStudentReport();
                  }}
                />
                <Button
                  variant="contained"
                  onClick={() => void runStudentReport()}
                  disabled={studentLoading}
                  sx={{ alignSelf: { xs: "stretch", sm: "center" }, flexShrink: 0, px: 2.5 }}
                >
                  Run report
                </Button>
              </Stack>
              {studentError && (
                <Alert severity="warning" sx={{ mt: 2 }}>
                  {studentError}
                </Alert>
              )}
              {studentLoading ? (
                <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
                  <CircularProgress size={32} />
                </Box>
              ) : studentRows ? (
                <StudentReportGrid
                  rows={studentRows}
                  emptyMessage="No attendance records found."
                  getRowKey={(r) => r.subject}
                />
              ) : (
                !studentError && (
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
                    Enter a student number and choose Run report to load the table.
                  </Typography>
                )
              )}
            </CardContent>
          </Card>

          <Card variant="outlined">
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Student — by month
              </Typography>
              <Stack
                sx={{
                  flexDirection: { xs: "column", sm: "row" },
                  alignItems: { xs: "stretch", sm: "center" },
                  gap: 2,
                  flexWrap: "wrap",
                }}
              >
                <TextField
                  select
                  label="Month"
                  value={month}
                  onChange={(e) => setMonth(Number(e.target.value))}
                  size="small"
                  sx={{ minWidth: { xs: "100%", sm: 170 }, flex: { sm: "0 1 auto" } }}
                >
                  {MONTHS.map((m) => (
                    <MenuItem key={m.value} value={m.value}>
                      {m.label}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  select
                  label="Year"
                  value={year}
                  onChange={(e) => setYear(Number(e.target.value))}
                  size="small"
                  sx={{ minWidth: { xs: "100%", sm: 130 }, flex: { sm: "0 1 auto" } }}
                >
                  {yearOptions.map((y) => (
                    <MenuItem key={y} value={y}>
                      {y}
                    </MenuItem>
                  ))}
                </TextField>
                <Button
                  variant="outlined"
                  onClick={() => void runMonthlyReport()}
                  disabled={monthlyLoading}
                  sx={{ alignSelf: { xs: "stretch", sm: "center" }, flexShrink: 0, px: 2.5 }}
                >
                  Load monthly
                </Button>
              </Stack>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 1 }}>
                Uses the same student number as above. Month filters by stored attendance date (UTC components).
              </Typography>
              {monthlyError && (
                <Alert severity="warning" sx={{ mt: 2 }}>
                  {monthlyError}
                </Alert>
              )}
              {monthlyLoading ? (
                <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
                  <CircularProgress size={32} />
                </Box>
              ) : monthlyRows ? (
                <StudentReportGrid
                  rows={monthlyRows}
                  emptyMessage="No records for this month."
                  getRowKey={(r, i) => `${r.subject}-${month}-${year}-${i}`}
                />
              ) : (
                !monthlyError && (
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
                    Enter a student number above, then Load monthly to see this table.
                  </Typography>
                )
              )}
            </CardContent>
          </Card>
        </Stack>
      )}

      {tab === 1 && (
        <Card variant="outlined">
          <CardContent>
            <Typography variant="h6" gutterBottom>
              Subject — all students
            </Typography>
            {metaLoading ? (
              <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
                <CircularProgress />
              </Box>
            ) : (
              <Stack spacing={2}>
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" },
                    gap: 2,
                  }}
                >
                  <TextField
                    select
                    label="Course"
                    value={courseId || ""}
                    onChange={(e) => {
                      setCourseId(Number(e.target.value));
                      setGroupId(0);
                      setSemesterId(0);
                      setSubjectId(0);
                    }}
                    size="small"
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
                    value={groupId || ""}
                    onChange={(e) => {
                      setGroupId(Number(e.target.value));
                      setSemesterId(0);
                      setSubjectId(0);
                    }}
                    size="small"
                    disabled={!courseId}
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
                    value={semesterId || ""}
                    onChange={(e) => {
                      setSemesterId(Number(e.target.value));
                      setSubjectId(0);
                    }}
                    size="small"
                    disabled={!courseId}
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
                    value={subjectId || ""}
                    onChange={(e) => setSubjectId(Number(e.target.value))}
                    size="small"
                    disabled={!courseId || !groupId || !semesterId}
                  >
                    <MenuItem value={0}>Select subject</MenuItem>
                    {subjects.map((s) => (
                      <MenuItem key={s.id} value={s.id}>
                        {s.name}
                        {s.isElective ? " (elective)" : ""}
                      </MenuItem>
                    ))}
                  </TextField>
                </Box>
                <Button
                  variant="contained"
                  onClick={() => void runSubjectReport()}
                  disabled={subjectLoading || !subjectId}
                  sx={{ alignSelf: "flex-start" }}
                >
                  Run report
                </Button>
                {subjectError && <Alert severity="warning">{subjectError}</Alert>}
                {subjectLoading ? (
                  <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
                    <CircularProgress size={32} />
                  </Box>
                ) : subjectRows ? (
                  <TableContainer sx={{ width: "100%" }}>
                    <Table size="small" sx={reportTableSx}>
                      <ReportColGroup4 />
                      <TableHead>
                        <TableRow>
                          <TableCell>Student number</TableCell>
                          <TableCell align="right" sx={metricHeadSx}>
                            Sessions
                          </TableCell>
                          <TableCell sx={metricHeadSx}>Outcome</TableCell>
                          <TableCell align="right" sx={metricHeadSx}>
                            %
                          </TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {subjectRows.length === 0 ? (
                          <TableRow>
                            <TableCell colSpan={4}>
                              <Typography variant="body2" color="text.secondary">
                                No attendance for this subject.
                              </Typography>
                            </TableCell>
                          </TableRow>
                        ) : (
                          subjectRows.map((r) => (
                            <TableRow key={r.student}>
                              <TableCell sx={reportFirstColSx}>{r.student}</TableCell>
                              <TableCell align="right" sx={metricCellSx}>
                                {r.total}
                              </TableCell>
                              <TableCell sx={metricCellSx}>{attendanceOutcome(r.present, r.total)}</TableCell>
                              <TableCell align="right" sx={metricCellSx}>
                                {pct(r.percentage)}
                              </TableCell>
                            </TableRow>
                          ))
                        )}
                      </TableBody>
                    </Table>
                  </TableContainer>
                ) : null}
              </Stack>
            )}
          </CardContent>
        </Card>
      )}
    </Stack>
  );
};

export default ReportsPage;

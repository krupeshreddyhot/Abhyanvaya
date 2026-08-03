import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  FormControl,
  Grid,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  getTimetableDashboard,
  listAcademicYears,
  type TimetableDashboardDto,
} from "../../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";

const CHART_COLORS = ["#1976d2", "#2e7d32", "#ed6c02", "#9c27b0", "#0288d1"];

const TimetableDashboardPage = () => {
  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [yearId, setYearId] = useState<number | "">("");
  const [data, setData] = useState<TimetableDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void listAcademicYears().then((res) => {
      setYears(res.data.map((y) => ({ id: y.id, label: `${y.code} — ${y.name}` })));
      const current = res.data.find((y) => y.isCurrent) ?? res.data[0];
      if (current) setYearId(current.id);
    });
  }, []);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await getTimetableDashboard(yearId === "" ? undefined : yearId);
        setData(res.data);
      } catch (e) {
        setError(errMsg(e));
      } finally {
        setLoading(false);
      }
    })();
  }, [yearId]);

  const statCards = data
    ? [
        { label: "Draft timetables", value: data.draftTimetableCount },
        { label: "Locked timetables", value: data.lockedCount },
        { label: "Scheduled periods", value: data.scheduledPeriodCount },
        { label: "Departments with timetable", value: data.departmentsWithTimetable },
        { label: "Faculty scheduled", value: data.facultyScheduledCount },
        { label: "Rooms scheduled", value: data.roomsScheduledCount },
      ]
    : [];

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Timetable dashboard
        </Typography>
        <FormControl size="small" sx={{ minWidth: 200 }}>
          <InputLabel>Academic year</InputLabel>
          <Select
            label="Academic year"
            value={yearId}
            onChange={(e) => setYearId(parseOptionalSelectNumber(e.target.value))}
          >
            <MenuItem value="">All years</MenuItem>
            {years.map((y) => (
              <MenuItem key={y.id} value={y.id}>
                {y.label}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
          <CircularProgress />
        </Box>
      ) : data ? (
        <>
          <Grid container spacing={2}>
            {statCards.map((c) => (
              <Grid key={c.label} size={{ xs: 12, sm: 6, md: 4 }}>
                <Card variant="outlined">
                  <CardContent>
                    <Typography variant="body2" color="text.secondary">
                      {c.label}
                    </Typography>
                    <Typography variant="h4">{c.value}</Typography>
                  </CardContent>
                </Card>
              </Grid>
            ))}
          </Grid>

          <Grid container spacing={2}>
            <Grid size={{ xs: 12, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Daily distribution
                  </Typography>
                  <ResponsiveContainer width="100%" height={260}>
                    <BarChart data={data.dailyDistribution}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill={CHART_COLORS[0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Faculty load
                  </Typography>
                  <ResponsiveContainer width="100%" height={260}>
                    <BarChart data={data.facultyLoad.slice(0, 12)}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" angle={-30} textAnchor="end" height={60} fontSize={10} interval={0} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Legend />
                      <Bar dataKey="count" name="Periods" fill={CHART_COLORS[1]} />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Room usage
                  </Typography>
                  <ResponsiveContainer width="100%" height={260}>
                    <BarChart data={data.roomUsage.slice(0, 12)}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" angle={-30} textAnchor="end" height={60} fontSize={10} interval={0} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill={CHART_COLORS[2]} />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
          </Grid>
        </>
      ) : null}
    </Stack>
  );
};

export default TimetableDashboardPage;

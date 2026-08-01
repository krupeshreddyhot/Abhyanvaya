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
  Line,
  LineChart,
  Pie,
  PieChart,
  Cell,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  getGovernanceDashboard,
  listAcademicYears,
  type TimetableGovernanceDashboardDto,
} from "../../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";

const CHART_COLORS = ["#1976d2", "#2e7d32", "#ed6c02"];

const GovernanceDashboardPage = () => {
  const [years, setYears] = useState<{ id: number; label: string }[]>([]);
  const [yearId, setYearId] = useState<number | "">("");
  const [data, setData] = useState<TimetableGovernanceDashboardDto | null>(null);
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
        const res = await getGovernanceDashboard(yearId === "" ? undefined : yearId);
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
        { label: "Draft versions", value: data.draftVersionCount },
        { label: "Published versions", value: data.publishedVersionCount },
        { label: "Approval queue", value: data.approvalQueueCount },
        { label: "Pending reviews", value: data.pendingReviewsCount },
        { label: "Soft warnings", value: data.softWarningCount },
        { label: "Recently published", value: data.recentlyPublishedCount },
        { label: "Archived versions", value: data.archivedVersionCount },
        { label: "Recent changes", value: data.recentChangesCount },
        { label: "Frozen timetables", value: data.frozenTimetableCount ?? 0 },
        { label: "Archived timetables", value: data.archivedTimetableCount ?? 0 },
      ]
    : [];

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Governance dashboard
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
              <Grid key={c.label} size={{ xs: 12, sm: 6, md: 3 }}>
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
                    Approval trend
                  </Typography>
                  <ResponsiveContainer width="100%" height={260}>
                    <LineChart data={data.approvalTrend}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Line type="monotone" dataKey="count" stroke={CHART_COLORS[0]} strokeWidth={2} />
                    </LineChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Version growth
                  </Typography>
                  <ResponsiveContainer width="100%" height={260}>
                    <BarChart data={data.versionGrowth}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill={CHART_COLORS[1]} />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 4 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Publishing history
                  </Typography>
                  <ResponsiveContainer width="100%" height={260}>
                    <BarChart data={data.publishingHistory}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill={CHART_COLORS[2]} />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          <Grid container spacing={2}>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Archive reason distribution
                  </Typography>
                  <ResponsiveContainer width="100%" height={260}>
                    <PieChart>
                      <Pie
                        data={data.archiveReasonDistribution ?? []}
                        dataKey="count"
                        nameKey="name"
                        outerRadius={90}
                        label
                      >
                        {(data.archiveReasonDistribution ?? []).map((_, i) => (
                          <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                        ))}
                      </Pie>
                      <Tooltip />
                    </PieChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Latest archives
                  </Typography>
                  <Stack spacing={1}>
                    {(data.latestArchives ?? []).length === 0 && (
                      <Typography variant="body2" color="text.secondary">
                        No archives yet.
                      </Typography>
                    )}
                    {(data.latestArchives ?? []).map((a) => (
                      <Box key={a.timetableId} sx={{ borderBottom: 1, borderColor: "divider", pb: 1 }}>
                        <Typography variant="subtitle2">{a.timetableName}</Typography>
                        <Typography variant="caption" color="text.secondary" display="block">
                          {a.archiveReasonName ?? "Unspecified"}
                          {a.archivedDate ? ` · ${new Date(a.archivedDate).toLocaleString()}` : ""}
                        </Typography>
                        {a.comments && (
                          <Typography variant="caption" color="text.secondary">
                            {a.comments}
                          </Typography>
                        )}
                      </Box>
                    ))}
                  </Stack>
                </CardContent>
              </Card>
            </Grid>
          </Grid>
        </>
      ) : null}
    </Stack>
  );
};

export default GovernanceDashboardPage;

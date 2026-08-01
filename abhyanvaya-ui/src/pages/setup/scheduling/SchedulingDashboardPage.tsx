import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Grid,
  Stack,
  Typography,
} from "@mui/material";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Legend,
} from "recharts";
import { getSchedulingDashboard, type SchedulingDashboardDto } from "../../../services/schedulingService";
import { errMsg } from "./schedulingFormUtils";

const CHART_COLORS = ["#1976d2", "#2e7d32", "#ed6c02", "#9c27b0", "#0288d1", "#d32f2f"];

const SchedulingDashboardPage = () => {
  const [data, setData] = useState<SchedulingDashboardDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await getSchedulingDashboard();
        setData(res.data);
      } catch (e) {
        setError(errMsg(e));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const facilityData = data
    ? [
        { name: "Campuses", value: data.campusCount },
        { name: "Buildings", value: data.buildingCount },
        { name: "Rooms", value: data.roomCount },
      ]
    : [];

  const allocationData = data
    ? [
        { name: "Subject allocations", count: data.subjectAllocationCount },
        { name: "Faculty workloads", count: data.facultyWorkloadCount },
        { name: "Room rules", count: data.roomRuleCount },
        { name: "Time slot sets", count: data.timeSlotSetCount },
        { name: "Holidays", count: data.holidayCount },
        { name: "Academic years", count: data.academicYearCount },
      ]
    : [];

  const phase1aData = data
    ? [
        { name: "Faculty availability", count: data.facultyAvailabilityCount },
        { name: "Room availability", count: data.roomAvailabilityCount },
        { name: "Subject categories", count: data.subjectCategoryCount },
        { name: "Time slot templates", count: data.timeSlotTemplateCount },
      ]
    : [];

  const phase1bData = data
    ? [
        { name: "Faculty preferences", count: data.facultyPreferenceCount },
        { name: "Room features", count: data.roomFeatureCount },
        { name: "Feature assignments", count: data.roomFeatureAssignmentCount },
        { name: "Delivery types", count: data.subjectDeliveryTypeCount },
        { name: "Holiday types", count: data.holidayTypeCatalogCount },
      ]
    : [];

  const holidayDistribution = data?.holidayDistribution ?? [];
  const deliveryTypeDistribution = data?.deliveryTypeDistribution ?? [];

  const healthMetrics = data
    ? [
        { name: "Faculty unavailable", count: data.facultyUnavailableCount },
        { name: "Rooms blocked", count: data.roomsBlockedCount },
        { name: "Subjects missing category", count: data.subjectsMissingCategoryCount },
        { name: "Unused templates", count: data.unusedTemplateCount },
        { name: "Depts without allocation", count: data.departmentsWithoutAllocationCount },
        { name: "Missing preferences", count: data.missingFacultyPreferencesCount },
        { name: "Rooms without features", count: data.roomsWithoutFeaturesCount },
      ]
    : [];

  const healthAlerts = data
    ? [
        { label: "Faculty unavailable entries", count: data.facultyUnavailableCount, severity: "warning" as const },
        { label: "Rooms blocked", count: data.roomsBlockedCount, severity: "warning" as const },
        { label: "Subjects missing category", count: data.subjectsMissingCategoryCount, severity: "error" as const },
        { label: "Unused time slot templates", count: data.unusedTemplateCount, severity: "info" as const },
        { label: "Departments without allocations", count: data.departmentsWithoutAllocationCount, severity: "warning" as const },
        { label: "Faculty missing teaching preferences", count: data.missingFacultyPreferencesCount, severity: "warning" as const },
        {
          label: "Rooms without feature assignments",
          count: data.roomsWithoutFeaturesCount,
          severity: data.roomFeatureCoveragePercent < 80 ? ("error" as const) : ("warning" as const),
        },
      ].filter((a) => a.count > 0)
    : [];

  const statCards = data
    ? [
        { label: "Total weekly hours", value: data.totalWeeklyHours.toFixed(1) },
        { label: "Total room capacity", value: String(data.totalRoomCapacity) },
        { label: "Subjects (catalog)", value: String(data.subjectCount) },
        { label: "Faculty (staff)", value: String(data.facultyCount) },
        { label: "Faculty preferences", value: String(data.facultyPreferenceCount) },
        { label: "Room feature coverage", value: `${data.roomFeatureCoveragePercent.toFixed(0)}%` },
        { label: "Rooms with features", value: String(data.roomsWithFeaturesCount) },
        { label: "Holiday type catalog", value: String(data.holidayTypeCatalogCount) },
      ]
    : [];

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Scheduling dashboard
        </Typography>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      {healthAlerts.length > 0 && (
        <Stack spacing={1}>
          {healthAlerts.map((a) => (
            <Alert key={a.label} severity={a.severity} icon={<WarningAmberIcon />}>
              {a.count} {a.label.toLowerCase()} — review scheduling setup.
            </Alert>
          ))}
        </Stack>
      )}

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
            <Grid size={{ xs: 12, md: 6 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Facilities
                  </Typography>
                  <ResponsiveContainer width="100%" height={280}>
                    <PieChart>
                      <Pie data={facilityData} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={90} label>
                        {facilityData.map((_, i) => (
                          <Cell key={i} fill={CHART_COLORS[i % CHART_COLORS.length]} />
                        ))}
                      </Pie>
                      <Tooltip />
                      <Legend />
                    </PieChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Scheduling records
                  </Typography>
                  <ResponsiveContainer width="100%" height={280}>
                    <BarChart data={allocationData} margin={{ top: 8, right: 8, left: 0, bottom: 40 }}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" angle={-25} textAnchor="end" height={70} interval={0} fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill="#1976d2" />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Phase 1A foundation
                  </Typography>
                  <ResponsiveContainer width="100%" height={280}>
                    <BarChart data={phase1aData} margin={{ top: 8, right: 8, left: 0, bottom: 40 }}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" angle={-25} textAnchor="end" height={70} interval={0} fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill="#2e7d32" />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Phase 1B foundation
                  </Typography>
                  <ResponsiveContainer width="100%" height={280}>
                    <BarChart data={phase1bData} margin={{ top: 8, right: 8, left: 0, bottom: 40 }}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" angle={-25} textAnchor="end" height={70} interval={0} fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill="#9c27b0" />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Holiday distribution
                  </Typography>
                  <ResponsiveContainer width="100%" height={280}>
                    <BarChart data={holidayDistribution} margin={{ top: 8, right: 8, left: 0, bottom: 40 }}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" angle={-25} textAnchor="end" height={70} interval={0} fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill="#0288d1" />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Delivery type distribution
                  </Typography>
                  <ResponsiveContainer width="100%" height={280}>
                    <BarChart data={deliveryTypeDistribution} margin={{ top: 8, right: 8, left: 0, bottom: 40 }}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" angle={-25} textAnchor="end" height={70} interval={0} fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill="#ed6c02" />
                    </BarChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, md: 6 }}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Health indicators
                  </Typography>
                  <ResponsiveContainer width="100%" height={280}>
                    <BarChart data={healthMetrics} margin={{ top: 8, right: 8, left: 0, bottom: 40 }}>
                      <CartesianGrid strokeDasharray="3 3" />
                      <XAxis dataKey="name" angle={-25} textAnchor="end" height={70} interval={0} fontSize={11} />
                      <YAxis allowDecimals={false} />
                      <Tooltip />
                      <Bar dataKey="count" fill="#ed6c02" />
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

export default SchedulingDashboardPage;

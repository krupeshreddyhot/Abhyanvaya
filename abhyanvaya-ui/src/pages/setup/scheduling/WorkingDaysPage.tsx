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
  Switch,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import {
  listAcademicYears,
  listWorkingDays,
  upsertWorkingDay,
  type AcademicYearDto,
  type WorkingDayDto,
} from "../../../services/schedulingService";
import { DAY_LABELS, errMsg, WEEKDAY_ORDER } from "./schedulingFormUtils";

const WorkingDaysPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [years, setYears] = useState<AcademicYearDto[]>([]);
  const [yearId, setYearId] = useState<number>(0);
  const [days, setDays] = useState<WorkingDayDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const res = await listAcademicYears();
        setYears(res.data);
        const current = res.data.find((y) => y.isCurrent) ?? res.data[0];
        if (current) setYearId(current.id);
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const loadDays = useCallback(async () => {
    if (!yearId) {
      setDays([]);
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await listWorkingDays(yearId);
      setDays(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [yearId]);

  useEffect(() => {
    void loadDays();
  }, [loadDays]);

  const isWorking = (dow: number) => days.find((d) => d.dayOfWeek === dow)?.isWorking ?? (dow >= 1 && dow <= 5);

  const toggleDay = async (dow: number) => {
    if (!canManage || !yearId) return;
    const existing = days.find((d) => d.dayOfWeek === dow);
    const next = !isWorking(dow);
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      await upsertWorkingDay({
        id: existing?.id ?? null,
        academicYearId: yearId,
        dayOfWeek: dow,
        isWorking: next,
      });
      setMessage(`${DAY_LABELS[dow]} updated.`);
      await loadDays();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Working days
        </Typography>
      </Box>

      <FormControl size="small" sx={{ minWidth: 240 }}>
        <InputLabel id="year-label">Academic year</InputLabel>
        <Select labelId="year-label" label="Academic year" value={yearId || ""} onChange={(e) => setYearId(Number(e.target.value))}>
          {years.map((y) => (
            <MenuItem key={y.id} value={y.id}>
              {y.code} — {y.name}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : (
        <Stack spacing={1}>
          {WEEKDAY_ORDER.map((dow) => (
            <Box
              key={dow}
              sx={{
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                p: 1.5,
                border: 1,
                borderColor: "divider",
                borderRadius: 1,
              }}
            >
              <Typography>{DAY_LABELS[dow]}</Typography>
              <Switch
                checked={isWorking(dow)}
                onChange={() => void toggleDay(dow)}
                disabled={!canManage || saving || !yearId}
              />
            </Box>
          ))}
        </Stack>
      )}
    </Stack>
  );
};

export default WorkingDaysPage;

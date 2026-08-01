import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { useForm, Controller } from "react-hook-form";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import { listStaff } from "../../../services/setupService";
import {
  FacultyDayPreferenceType,
  deleteFacultyDayPreference,
  getFacultyWorkload,
  upsertFacultyDayPreference,
  upsertFacultyWorkload,
  type FacultyWorkloadDto,
  type UpsertFacultyWorkloadRequest,
} from "../../../services/schedulingService";
import { DAY_LABELS, errMsg, WEEKDAY_ORDER } from "./schedulingFormUtils";

const FacultyWorkloadPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [staffOptions, setStaffOptions] = useState<{ id: number; label: string }[]>([]);
  const [staffId, setStaffId] = useState<number>(0);
  const [workload, setWorkload] = useState<FacultyWorkloadDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const form = useForm<UpsertFacultyWorkloadRequest>({
    defaultValues: {
      staffId: 0,
      maxPeriodsPerDay: 4,
      maxPeriodsPerWeek: 20,
      teachingLoadHours: 0,
      labLoadHours: 0,
      mentoringLoadHours: 0,
      administrativeLoadHours: 0,
      isGuestFaculty: false,
      isAdjunctFaculty: false,
      notes: "",
    },
  });

  useEffect(() => {
    void (async () => {
      try {
        const res = await listStaff({ page: 1, pageSize: 500 });
        const opts = res.data.items.map((s) => ({
          id: s.id,
          label: `${s.staffCode ?? s.id} — ${s.firstName} ${s.lastName}`,
        }));
        setStaffOptions(opts);
        if (opts[0]) setStaffId(opts[0].id);
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  useEffect(() => {
    if (!staffId) return;
    void (async () => {
      setLoading(true);
      setError(null);
      try {
        const res = await getFacultyWorkload(staffId);
        setWorkload(res.data);
        form.reset({
          staffId: res.data.staffId,
          maxPeriodsPerDay: res.data.maxPeriodsPerDay,
          maxPeriodsPerWeek: res.data.maxPeriodsPerWeek,
          teachingLoadHours: res.data.teachingLoadHours,
          labLoadHours: res.data.labLoadHours,
          mentoringLoadHours: res.data.mentoringLoadHours,
          administrativeLoadHours: res.data.administrativeLoadHours,
          isGuestFaculty: res.data.isGuestFaculty,
          isAdjunctFaculty: res.data.isAdjunctFaculty,
          notes: res.data.notes ?? "",
        });
      } catch (e) {
        const status = (e as { response?: { status?: number } }).response?.status;
        if (status === 404) {
          setWorkload(null);
          form.reset({
            staffId,
            maxPeriodsPerDay: 4,
            maxPeriodsPerWeek: 20,
            teachingLoadHours: 0,
            labLoadHours: 0,
            mentoringLoadHours: 0,
            administrativeLoadHours: 0,
            isGuestFaculty: false,
            isAdjunctFaculty: false,
            notes: "",
          });
        } else {
          setError(errMsg(e));
        }
      } finally {
        setLoading(false);
      }
    })();
  }, [staffId, form]);

  const save = form.handleSubmit(async (values) => {
    if (!canManage) return;
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const res = await upsertFacultyWorkload({ ...values, staffId, notes: values.notes || null });
      setWorkload(res.data);
      setMessage("Faculty workload saved.");
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const getDayPref = (dow: number) => workload?.dayPreferences.find((d) => d.dayOfWeek === dow);

  const toggleDayPref = async (dow: number, type: FacultyDayPreferenceType) => {
    if (!canManage || !workload) return;
    const existing = getDayPref(dow);
    if (existing?.preferenceType === type) {
      await deleteFacultyDayPreference(existing.id);
    } else {
      await upsertFacultyDayPreference({
        id: existing?.id ?? null,
        facultyWorkloadId: workload.id,
        dayOfWeek: dow,
        preferenceType: type,
      });
    }
    const res = await getFacultyWorkload(staffId);
    setWorkload(res.data);
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Faculty workloads
        </Typography>
      </Box>

      <FormControl size="small" sx={{ minWidth: 280 }}>
        <InputLabel id="staff">Staff member</InputLabel>
        <Select labelId="staff" label="Staff member" value={staffId || ""} onChange={(e) => setStaffId(Number(e.target.value))}>
          {staffOptions.map((s) => (
            <MenuItem key={s.id} value={s.id}>
              {s.label}
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
        <Stack spacing={2} component="form" onSubmit={save}>
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <Controller
              name="maxPeriodsPerDay"
              control={form.control}
              render={({ field }) => (
                <TextField {...field} label="Max periods / day" type="number" fullWidth disabled={!canManage} onChange={(e) => field.onChange(Number(e.target.value))} />
              )}
            />
            <Controller
              name="maxPeriodsPerWeek"
              control={form.control}
              render={({ field }) => (
                <TextField {...field} label="Max periods / week" type="number" fullWidth disabled={!canManage} onChange={(e) => field.onChange(Number(e.target.value))} />
              )}
            />
          </Stack>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <Controller name="teachingLoadHours" control={form.control} render={({ field }) => <TextField {...field} label="Teaching load (hrs)" type="number" fullWidth disabled={!canManage} onChange={(e) => field.onChange(Number(e.target.value))} />} />
            <Controller name="labLoadHours" control={form.control} render={({ field }) => <TextField {...field} label="Lab load (hrs)" type="number" fullWidth disabled={!canManage} onChange={(e) => field.onChange(Number(e.target.value))} />} />
            <Controller name="mentoringLoadHours" control={form.control} render={({ field }) => <TextField {...field} label="Mentoring load (hrs)" type="number" fullWidth disabled={!canManage} onChange={(e) => field.onChange(Number(e.target.value))} />} />
            <Controller name="administrativeLoadHours" control={form.control} render={({ field }) => <TextField {...field} label="Admin load (hrs)" type="number" fullWidth disabled={!canManage} onChange={(e) => field.onChange(Number(e.target.value))} />} />
          </Stack>

          <Stack direction="row" spacing={2}>
            <Controller name="isGuestFaculty" control={form.control} render={({ field }) => <FormControlLabel control={<Switch checked={field.value} onChange={(_, v) => field.onChange(v)} disabled={!canManage} />} label="Guest faculty" />} />
            <Controller name="isAdjunctFaculty" control={form.control} render={({ field }) => <FormControlLabel control={<Switch checked={field.value} onChange={(_, v) => field.onChange(v)} disabled={!canManage} />} label="Adjunct faculty" />} />
          </Stack>

          <Controller name="notes" control={form.control} render={({ field }) => <TextField {...field} label="Notes" fullWidth multiline minRows={2} disabled={!canManage} />} />

          {canManage && (
            <Button type="submit" variant="contained" disabled={saving}>
              Save workload
            </Button>
          )}

          {workload && (
            <Box>
              <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1 }}>
                Day preferences
              </Typography>
              <Stack spacing={1}>
                {WEEKDAY_ORDER.map((dow) => {
                  const pref = getDayPref(dow);
                  return (
                    <Box key={dow} sx={{ display: "flex", alignItems: "center", gap: 2, flexWrap: "wrap" }}>
                      <Typography sx={{ minWidth: 100 }}>{DAY_LABELS[dow]}</Typography>
                      <Button
                        size="small"
                        variant={pref?.preferenceType === FacultyDayPreferenceType.Preferred ? "contained" : "outlined"}
                        onClick={() => void toggleDayPref(dow, FacultyDayPreferenceType.Preferred)}
                        disabled={!canManage}
                      >
                        Preferred
                      </Button>
                      <Button
                        size="small"
                        color="warning"
                        variant={pref?.preferenceType === FacultyDayPreferenceType.Unavailable ? "contained" : "outlined"}
                        onClick={() => void toggleDayPref(dow, FacultyDayPreferenceType.Unavailable)}
                        disabled={!canManage}
                      >
                        Unavailable
                      </Button>
                    </Box>
                  );
                })}
              </Stack>
            </Box>
          )}
        </Stack>
      )}
    </Stack>
  );
};

export default FacultyWorkloadPage;

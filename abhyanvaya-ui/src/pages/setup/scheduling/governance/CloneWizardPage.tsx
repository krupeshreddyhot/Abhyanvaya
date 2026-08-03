import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  FormControl,
  InputLabel,
  LinearProgress,
  MenuItem,
  Select,
  Stack,
  Step,
  StepLabel,
  Stepper,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  enqueueCloneJob,
  getCloneJob,
  listTimetables,
  TimetableCloneJobStatus,
  TimetableCloneJobType,
  type EnqueueTimetableCloneRequest,
  type TimetableCloneJobDto,
  type TimetableDto,
} from "../../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "../schedulingFormUtils";
import { CLONE_JOB_STATUS_LABELS, CLONE_JOB_TYPE_LABELS } from "./governanceEnumLabels";

const STEPS = ["Clone type", "Source timetable", "Options", "Preview", "Progress"];

const CloneWizardPage = () => {
  const [activeStep, setActiveStep] = useState(0);
  const [timetables, setTimetables] = useState<TimetableDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [jobType, setJobType] = useState<TimetableCloneJobType>(TimetableCloneJobType.Week);
  const [sourceTimetableId, setSourceTimetableId] = useState<number | "">("");
  const [targetTimetableName, setTargetTimetableName] = useState("");
  const [sourceDayOfWeek, setSourceDayOfWeek] = useState<number | "">("");
  const [targetDayOfWeek, setTargetDayOfWeek] = useState<number | "">("");
  const [departmentId, setDepartmentId] = useState<number | "">("");
  const [staffId, setStaffId] = useState<number | "">("");
  const [roomId, setRoomId] = useState<number | "">("");

  const [job, setJob] = useState<TimetableCloneJobDto | null>(null);
  const [enqueueing, setEnqueueing] = useState(false);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const res = await listTimetables();
        setTimetables(res.data);
      } catch (e) {
        setError(errMsg(e));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  useEffect(() => {
    if (!job || job.status === TimetableCloneJobStatus.Completed || job.status === TimetableCloneJobStatus.Failed) {
      return;
    }
    const timer = window.setInterval(() => {
      void getCloneJob(job.id)
        .then((res) => setJob(res.data))
        .catch(() => undefined);
    }, 1500);
    return () => window.clearInterval(timer);
  }, [job]);

  const buildRequest = (): EnqueueTimetableCloneRequest => ({
    jobType,
    sourceTimetableId: Number(sourceTimetableId),
    targetTimetableName: targetTimetableName.trim() || null,
    sourceDayOfWeek: sourceDayOfWeek === "" ? null : Number(sourceDayOfWeek),
    targetDayOfWeek: targetDayOfWeek === "" ? null : Number(targetDayOfWeek),
    departmentId: departmentId === "" ? null : Number(departmentId),
    staffId: staffId === "" ? null : Number(staffId),
    roomId: roomId === "" ? null : Number(roomId),
    executeSynchronously: true,
  });

  const handleEnqueue = async () => {
    setEnqueueing(true);
    setError(null);
    try {
      const res = await enqueueCloneJob(buildRequest());
      setJob(res.data);
      setActiveStep(4);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setEnqueueing(false);
    }
  };

  const canNext = (): boolean => {
    if (activeStep === 1) return sourceTimetableId !== "";
    return true;
  };

  const sourceTimetable = timetables.find((t) => t.id === sourceTimetableId);

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Clone wizard
        </Typography>
      </Box>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}

      <Stepper activeStep={activeStep} alternativeLabel>
        {STEPS.map((label) => (
          <Step key={label}>
            <StepLabel>{label}</StepLabel>
          </Step>
        ))}
      </Stepper>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}><CircularProgress /></Box>
      ) : (
        <Box sx={{ maxWidth: 560, mx: "auto", width: "100%" }}>
          {activeStep === 0 && (
            <FormControl fullWidth>
              <InputLabel>Clone type</InputLabel>
              <Select
                label="Clone type"
                value={jobType}
                onChange={(e) => setJobType(Number(e.target.value) as TimetableCloneJobType)}
              >
                {Object.entries(CLONE_JOB_TYPE_LABELS).map(([k, v]) => (
                  <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          {activeStep === 1 && (
            <FormControl fullWidth>
              <InputLabel>Source timetable</InputLabel>
              <Select
                label="Source timetable"
                value={sourceTimetableId}
                onChange={(e) => setSourceTimetableId(parseOptionalSelectNumber(e.target.value))}
              >
                {timetables.map((t) => (
                  <MenuItem key={t.id} value={t.id}>
                    {t.name} ({t.academicYearName ?? t.academicYearId})
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          {activeStep === 2 && (
            <Stack spacing={2}>
              <TextField
                label="Target timetable name"
                value={targetTimetableName}
                onChange={(e) => setTargetTimetableName(e.target.value)}
                fullWidth
              />
              {(jobType === TimetableCloneJobType.Day || jobType === TimetableCloneJobType.Week) && (
                <>
                  <TextField
                    label="Source day of week (1–7)"
                    type="number"
                    value={sourceDayOfWeek}
                    onChange={(e) => setSourceDayOfWeek(parseOptionalSelectNumber(e.target.value))}
                    fullWidth
                  />
                  <TextField
                    label="Target day of week (1–7)"
                    type="number"
                    value={targetDayOfWeek}
                    onChange={(e) => setTargetDayOfWeek(parseOptionalSelectNumber(e.target.value))}
                    fullWidth
                  />
                </>
              )}
              {jobType === TimetableCloneJobType.Department && (
                <TextField
                  label="Department ID"
                  type="number"
                  value={departmentId}
                  onChange={(e) => setDepartmentId(parseOptionalSelectNumber(e.target.value))}
                  fullWidth
                />
              )}
              {jobType === TimetableCloneJobType.Faculty && (
                <TextField
                  label="Staff ID"
                  type="number"
                  value={staffId}
                  onChange={(e) => setStaffId(parseOptionalSelectNumber(e.target.value))}
                  fullWidth
                />
              )}
              {jobType === TimetableCloneJobType.Room && (
                <TextField
                  label="Room ID"
                  type="number"
                  value={roomId}
                  onChange={(e) => setRoomId(parseOptionalSelectNumber(e.target.value))}
                  fullWidth
                />
              )}
            </Stack>
          )}

          {activeStep === 3 && (
            <Stack spacing={1}>
              <Typography variant="subtitle2">Summary</Typography>
              <Typography variant="body2">Type: {CLONE_JOB_TYPE_LABELS[jobType]}</Typography>
              <Typography variant="body2">
                Source: {sourceTimetable?.name ?? sourceTimetableId}
              </Typography>
              {targetTimetableName && (
                <Typography variant="body2">Target name: {targetTimetableName}</Typography>
              )}
            </Stack>
          )}

          {activeStep === 4 && job && (
            <Stack spacing={2}>
              <Typography variant="subtitle2">
                Job #{job.id} — {CLONE_JOB_STATUS_LABELS[job.status]}
              </Typography>
              <LinearProgress variant="determinate" value={job.progressPercent} />
              <Typography variant="body2" color="text.secondary">
                {job.progressPercent}% complete
              </Typography>
              {job.summary && <Alert severity="info">{job.summary}</Alert>}
              {job.error && <Alert severity="error">{job.error}</Alert>}
              {job.status === TimetableCloneJobStatus.Completed && job.targetTimetableId && (
                <Button
                  component={RouterLink}
                  to={`/setup/scheduling/timetables/${job.targetTimetableId}`}
                  variant="outlined"
                >
                  Open cloned timetable
                </Button>
              )}
            </Stack>
          )}

          <Stack direction="row" spacing={1} sx={{ mt: 3, justifyContent: "flex-end" }}>
            <Button disabled={activeStep === 0 || activeStep === 4} onClick={() => setActiveStep((s) => s - 1)}>
              Back
            </Button>
            {activeStep < 3 && (
              <Button variant="contained" disabled={!canNext()} onClick={() => setActiveStep((s) => s + 1)}>
                Next
              </Button>
            )}
            {activeStep === 3 && (
              <Button variant="contained" disabled={enqueueing} onClick={() => void handleEnqueue()}>
                Enqueue clone
              </Button>
            )}
            {activeStep === 4 && (
              <Button onClick={() => { setActiveStep(0); setJob(null); }}>Start over</Button>
            )}
          </Stack>
        </Box>
      )}
    </Stack>
  );
};

export default CloneWizardPage;

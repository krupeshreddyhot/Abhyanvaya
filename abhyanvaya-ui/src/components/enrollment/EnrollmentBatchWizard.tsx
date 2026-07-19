import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Step,
  StepLabel,
  Stepper,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import { enrollmentApiClient } from "../../api/enrollmentApiClient";
import { getTenantCollege } from "../../services/adminService";
import { listMasterCourses, listMasterGroups, listSemesters } from "../../services/setupService";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";
import { useAuth } from "../../context/AuthContext";
import type { CreateEnrollmentBatchApiRequest } from "../../types/enrollment";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";

type Props = {
  open: boolean;
  onClose: () => void;
};

const steps = ["College", "Academic Year", "Scope", "Preview", "Confirm"];

const EnrollmentBatchWizard = ({ open, onClose }: Props) => {
  const { createBatch, refreshReadiness, academicYear } = useEnrollmentDashboard();
  const { user } = useAuth();
  const [activeStep, setActiveStep] = useState(0);
  const [universityId, setUniversityId] = useState<number | "">("");
  const [collegeId, setCollegeId] = useState<number | "">("");
  const [year, setYear] = useState(academicYear);
  const [courseId, setCourseId] = useState<number | "">("");
  const [groupId, setGroupId] = useState<number | "">("");
  const [semesterId, setSemesterId] = useState<number | "">("");
  const [batch, setBatch] = useState<number | "">("");
  const [previewCount, setPreviewCount] = useState<number | null>(null);
  const [sampleNumbers, setSampleNumbers] = useState<string[]>([]);
  const [courses, setCourses] = useState<Array<{ id: number; name: string }>>([]);
  const [groups, setGroups] = useState<Array<{ id: number; name: string; courseId: number }>>([]);
  const [semesters, setSemesters] = useState<Array<{ id: number; name: string; courseId: number; groupId: number | null }>>([]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    void (async () => {
      try {
        const [courseRes, groupRes, semesterRes] = await Promise.all([
          listMasterCourses(),
          listMasterGroups(),
          listSemesters(),
        ]);
        setCourses(courseRes.data);
        setGroups(groupRes.data);
        setSemesters(semesterRes.data);
        if (user?.tenantId) {
          const college = await getTenantCollege();
          setCollegeId(college.data.id);
          setUniversityId(college.data.universityId);
        }
      } catch (err) {
        setError(getApiErrorMessage(err));
      }
    })();
  }, [open, user?.tenantId]);

  const filteredGroups = useMemo(
    () => (courseId ? groups.filter((g) => g.courseId === courseId) : groups),
    [courseId, groups],
  );

  const filteredSemesters = useMemo(
    () =>
      semesters.filter((s) => {
        if (courseId && s.courseId !== courseId) return false;
        if (groupId && s.groupId !== groupId) return false;
        return true;
      }),
    [semesters, courseId, groupId],
  );

  const loadPreview = async () => {
    if (!collegeId || !year) return;
    setError(null);
    try {
      const res = await enrollmentApiClient.previewBatch({
        tenantId: user?.tenantId ?? 0,
        collegeId: Number(collegeId),
        academicYear: year,
        courseId: courseId ? Number(courseId) : undefined,
        groupId: groupId ? Number(groupId) : undefined,
        batch: batch ? Number(batch) : undefined,
        subjectId: semesterId ? Number(semesterId) : undefined,
      });
      setPreviewCount(res.data.eligibleStudentCount);
      setSampleNumbers(res.data.sampleStudentNumbers);
      await refreshReadiness({
        collegeId: Number(collegeId),
        academicYear: year,
        courseId: courseId ? Number(courseId) : undefined,
        groupId: groupId ? Number(groupId) : undefined,
        batch: batch ? Number(batch) : undefined,
        subjectId: semesterId ? Number(semesterId) : undefined,
      });
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  };

  const handleNext = async () => {
    if (activeStep === 3) {
      await loadPreview();
    }
    setActiveStep((s) => s + 1);
  };

  const handleBack = () => setActiveStep((s) => s - 1);

  const handleSubmit = async () => {
    if (!collegeId || !universityId) return;
    setSubmitting(true);
    const payload: CreateEnrollmentBatchApiRequest = {
      universityId: Number(universityId),
      collegeId: Number(collegeId),
      academicYear: year,
      courseId: courseId ? Number(courseId) : undefined,
      groupId: groupId ? Number(groupId) : undefined,
      batch: batch ? Number(batch) : undefined,
      subjectId: semesterId ? Number(semesterId) : undefined,
    };
    const ok = await createBatch(payload);
    setSubmitting(false);
    if (ok) {
      onClose();
      setActiveStep(0);
    }
  };

  const stepContent = () => {
    switch (activeStep) {
      case 0:
        return (
          <Stack spacing={2}>
            <TextField
              label="College ID"
              type="number"
              value={collegeId}
              onChange={(e) => setCollegeId(Number(e.target.value))}
              helperText="Tenant college identifier from your organization profile."
              fullWidth
            />
            <TextField
              label="University ID"
              type="number"
              value={universityId}
              onChange={(e) => setUniversityId(Number(e.target.value))}
              fullWidth
            />
          </Stack>
        );
      case 1:
        return (
          <TextField
            label="Academic Year"
            type="number"
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            fullWidth
          />
        );
      case 2:
        return (
          <Stack spacing={2}>
            <FormControl fullWidth>
              <InputLabel id="course-label">Course</InputLabel>
              <Select
                labelId="course-label"
                label="Course"
                value={courseId}
                onChange={(e) => {
                  setCourseId(Number(e.target.value));
                  setGroupId("");
                  setSemesterId("");
                }}
              >
                <MenuItem value="">All courses</MenuItem>
                {courses.map((c) => (
                  <MenuItem key={c.id} value={c.id}>
                    {c.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl fullWidth>
              <InputLabel id="group-label">Group</InputLabel>
              <Select
                labelId="group-label"
                label="Group"
                value={groupId}
                onChange={(e) => setGroupId(Number(e.target.value))}
              >
                <MenuItem value="">All groups</MenuItem>
                {filteredGroups.map((g) => (
                  <MenuItem key={g.id} value={g.id}>
                    {g.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl fullWidth>
              <InputLabel id="semester-label">Semester</InputLabel>
              <Select
                labelId="semester-label"
                label="Semester"
                value={semesterId}
                onChange={(e) => setSemesterId(Number(e.target.value))}
              >
                <MenuItem value="">All semesters</MenuItem>
                {filteredSemesters.map((s) => (
                  <MenuItem key={s.id} value={s.id}>
                    {s.name}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField
              label="Section / Batch"
              type="number"
              value={batch}
              onChange={(e) => setBatch(Number(e.target.value))}
              fullWidth
            />
          </Stack>
        );
      case 3:
        return (
          <Stack spacing={1}>
            <Typography variant="body1">
              Eligible students: {previewCount ?? "—"}
            </Typography>
            {sampleNumbers.length > 0 ? (
              <Box component="ul" sx={{ m: 0, pl: 2 }}>
                {sampleNumbers.map((n) => (
                  <li key={n}>
                    <Typography variant="caption">{n}</Typography>
                  </li>
                ))}
              </Box>
            ) : null}
          </Stack>
        );
      case 4:
        return (
          <Typography variant="body2">
            Confirm creation of an enrollment batch for {previewCount ?? 0} students. Processing runs in background
            workers — no AI executes in the browser.
          </Typography>
        );
      default:
        return null;
    }
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="md" aria-labelledby="enrollment-wizard-title">
      <DialogTitle id="enrollment-wizard-title">Start Enrollment Batch</DialogTitle>
      <DialogContent dividers>
        <Stepper activeStep={activeStep} alternativeLabel sx={{ mb: 3 }}>
          {steps.map((label) => (
            <Step key={label}>
              <StepLabel>{label}</StepLabel>
            </Step>
          ))}
        </Stepper>
        {error ? (
          <Typography variant="body2" color="error" sx={{ mb: 2 }}>
            {error}
          </Typography>
        ) : null}
        {stepContent()}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        {activeStep > 0 ? (
          <Button onClick={handleBack}>Back</Button>
        ) : null}
        {activeStep < steps.length - 1 ? (
          <Button variant="contained" onClick={() => void handleNext()}>
            Next
          </Button>
        ) : (
          <Button variant="contained" onClick={() => void handleSubmit()} disabled={submitting}>
            Confirm & Create
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};

export default EnrollmentBatchWizard;

import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  Checkbox,
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
import { getTenantCollege, type TenantCollegeDto } from "../../services/adminService";
import { listMasterCourses, listMasterGroups, listSemesters } from "../../services/setupService";
import { useEnrollmentDashboard } from "../../context/EnrollmentDashboardContext";
import { useTenantContext } from "../../context/TenantContextProvider";
import { useAuth } from "../../context/AuthContext";
import type { CreateEnrollmentBatchApiRequest } from "../../types/enrollment";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import { WIZARD_STEPS, buildScopeFilters, type WizardScopeSelection } from "../../utils/enrollmentWizardUtils";
import WizardContextHeader from "./WizardContextHeader";
import EnrollmentSummaryPreview from "./EnrollmentSummaryPreview";

type Props = {
  open: boolean;
  onClose: () => void;
};

const EnrollmentBatchWizard = ({ open, onClose }: Props) => {
  const {
    createBatch,
    refreshReadiness,
    academicYear,
    configuration,
    dashboard,
    systemStatus,
    readiness,
    collegeId: contextCollegeId,
  } = useEnrollmentDashboard();
  const { context, hasOperationalContext } = useTenantContext();
  const { user } = useAuth();

  const [activeStep, setActiveStep] = useState(0);
  const [collegeProfile, setCollegeProfile] = useState<TenantCollegeDto | null>(null);
  const [year, setYear] = useState(academicYear);
  const [scope, setScope] = useState<WizardScopeSelection>({
    courseId: "",
    groupId: "",
    semesterId: "",
    batch: "",
  });
  const [previewCount, setPreviewCount] = useState<number | null>(null);
  const [sampleNumbers, setSampleNumbers] = useState<string[]>([]);
  const [courses, setCourses] = useState<Array<{ id: number; name: string }>>([]);
  const [groups, setGroups] = useState<Array<{ id: number; name: string; courseId: number }>>([]);
  const [semesters, setSemesters] = useState<Array<{ id: number; name: string; courseId: number; groupId: number | null }>>([]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [forceReEnrollment, setForceReEnrollment] = useState(false);

  const collegeId = contextCollegeId ?? collegeProfile?.id ?? null;
  const universityId = collegeProfile?.universityId ?? null;
  const collegeName = context?.selectedCollegeName ?? collegeProfile?.name ?? "Selected college";
  const universityName = collegeProfile?.universityName ?? "University";

  useEffect(() => {
    if (!open || !hasOperationalContext) return;
    void (async () => {
      try {
        const [courseRes, groupRes, semesterRes, collegeRes] = await Promise.all([
          listMasterCourses(),
          listMasterGroups(),
          listSemesters(),
          getTenantCollege().catch(() => null),
        ]);
        setCourses(courseRes.data);
        setGroups(groupRes.data);
        setSemesters(semesterRes.data);
        if (collegeRes?.data) setCollegeProfile(collegeRes.data);
      } catch (err) {
        setError(getApiErrorMessage(err));
      }
    })();
  }, [open, hasOperationalContext]);

  useEffect(() => {
    if (open) setYear(academicYear);
  }, [open, academicYear]);

  const filteredGroups = useMemo(
    () => (scope.courseId ? groups.filter((g) => g.courseId === scope.courseId) : groups),
    [scope.courseId, groups],
  );

  const filteredSemesters = useMemo(
    () =>
      semesters.filter((s) => {
        if (scope.courseId && s.courseId !== scope.courseId) return false;
        if (scope.groupId && s.groupId !== scope.groupId) return false;
        return true;
      }),
    [semesters, scope.courseId, scope.groupId],
  );

  const scopeLabels = useMemo(
    () => ({
      course: courses.find((c) => c.id === scope.courseId)?.name,
      group: groups.find((g) => g.id === scope.groupId)?.name,
      semester: semesters.find((s) => s.id === scope.semesterId)?.name,
    }),
    [courses, groups, semesters, scope],
  );

  const loadPreview = async () => {
    if (!collegeId || !year) return;
    setError(null);
    const filters = buildScopeFilters(collegeId, year, scope);
    try {
      const res = await enrollmentApiClient.previewBatch({
        tenantId: user?.tenantId ?? context?.tenantId ?? 0,
        collegeId,
        academicYear: year,
        courseId: filters.courseId,
        groupId: filters.groupId,
        batch: filters.batch,
        subjectId: filters.subjectId,
        forceReEnrollment,
      });
      setPreviewCount(res.data.eligibleStudentCount);
      setSampleNumbers(res.data.sampleStudentNumbers);
      await refreshReadiness({ ...filters, academicYear: year, forceReEnrollment: forceReEnrollment ? true : undefined });
    } catch (err) {
      setError(getApiErrorMessage(err));
    }
  };

  const handleNext = async () => {
    if (activeStep === 1) {
      await loadPreview();
    }
    setActiveStep((s) => s + 1);
  };

  const handleBack = () => setActiveStep((s) => s - 1);

  const resetWizard = () => {
    setActiveStep(0);
    setScope({ courseId: "", groupId: "", semesterId: "", batch: "" });
    setPreviewCount(null);
    setSampleNumbers([]);
    setError(null);
    setForceReEnrollment(false);
  };

  const handleSubmit = async () => {
    if (!collegeId || !universityId) return;
    setSubmitting(true);
    const filters = buildScopeFilters(collegeId, year, scope);
    const payload: CreateEnrollmentBatchApiRequest = {
      universityId,
      collegeId,
      academicYear: year,
      courseId: filters.courseId,
      groupId: filters.groupId,
      batch: filters.batch,
      subjectId: filters.subjectId,
      forceReEnrollment,
    };
    const ok = await createBatch(payload);
    setSubmitting(false);
    if (ok) {
      resetWizard();
      onClose();
    }
  };

  const stepContent = () => {
    switch (activeStep) {
      case 0:
        return (
          <TextField
            label="Academic Year"
            type="number"
            value={year}
            onChange={(e) => setYear(Number(e.target.value))}
            fullWidth
            slotProps={{ htmlInput: { "aria-label": "Academic year" } }}
            helperText="First business decision — which academic year to enroll."
          />
        );
      case 1:
        return (
          <Stack spacing={2}>
            <FormControl fullWidth>
              <InputLabel id="course-label">Course</InputLabel>
              <Select
                labelId="course-label"
                label="Course"
                value={scope.courseId}
                onChange={(e) =>
                  setScope({ courseId: Number(e.target.value), groupId: "", semesterId: "", batch: scope.batch })
                }
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
                value={scope.groupId}
                onChange={(e) => setScope({ ...scope, groupId: Number(e.target.value) })}
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
                value={scope.semesterId}
                onChange={(e) => setScope({ ...scope, semesterId: Number(e.target.value) })}
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
              value={scope.batch}
              onChange={(e) => setScope({ ...scope, batch: Number(e.target.value) })}
              fullWidth
            />
            <FormControlLabel
              control={
                <Checkbox
                  checked={forceReEnrollment}
                  onChange={(e) => setForceReEnrollment(e.target.checked)}
                />
              }
              label="Force re-enrollment (include students who already have face embeddings)"
            />
          </Stack>
        );
      case 2:
        return (
          <Stack spacing={2}>
            <EnrollmentSummaryPreview
              collegeName={collegeName}
              universityName={universityName}
              academicYear={year}
              scope={scope}
              scopeLabels={scopeLabels}
              eligibleCount={previewCount}
              readiness={readiness}
              configuration={configuration}
              dashboard={dashboard}
              systemStatus={systemStatus}
            />
            {sampleNumbers.length > 0 ? (
              <Box>
                <Typography variant="caption" color="text.secondary">
                  Sample student numbers
                </Typography>
                <Box component="ul" sx={{ m: 0, pl: 2 }}>
                  {sampleNumbers.map((n) => (
                    <li key={n}>
                      <Typography variant="caption">{n}</Typography>
                    </li>
                  ))}
                </Box>
              </Box>
            ) : null}
          </Stack>
        );
      case 3:
        return (
          <Typography variant="body2">
            Confirm creation of an enrollment batch for {previewCount ?? 0} students at {collegeName}. Processing
            runs in background workers — no AI executes in the browser.
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
        <WizardContextHeader
          collegeName={collegeName}
          collegeCode={context?.selectedCollegeCode ?? collegeProfile?.code}
          universityName={universityName}
          contextCreatedUtc={context?.createdUtc}
        />
        <Stepper activeStep={activeStep} alternativeLabel sx={{ mb: 3 }}>
          {WIZARD_STEPS.map((label) => (
            <Step key={label}>
              <StepLabel>{label}</StepLabel>
            </Step>
          ))}
        </Stepper>
        {error ? (
          <Typography variant="body2" color="error" sx={{ mb: 2 }} role="alert">
            {error}
          </Typography>
        ) : null}
        {!hasOperationalContext ? (
          <Typography variant="body2" color="warning.main" role="alert">
            Select an operational college context before starting enrollment.
          </Typography>
        ) : (
          stepContent()
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        {activeStep > 0 ? <Button onClick={handleBack}>Back</Button> : null}
        {activeStep < WIZARD_STEPS.length - 1 ? (
          <Button variant="contained" onClick={() => void handleNext()} disabled={!hasOperationalContext || !collegeId}>
            Next
          </Button>
        ) : (
          <Button variant="contained" onClick={() => void handleSubmit()} disabled={submitting || !collegeId}>
            Confirm & Create
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};

export default EnrollmentBatchWizard;

import AssignmentOutlinedIcon from "@mui/icons-material/AssignmentOutlined";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import FaceOutlinedIcon from "@mui/icons-material/FaceOutlined";
import PersonIcon from "@mui/icons-material/Person";
import PhotoCameraIcon from "@mui/icons-material/PhotoCamera";
import TaskOutlinedIcon from "@mui/icons-material/TaskOutlined";
import { Alert, Box, Card, CardContent, Grid, Paper, Stack, Typography } from "@mui/material";
import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../../context/AuthContext";
import {
  AttendanceSnackbarProvider,
  useAttendanceSnackbar,
} from "../../context/AttendanceSnackbarContext";
import { useAttendanceSessionPolling } from "../../hooks/useAttendanceSessionPolling";
import { useClassroomPhotoUpload } from "../../hooks/useClassroomPhotoUpload";
import { createInitialAiAttendanceState } from "../../types/aiAttendanceState";
import type { AttendanceContext } from "../../types/attendanceContext";
import { AIStatus } from "../../types/aiWorkflow";
import { AttendanceSessionStatusCode } from "../../utils/attendanceSessionStatus";
import { getAuthenticatedFacultyInfo } from "../../utils/authDisplay";
import {
  isFinalizeVisible,
  isProcessingVisible,
  isReviewVisible,
} from "../../utils/sessionStatusMapper";
import { SessionTimer } from "../common/SessionTimer";
import { AiWorkflowStepper } from "./AiWorkflowStepper";
import { AttendanceContextCard } from "./AttendanceContextCard";
import { ClassroomPhotoUpload } from "./ClassroomPhotoUpload";
import { RecognitionActivityPanel } from "./RecognitionActivityPanel";
import { RecognitionCoverageSummary } from "./RecognitionCoverageSummary";
import { RecognitionErrorPanel } from "./RecognitionErrorPanel";
import { RecognitionProgressSummary } from "./RecognitionProgressSummary";
import { RecognitionProgressTimeline } from "./RecognitionProgressTimeline";
import { RecognitionQueueCard } from "./RecognitionQueueCard";
import { RecognitionReadinessBanner } from "./RecognitionReadinessBanner";
import { RecognitionReviewSection } from "./RecognitionReviewSection";
import { SessionDashboardCard } from "./SessionDashboardCard";
import { WorkflowPlaceholderSection } from "./WorkflowPlaceholderSection";

export type AiAttendancePanelProps = {
  context: AttendanceContext;
  totalStudents: number;
};

const AiAttendancePanelInner = ({ context, totalStudents }: AiAttendancePanelProps) => {
  const { user, token } = useAuth();
  const { notify } = useAttendanceSnackbar();
  const [aiState, setAiState] = useState(createInitialAiAttendanceState);
  const previousStatusRef = useRef(aiState.status);

  const {
    uploadState,
    images,
    canAddMore,
    collectionError,
    handleSelectFile,
    handleSelectFiles,
    handleDeleteImage,
    handleDeleteAllImages,
    handleReplaceImage,
    handleReorderImages,
    handleRetryRecognition,
    handleRetryImageRecognition,
    retryUpload,
    resetUploadState,
    isUploading,
    sessionId,
  } = useClassroomPhotoUpload({
    context,
    totalStudents,
    aiState,
    setAiState,
  });

  const { activityLog } = useAttendanceSessionPolling(aiState.attendanceSessionId, setAiState);

  useEffect(() => {
    const previous = previousStatusRef.current;
    const next = aiState.status;
    if (previous === next) {
      return;
    }

    if (next === AIStatus.Pending || next === AIStatus.Processing) {
      notify("Recognition Started", "info");
    } else if (next === AIStatus.AwaitingReview || next === AIStatus.Completed) {
      notify("Recognition Completed", "success");
    } else if (next === AIStatus.Failed) {
      notify("Recognition Failed", "error");
    }

    previousStatusRef.current = next;
  }, [aiState.status, notify]);

  const filtersReady =
    context.courseId > 0 &&
    context.groupId > 0 &&
    context.semesterId > 0 &&
    context.subjectId > 0 &&
    !!context.attendanceDate;

  const facultyInfo = getAuthenticatedFacultyInfo(token, user?.role);

  const statusCode = aiState.sessionStatusCode ?? AttendanceSessionStatusCode.Draft;

  const queueStatus = aiState.recognitionQueueStatus ?? 0;

  const showProcessingPanel = isProcessingVisible(statusCode, queueStatus) && !isReviewVisible(statusCode);
  const showReviewSection = isReviewVisible(statusCode) && aiState.attendanceSessionId;
  const showFinalizeSection = isFinalizeVisible(statusCode);
  const showErrorPanel =
    aiState.status === AIStatus.Failed || aiState.status === AIStatus.Cancelled;

  const recognitionSessionStatus = useMemo((): AIStatus => {
    if (aiState.status === AIStatus.Processing || aiState.status === AIStatus.AwaitingReview) {
      return aiState.status;
    }
    if (aiState.recognitionQueued || aiState.status === AIStatus.Pending) {
      return AIStatus.Pending;
    }
    return AIStatus.NotStarted;
  }, [aiState.recognitionQueued, aiState.status]);

  return (
    <Stack spacing={2}>
      <Card variant="outlined">
        <CardContent sx={{ py: 1.75, "&:last-child": { pb: 1.75 } }}>
          <Stack spacing={1.5}>
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <PhotoCameraIcon color="primary" sx={{ fontSize: 22 }} />
              <Typography variant="h6" component="h2" sx={{ fontSize: "1.05rem" }}>
                AI Photo Attendance
              </Typography>
            </Stack>

            <Box>
              <Typography variant="subtitle2" gutterBottom sx={{ fontWeight: 700, mb: 1 }}>
                AI Attendance Session
              </Typography>
              <Grid container spacing={1.25} sx={{ mb: 1.25 }}>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <SessionDashboardCard icon={<CheckCircleIcon />} title="Status" status={aiState.status} />
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  {aiState.attendanceSessionId ? (
                    <SessionDashboardCard
                      icon={<AssignmentOutlinedIcon />}
                      title="Attendance Session"
                      eyebrow="Created"
                      sessionId={aiState.attendanceSessionId}
                    />
                  ) : (
                    <SessionDashboardCard
                      icon={<AssignmentOutlinedIcon />}
                      title="Attendance Session"
                      headline="Not Created"
                      subline="Upload a photo to begin"
                    />
                  )}
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <SessionDashboardCard
                    icon={<FaceOutlinedIcon />}
                    title="Recognition Session"
                    status={recognitionSessionStatus}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <RecognitionQueueCard queueStatus={queueStatus} />
                </Grid>
                <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                  <SessionDashboardCard
                    icon={<PersonIcon />}
                    title="Faculty"
                    headline={facultyInfo.name}
                    subline={facultyInfo.title}
                    detailRows={[
                      { label: "Department:", value: facultyInfo.department },
                      { label: "Today's Classes:", value: String(facultyInfo.todaysClasses) },
                    ]}
                  />
                </Grid>
              </Grid>
              <SessionTimer
                startTime={aiState.processingStartTime}
                status={aiState.status}
                elapsedMilliseconds={aiState.elapsedMilliseconds}
              />
            </Box>

            <AttendanceContextCard context={context} />

            {!filtersReady && (
              <Alert severity="info">
                Select course, group, semester, subject, and date to begin AI photo attendance.
              </Alert>
            )}

            {filtersReady && totalStudents <= 0 && (
              <Alert severity="warning">
                Student roster is still loading. Upload will be available once the class roster is loaded.
              </Alert>
            )}

            {filtersReady && (
              <Paper variant="outlined" sx={{ p: 1.5 }}>
                <Typography variant="subtitle2" gutterBottom sx={{ fontWeight: 700 }}>
                  How AI Attendance Works
                </Typography>
                <AiWorkflowStepper currentStep={aiState.workflowStep} />
              </Paper>
            )}
          </Stack>
        </CardContent>
      </Card>

      {filtersReady && (
        <Stack spacing={2}>
          <RecognitionReadinessBanner
            imageCount={images.length}
            status={aiState.status}
            sessionStatusCode={aiState.sessionStatusCode}
            recognitionQueued={aiState.recognitionQueued}
            queueStatus={aiState.recognitionQueueStatus}
            hasFailedImages={images.some((image) => image.status === 4)}
          />

          {images.length > 0 && (
            <RecognitionCoverageSummary
              images={images}
              detectedFaces={aiState.detectedFaces}
              matchedFaces={aiState.matchedFaces}
              unknownFaces={Math.max(0, aiState.detectedFaces - aiState.matchedFaces)}
              status={aiState.status}
              sessionStatusCode={aiState.sessionStatusCode}
              recognitionQueued={aiState.recognitionQueued}
              queueStatus={aiState.recognitionQueueStatus}
              variant="dashboard"
            />
          )}

          <ClassroomPhotoUpload
            disabled={totalStudents <= 0}
            uploadState={uploadState}
            images={images}
            canAddMore={canAddMore}
            collectionError={collectionError}
            sessionId={sessionId ?? aiState.attendanceSessionId}
            detectedFaces={aiState.detectedFaces}
            onSelectFile={handleSelectFile}
            onSelectFiles={handleSelectFiles}
            onDeleteImage={handleDeleteImage}
            onDeleteAllImages={handleDeleteAllImages}
            onReplaceImage={handleReplaceImage}
            onReorderImages={handleReorderImages}
            onRetryRecognition={handleRetryRecognition}
            onRetryImageRecognition={handleRetryImageRecognition}
            onReset={resetUploadState}
            onRetry={retryUpload}
            onNotify={notify}
            isUploading={isUploading}
          />

          {showErrorPanel && (
            <RecognitionErrorPanel
              errorCode={aiState.errorCode}
              processingError={aiState.processingError}
              onRetry={retryUpload}
              retryDisabled={isUploading || (images.length === 0 && !uploadState.selectedFile)}
            />
          )}

          {showProcessingPanel && (
            <RecognitionProgressTimeline
              workflowStep={aiState.workflowStep}
              status={aiState.status}
              queueStatus={aiState.recognitionQueueStatus}
              progressPercent={aiState.recognitionProgress}
              currentStage={aiState.currentStage}
              currentOperation={aiState.currentOperation}
              elapsedMilliseconds={aiState.elapsedMilliseconds}
            />
          )}

          {showReviewSection && (
            <RecognitionReviewSection sessionId={aiState.attendanceSessionId!} />
          )}

          {showFinalizeSection && (
            <WorkflowPlaceholderSection
              icon={<TaskOutlinedIcon />}
              title="Finalize Attendance"
              description="Approve and save official attendance for this session."
              active
            />
          )}

          {(showProcessingPanel || showReviewSection) && (
            <RecognitionActivityPanel entries={activityLog} />
          )}

          <RecognitionProgressSummary
            detectedFaces={aiState.detectedFaces}
            matchedFaces={aiState.matchedFaces}
            reviewedFaces={aiState.reviewedFaces}
            recognitionAccuracy={aiState.recognitionAccuracy ?? null}
            status={aiState.status}
          />
        </Stack>
      )}
    </Stack>
  );
};

export const AiAttendancePanel = (props: AiAttendancePanelProps) => (
  <AttendanceSnackbarProvider>
    <AiAttendancePanelInner {...props} />
  </AttendanceSnackbarProvider>
);

export default AiAttendancePanel;

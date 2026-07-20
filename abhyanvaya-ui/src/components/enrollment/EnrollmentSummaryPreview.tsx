import CalendarTodayOutlinedIcon from "@mui/icons-material/CalendarTodayOutlined";
import FilterAltOutlinedIcon from "@mui/icons-material/FilterAltOutlined";
import GroupsOutlinedIcon from "@mui/icons-material/GroupsOutlined";
import PhotoCameraOutlinedIcon from "@mui/icons-material/PhotoCameraOutlined";
import FaceRetouchingNaturalOutlinedIcon from "@mui/icons-material/FaceRetouchingNaturalOutlined";
import CloudOutlinedIcon from "@mui/icons-material/CloudOutlined";
import MemoryOutlinedIcon from "@mui/icons-material/MemoryOutlined";
import DataObjectOutlinedIcon from "@mui/icons-material/DataObjectOutlined";
import LinearScaleOutlinedIcon from "@mui/icons-material/LinearScaleOutlined";
import SpeedOutlinedIcon from "@mui/icons-material/SpeedOutlined";
import ReplayOutlinedIcon from "@mui/icons-material/ReplayOutlined";
import QueueOutlinedIcon from "@mui/icons-material/QueueOutlined";
import PlayCircleOutlineIcon from "@mui/icons-material/PlayCircleOutlined";
import TimerOutlinedIcon from "@mui/icons-material/TimerOutlined";
import SchoolOutlinedIcon from "@mui/icons-material/SchoolOutlined";
import AccountBalanceOutlinedIcon from "@mui/icons-material/AccountBalanceOutlined";
import type {
  EnrollmentConfigurationDto,
  EnrollmentDashboardDto,
  EnrollmentReadinessResult,
  EnrollmentSystemStatusDto,
} from "../../types/enrollment";
import {
  deriveSimilarityMetric,
  describeEnrollmentScope,
  estimateProcessingSeconds,
  formatEstimatedDuration,
  type WizardScopeSelection,
} from "../../utils/enrollmentWizardUtils";
import EnterpriseSummaryGrid, { type SummaryCardItem } from "./EnterpriseSummaryGrid";

type Props = {
  collegeName: string;
  universityName: string;
  academicYear: number;
  scope: WizardScopeSelection;
  scopeLabels: { course?: string; group?: string; semester?: string };
  eligibleCount: number | null;
  readiness: EnrollmentReadinessResult | null;
  configuration: EnrollmentConfigurationDto | null;
  dashboard: EnrollmentDashboardDto | null;
  systemStatus: EnrollmentSystemStatusDto | null;
};

const readinessStatus = (ready: boolean): SummaryCardItem["status"] => (ready ? "ready" : "warning");

const EnrollmentSummaryPreview = ({
  collegeName,
  universityName,
  academicYear,
  scope,
  scopeLabels,
  eligibleCount,
  readiness,
  configuration,
  dashboard,
  systemStatus,
}: Props) => {
  const scopeDescription = describeEnrollmentScope(scope, scopeLabels);
  const estSeconds = estimateProcessingSeconds(
    eligibleCount ?? readiness?.eligibleStudents ?? 0,
    dashboard?.averageDuration,
    configuration?.downloadThreads ?? 1,
  );
  const queueEstimate = dashboard?.queueLength
    ? formatEstimatedDuration((estSeconds ?? 0) * (dashboard.queueLength + 1))
    : formatEstimatedDuration(estSeconds);

  const warnings: string[] = readiness?.reasons ?? [];
  if (readiness && !readiness.canStart) {
    warnings.push("Enrollment cannot start until all readiness checks pass.");
  }

  const items: SummaryCardItem[] = [
    { icon: <SchoolOutlinedIcon />, label: "College", value: collegeName, status: "neutral" },
    { icon: <AccountBalanceOutlinedIcon />, label: "University", value: universityName, status: "neutral" },
    { icon: <CalendarTodayOutlinedIcon />, label: "Academic Year", value: String(academicYear), status: "neutral" },
    { icon: <FilterAltOutlinedIcon />, label: "Enrollment Scope", value: scopeDescription, status: "neutral" },
    {
      icon: <GroupsOutlinedIcon />,
      label: "Eligible Students",
      value: String(eligibleCount ?? readiness?.eligibleStudents ?? "—"),
      status: (eligibleCount ?? 0) > 0 ? "ready" : "warning",
    },
    {
      icon: <PhotoCameraOutlinedIcon />,
      label: "Photo Provider",
      value: configuration?.photoProvider ?? "—",
      status: readinessStatus(readiness?.photoProviderReady ?? false),
    },
    {
      icon: <FaceRetouchingNaturalOutlinedIcon />,
      label: "Recognition Engine",
      value: configuration?.recognitionEngine ?? "—",
      status: readinessStatus(readiness?.recognitionReady ?? false),
    },
    {
      icon: <CloudOutlinedIcon />,
      label: "Storage Provider",
      value: configuration?.storageProvider ?? "—",
      status: readinessStatus(readiness?.storageReady ?? false),
    },
    {
      icon: <MemoryOutlinedIcon />,
      label: "Embedding Model",
      value: configuration?.embeddingEngine ?? "—",
      status: "neutral",
    },
    {
      icon: <DataObjectOutlinedIcon />,
      label: "Embedding Size",
      value: configuration ? `${configuration.embeddingDimensions} dimensions` : "—",
      status: "neutral",
    },
    {
      icon: <LinearScaleOutlinedIcon />,
      label: "Similarity Metric",
      value: deriveSimilarityMetric(configuration?.recognitionEngine),
      status: "neutral",
    },
    {
      icon: <SpeedOutlinedIcon />,
      label: "Worker Status",
      value: systemStatus?.workerStatus ?? (readiness?.workerReady ? "Live" : "Offline"),
      status: readinessStatus(readiness?.workerReady ?? false),
    },
    {
      icon: <SpeedOutlinedIcon />,
      label: "Download Threads",
      value: configuration ? String(configuration.downloadThreads) : "—",
      status: "neutral",
    },
    {
      icon: <TimerOutlinedIcon />,
      label: "Estimated Processing Time",
      value: formatEstimatedDuration(estSeconds),
      status: estSeconds != null ? "ready" : "neutral",
    },
    {
      icon: <ReplayOutlinedIcon />,
      label: "Retry Policy",
      value: configuration?.retryPolicy ?? "—",
      status: "neutral",
    },
    {
      icon: <QueueOutlinedIcon />,
      label: "Current Queue Length",
      value: String(dashboard?.queueLength ?? 0),
      status: (dashboard?.queueLength ?? 0) > 0 ? "warning" : "ready",
    },
    {
      icon: <PlayCircleOutlineIcon />,
      label: "Active Batches",
      value: readiness?.runningBatchId ? "1 running" : "None",
      status: readiness?.runningBatchId ? "warning" : "ready",
    },
    {
      icon: <TimerOutlinedIcon />,
      label: "Queue Completion Estimate",
      value: queueEstimate,
      status: "neutral",
    },
  ];

  return <EnterpriseSummaryGrid title="Enrollment Summary Preview" items={items} warnings={warnings} />;
};

export default EnrollmentSummaryPreview;
